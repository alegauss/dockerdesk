using System.IO.Pipes;
using System.Text;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// A daemon that answers on a real named pipe. The transport under test is the actual one — a
/// pipe, HTTP over it, and .NET's own parser — so what is faked is only what the engine would say.
/// </summary>
internal sealed class FakeDockerDaemon : IAsyncDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly Dictionary<string, Func<string>> _routes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _prefixes = new(StringComparer.Ordinal);
    private readonly List<string> _requested = [];
    private readonly Lock _guard = new();
    private readonly Task _serving;

    /// <summary>The pipe this is listening on.</summary>
    internal string PipeName { get; } = $"dockerdesk-api-{Guid.NewGuid():N}";

    /// <summary>Start serving.</summary>
    internal FakeDockerDaemon() => _serving = Task.Run(() => ServeAsync(_stopping.Token));

    /// <summary>Every request line this saw, in order.</summary>
    internal IReadOnlyList<string> Requested
    {
        get
        {
            lock (_guard)
            {
                return [.. _requested];
            }
        }
    }

    /// <summary>Answer <paramref name="path"/> with a 200 carrying <paramref name="json"/>.</summary>
    internal FakeDockerDaemon Json(string path, string json) =>
        Raw(path, Http("200 OK", "application/json", json));

    /// <summary>Answer <paramref name="path"/> with a status and a body.</summary>
    internal FakeDockerDaemon Fails(string path, string status, string body) =>
        Raw(path, Http(status, "text/plain", body));

    /// <summary>Answer <paramref name="path"/> with exactly these bytes.</summary>
    internal FakeDockerDaemon Raw(string path, string response)
    {
        lock (_guard)
        {
            _routes[path] = () => response;
        }

        return this;
    }

    /// <summary>
    /// Answer anything beginning with <paramref name="prefix"/>, where an exact route does not.
    /// </summary>
    /// <remarks>
    /// For <c>/events?since=..&amp;until=..</c>, whose query carries a clock. A caller that can fix its
    /// own clock should still use <see cref="Json"/> and assert the exact URL; this is for the guards
    /// that drive every registered verb and cannot.
    /// </remarks>
    internal FakeDockerDaemon JsonPrefix(string prefix, string json)
    {
        lock (_guard)
        {
            _prefixes[prefix] = Http("200 OK", "application/json", json);
        }

        return this;
    }

    private static string Http(string status, string type, string body) =>
        $"HTTP/1.1 {status}\r\nContent-Type: {type}\r\n"
        + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n"
        + "Api-Version: 1.55\r\nConnection: close\r\n\r\n"
        + body;

    private async Task ServeAsync(CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            NamedPipeServerStream server;
            try
            {
                server = new NamedPipeServerStream(
                    PipeName, PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellation).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException
                or ObjectDisposedException or IOException)
            {
                return;
            }

            _ = AnswerAsync(server, cancellation);
        }
    }

    private async Task AnswerAsync(NamedPipeServerStream client, CancellationToken cancellation)
    {
        try
        {
            var request = await ReadRequestAsync(client, cancellation).ConfigureAwait(false);
            if (request is null)
            {
                return;
            }

            lock (_guard)
            {
                _requested.Add(request);
            }

            // The request line is `GET /v1.43/version HTTP/1.1`; routes are keyed by the path.
            var path = request.Split(' ') is [_, var target, ..] ? target : "";
            string response;
            lock (_guard)
            {
                response = _routes.TryGetValue(path, out var canned)
                    ? canned()
                    : _prefixes.FirstOrDefault(p =>
                        path.StartsWith(p.Key, StringComparison.Ordinal)).Value
                      ?? Http("404 Not Found", "text/plain", $"no route for {path}");
            }

            var bytes = Encoding.UTF8.GetBytes(response);
            await client.WriteAsync(bytes, cancellation).ConfigureAwait(false);
            await client.FlushAsync(cancellation).ConfigureAwait(false);
            client.WaitForPipeDrain();
        }
        catch (Exception exception) when (exception is IOException
            or ObjectDisposedException or OperationCanceledException)
        {
            // A client that hung up is not this fake failing.
        }
        finally
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<string?> ReadRequestAsync(
        Stream client, CancellationToken cancellation)
    {
        var buffer = new byte[8192];
        var head = new StringBuilder();
        while (!head.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await client.ReadAsync(buffer, cancellation).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            head.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        return head.ToString().Split("\r\n")[0];
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await _serving.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or ObjectDisposedException or IOException)
        {
            // Shutting down.
        }

        _stopping.Dispose();
    }
}
