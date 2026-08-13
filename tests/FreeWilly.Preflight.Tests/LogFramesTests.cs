using System.Buffers.Binary;
using System.Text;
using FreeWilly.Core.Api;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The de-framer. "Rendered without de-framing, every line carries visible control bytes, and that
/// is the bug this task exists to not ship" — so these are the tests that would have caught it.
/// </summary>
public sealed class LogFramesTests
{
    /// <summary>One frame: the stream number, three zeroes, the length big-endian, the payload.</summary>
    private static byte[] Frame(byte stream, string payload)
    {
        var body = Encoding.UTF8.GetBytes(payload);
        var frame = new byte[LogFrames.HeaderSize + body.Length];
        frame[0] = stream;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4, 4), (uint)body.Length);
        body.CopyTo(frame, LogFrames.HeaderSize);
        return frame;
    }

    private static Stream Wire(params byte[][] frames) =>
        new MemoryStream([.. frames.SelectMany(f => f)]);

    private static async Task<List<LogChunk>> DrainAsync(LogFrames frames)
    {
        var chunks = new List<LogChunk>();
        await foreach (var chunk in frames.ReadAllAsync())
        {
            chunks.Add(chunk);
        }

        return chunks;
    }

    // ---- the framed stream ----------------------------------------------------------------------

    [Fact]
    public async Task The_eight_byte_header_never_reaches_the_text()
    {
        // The bug this exists to prevent: the header rendered as text is a NUL, three more, and the
        // length bytes, in front of every chunk.
        var frames = new LogFrames(Wire(Frame(1, "listening on :8080\n")), framed: true);

        var chunks = await DrainAsync(frames);

        Assert.Single(chunks);
        Assert.Equal("listening on :8080\n", chunks[0].Text);
        Assert.DoesNotContain('\0', chunks[0].Text);
    }

    [Fact]
    public async Task Stdout_and_stderr_come_back_told_apart()
    {
        // The whole reason the daemon frames at all: one stream carrying two.
        var frames = new LogFrames(
            Wire(Frame(1, "starting\n"), Frame(2, "panic: nil map\n")), framed: true);

        var chunks = await DrainAsync(frames);

        Assert.Equal(LogStream.StdOut, chunks[0].Stream);
        Assert.Equal(LogStream.StdErr, chunks[1].Stream);
        Assert.Equal("panic: nil map\n", chunks[1].Text);
    }

    [Fact]
    public async Task A_header_split_across_two_reads_is_still_one_frame()
    {
        // A stream read returns what has arrived, not what was asked for. A header landing in two
        // TCP segments is ordinary, and a reader that assumes otherwise loses the frame.
        var whole = Frame(1, "hello\n");
        var frames = new LogFrames(new DribblingStream(whole, 3), framed: true);

        var chunks = await DrainAsync(frames);

        Assert.Single(chunks);
        Assert.Equal("hello\n", chunks[0].Text);
    }

    [Fact]
    public async Task A_payload_split_across_reads_is_still_one_frame()
    {
        var whole = Frame(2, "a long line that will not arrive in one piece\n");
        var frames = new LogFrames(new DribblingStream(whole, 7), framed: true);

        var chunks = await DrainAsync(frames);

        Assert.Single(chunks);
        Assert.Equal("a long line that will not arrive in one piece\n", chunks[0].Text);
    }

    [Fact]
    public async Task A_frame_carrying_nothing_is_a_frame_and_not_the_end_of_the_stream()
    {
        var frames = new LogFrames(
            Wire(Frame(1, ""), Frame(1, "after\n")), framed: true);

        var chunks = await DrainAsync(frames);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("", chunks[0].Text);
        Assert.Equal("after\n", chunks[1].Text);
    }

    [Fact]
    public async Task A_utf8_character_split_across_two_frames_survives()
    {
        // The daemon frames by chunk, not by character. Decoding each frame independently turns a
        // two-byte character straddling the boundary into two replacement characters.
        var text = Encoding.UTF8.GetBytes("café\n");
        var head = text[..4];
        var tail = text[4..];

        var first = new byte[LogFrames.HeaderSize + head.Length];
        first[0] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(first.AsSpan(4, 4), (uint)head.Length);
        head.CopyTo(first, LogFrames.HeaderSize);

        var second = new byte[LogFrames.HeaderSize + tail.Length];
        second[0] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(second.AsSpan(4, 4), (uint)tail.Length);
        tail.CopyTo(second, LogFrames.HeaderSize);

        var frames = new LogFrames(Wire(first, second), framed: true);

        var chunks = await DrainAsync(frames);

        Assert.Equal("café\n", string.Concat(chunks.Select(c => c.Text)));
        Assert.DoesNotContain('�', string.Concat(chunks.Select(c => c.Text)));
    }

    [Fact]
    public async Task A_header_that_promised_more_than_arrived_ends_the_stream_rather_than_hanging()
    {
        // The daemon was killed mid-frame. There is no partial chunk worth showing.
        var truncated = Frame(1, "twenty characters!!!")[..12];
        var frames = new LogFrames(new MemoryStream(truncated), framed: true);

        var chunks = await DrainAsync(frames);

        Assert.Empty(chunks);
    }

    [Fact]
    public async Task A_frame_claiming_more_than_any_real_one_ends_the_stream_instead_of_allocating_it()
    {
        // Measured the hard way: reading this length wrong made the reader ask for a 300 MB buffer
        // per frame and the test run never finished. The length is four bytes off the wire, so a
        // desynced stream — a TTY container this client got wrong about — can claim four gigabytes.
        var header = new byte[LogFrames.HeaderSize];
        header[0] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(
            header.AsSpan(4, 4), (uint)LogFrames.MaximumFrameSize + 1);
        var frames = new LogFrames(new MemoryStream(header), framed: true);

        var chunks = await DrainAsync(frames);

        Assert.Empty(chunks);
    }

    [Fact]
    public async Task A_frame_at_the_ceiling_is_still_read()
    {
        // The bound is a sanity check on a desynced stream, not a cap on real output.
        var payload = new string('x', 64_000);
        var frames = new LogFrames(Wire(Frame(1, payload)), framed: true);

        var chunks = await DrainAsync(frames);

        Assert.Equal(payload, string.Concat(chunks.Select(c => c.Text)));
    }

    [Fact]
    public async Task A_stream_number_this_build_does_not_know_is_shown_rather_than_dropped()
    {
        // A daemon newer than the pinned API version numbering a third stream is not a reason to
        // hide output.
        var frames = new LogFrames(Wire(Frame(7, "from somewhere new\n")), framed: true);

        var chunks = await DrainAsync(frames);

        Assert.Single(chunks);
        Assert.Equal("from somewhere new\n", chunks[0].Text);
    }

    // ---- the TTY stream, which has no headers at all --------------------------------------------

    [Fact]
    public async Task A_tty_stream_is_passed_through_untouched()
    {
        // Run in framed mode this would eat the first eight characters of real output.
        var raw = new MemoryStream(Encoding.UTF8.GetBytes("interactive output, no headers\n"));
        var frames = new LogFrames(raw, framed: false);

        var chunks = await DrainAsync(frames);

        Assert.Equal("interactive output, no headers\n", string.Concat(chunks.Select(c => c.Text)));
    }

    [Fact]
    public async Task A_tty_stream_reports_one_stream_because_that_is_what_a_terminal_is()
    {
        var raw = new MemoryStream(Encoding.UTF8.GetBytes("anything\n"));
        var frames = new LogFrames(raw, framed: false);

        var chunks = await DrainAsync(frames);

        Assert.All(chunks, chunk => Assert.Equal(LogStream.StdOut, chunk.Stream));
    }

    /// <summary>A stream that hands back at most <c>bite</c> bytes per read, like a socket does.</summary>
    private sealed class DribblingStream(byte[] content, int bite) : Stream
    {
        private int _position;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var left = content.Length - _position;
            var give = Math.Min(Math.Min(bite, count), left);
            Array.Copy(content, _position, buffer, offset, give);
            _position += give;
            return give;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => content.Length;

        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
