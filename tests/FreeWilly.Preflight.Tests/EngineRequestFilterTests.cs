using System.Text;
using System.Text.Json;
using FreeWilly.Core.Engine;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The relay's client-side pump, which reads HTTP only as far as it must (DD125).
/// </summary>
/// <remarks>
/// Every test here is really one assertion: what reached the daemon is either exactly what the client
/// sent, or that with one bind source respelled and the length corrected. This sits under every
/// Docker client on the machine, so a corrupted stream is a worse defect than an untranslated bind —
/// which is why most of these drive shapes this deliberately does not understand.
/// </remarks>
public class EngineRequestFilterTests
{
    private static async Task<byte[]> Through(byte[] sent)
    {
        var to = new MemoryStream();
        await EngineRequestFilter.PumpAsync(new MemoryStream(sent), to, CancellationToken.None);
        return to.ToArray();
    }

    private static async Task<string> ThroughText(string sent) =>
        Encoding.ASCII.GetString(await Through(Encoding.ASCII.GetBytes(sent)));

    private static string Request(string body, string target = "/v1.51/containers/create?name=x") =>
        $"POST {target} HTTP/1.1\r\n"
        + "Host: docker\r\n"
        + "User-Agent: Docker-Client/28.0.0 (windows)\r\n"
        + "Content-Type: application/json\r\n"
        + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n"
        + "\r\n"
        + body;

    /// <summary>The body of the one request in a forwarded stream, by its declared length.</summary>
    private static string BodyOf(string forwarded)
    {
        var split = forwarded.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        Assert.True(split > 0, "the forwarded stream carries no head");

        var head = forwarded[..split];
        var body = forwarded[(split + 4)..];

        // The assertion that matters most: a length that disagrees with the body hangs the daemon or
        // makes it read the next request as this one's payload.
        var declared = head.Split("\r\n")
            .First(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            int.Parse(declared["Content-Length:".Length..].Trim()),
            Encoding.UTF8.GetByteCount(body));

        return body;
    }

    [Fact]
    public async Task A_create_with_a_Windows_bind_reaches_the_daemon_respelled()
    {
        var forwarded = await ThroughText(
            Request("""{"Image":"aem","HostConfig":{"Binds":["D:\\p\\data:/data:rw"]}}"""));

        var body = JsonDocument.Parse(BodyOf(forwarded));
        Assert.Equal(
            "/mnt/d/p/data:/data:rw",
            body.RootElement.GetProperty("HostConfig").GetProperty("Binds")
                .EnumerateArray().Single().GetString());

        // The head is the client's own but for the one length. Nothing else was re-emitted.
        Assert.Contains("User-Agent: Docker-Client/28.0.0 (windows)", forwarded, StringComparison.Ordinal);
        Assert.Contains("POST /v1.51/containers/create?name=x HTTP/1.1", forwarded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_create_with_nothing_to_respell_is_forwarded_byte_for_byte()
    {
        var sent = Request("""{"Image":"aem","HostConfig":{"Binds":["named:/data"]}}""");
        Assert.Equal(sent, await ThroughText(sent));
    }

    [Fact]
    public async Task A_request_that_is_not_a_create_is_forwarded_byte_for_byte()
    {
        // The overwhelming majority of the traffic. `docker ps`, `docker logs`, the event stream the
        // tray holds open — none of it is parsed beyond finding where the next head starts.
        var sent = "GET /v1.51/containers/json?all=1 HTTP/1.1\r\nHost: docker\r\n\r\n";
        Assert.Equal(sent, await ThroughText(sent));
    }

    [Fact]
    public async Task A_create_on_a_bare_path_with_no_version_prefix_is_still_a_create()
    {
        var forwarded = await ThroughText(
            Request("""{"HostConfig":{"Binds":["D:\\p:/w"]}}""", target: "/containers/create"));

        Assert.Contains("/mnt/d/p:/w", forwarded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_path_that_merely_ends_in_a_similar_word_is_not_a_create()
    {
        // `/images/create` and `/networks/create` are real endpoints with bodies of their own, and
        // neither carries a bind. Matching on the wrong tail would have this rewrite them.
        var sent = Request("""{"HostConfig":{"Binds":["D:\\p:/w"]}}""", target: "/v1.51/images/create");
        Assert.Equal(sent, await ThroughText(sent));
    }

    [Fact]
    public async Task Two_requests_on_one_connection_are_both_seen()
    {
        // Keep-alive is what every Docker client uses, and compose brings up several containers on
        // one connection. A filter that parsed only the first request would translate one service of
        // a project and silently miss the rest.
        var first = Request("""{"HostConfig":{"Binds":["D:\\one:/a"]}}""");
        var second = Request("""{"HostConfig":{"Binds":["D:\\two:/b"]}}""");

        var forwarded = await ThroughText(first + second);

        Assert.Contains("/mnt/d/one:/a", forwarded, StringComparison.Ordinal);
        Assert.Contains("/mnt/d/two:/b", forwarded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_create_split_across_reads_is_still_assembled()
    {
        // A named pipe delivers what it delivers. The head, the body and even the blank line between
        // them can arrive in separate reads, and a parser that assumed one read per request would
        // work on every developer machine and fail on a loaded one.
        var sent = Encoding.ASCII.GetBytes(
            Request("""{"HostConfig":{"Binds":["D:\\p:/w"]}}"""));

        var to = new MemoryStream();
        await EngineRequestFilter.PumpAsync(new DribblingStream(sent), to, CancellationToken.None);

        Assert.Contains("/mnt/d/p:/w", Encoding.ASCII.GetString(to.ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_chunked_body_is_forwarded_with_its_framing_intact()
    {
        // What `docker build` sends. It is streamed rather than collected, so the framing has to be
        // walked to find where the request ends — and re-emitted exactly, sizes included.
        var sent = "POST /v1.51/build HTTP/1.1\r\nHost: docker\r\n"
            + "Transfer-Encoding: chunked\r\n\r\n"
            + "5\r\nhello\r\n"
            + "6\r\n world\r\n"
            + "0\r\n\r\n";

        Assert.Equal(sent, await ThroughText(sent));
    }

    [Fact]
    public async Task A_chunked_create_is_forwarded_rather_than_collected()
    {
        // No client sends one, and supporting it would mean buffering a body with no declared size.
        // Forwarding it untranslated is the same outcome the relay had before DD125.
        var sent = "POST /v1.51/containers/create HTTP/1.1\r\nHost: docker\r\n"
            + "Transfer-Encoding: chunked\r\n\r\n"
            + "24\r\n{\"HostConfig\":{\"Binds\":[\"D:\\\\p:/w\"]}}\r\n"
            + "0\r\n\r\n";

        Assert.Equal(sent, await ThroughText(sent));
    }

    [Fact]
    public async Task A_request_that_upgrades_hands_the_rest_of_the_connection_over_raw()
    {
        // `attach` and `exec` stop being HTTP after the response: the connection becomes the
        // container's own stdio, and bytes in it can look like anything at all — including something
        // that reads as a head. Parsing past the upgrade is how this would corrupt a terminal.
        var sent = "POST /v1.51/containers/abc/attach?stream=1 HTTP/1.1\r\n"
            + "Host: docker\r\nConnection: Upgrade\r\nUpgrade: tcp\r\n\r\n"
            + "POST /v1.51/containers/create HTTP/1.1\r\nContent-Length: 3\r\n\r\nraw";

        Assert.Equal(sent, await ThroughText(sent));
    }

    [Fact]
    public async Task A_head_too_long_to_be_a_head_is_forwarded_rather_than_parsed()
    {
        // The safety valve. Something that is not HTTP on this pipe is still somebody's connection,
        // and the relay's job is to carry it.
        var sent = new string('x', EngineRequestFilter.MaxHeadLength + 10);
        Assert.Equal(sent, await ThroughText(sent));
    }

    [Fact]
    public async Task A_malformed_request_line_is_forwarded_rather_than_parsed()
    {
        var sent = "GARBAGE\r\nHost: docker\r\n\r\nand whatever followed it";
        Assert.Equal(sent, await ThroughText(sent));
    }

    [Fact]
    public async Task An_empty_connection_forwards_nothing()
    {
        Assert.Empty(await Through([]));
    }

    [Fact]
    public async Task A_create_body_over_the_cap_is_forwarded_untranslated()
    {
        // Declared, not measured: a Content-Length far past the cap must not become an allocation.
        var sent = $"POST /v1.51/containers/create HTTP/1.1\r\nHost: docker\r\n"
            + $"Content-Length: {(long)EngineRequestFilter.MaxCollectedBody + 1}\r\n\r\n"
            + "the body this claims to be does not have to arrive";

        Assert.Equal(sent, await ThroughText(sent));
    }

    /// <summary>A stream that answers one byte at a time, however much was asked for.</summary>
    /// <remarks>
    /// The pathological delivery a named pipe is allowed to make. Anything that survives this
    /// survives a real one.
    /// </remarks>
    private sealed class DribblingStream(byte[] content) : Stream
    {
        private int _position;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= content.Length || count == 0)
            {
                return 0;
            }

            buffer[offset] = content[_position++];
            return 1;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position >= content.Length || buffer.Length == 0)
            {
                return ValueTask.FromResult(0);
            }

            buffer.Span[0] = content[_position++];
            return ValueTask.FromResult(1);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => content.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
