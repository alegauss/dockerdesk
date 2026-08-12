using System.Buffers.Binary;
using System.Text;

namespace DockerDesk.Core.Api;

/// <summary>Which of a container's two outputs a chunk came from.</summary>
public enum LogStream
{
    /// <summary>stdin, which a log never carries and the protocol still numbers.</summary>
    StdIn = 0,

    /// <summary>Ordinary output.</summary>
    StdOut = 1,

    /// <summary>Errors, which is where a container that died two seconds in usually says why.</summary>
    StdErr = 2,
}

/// <summary>One piece of a container's output, as it came off the wire.</summary>
/// <param name="Stream">Where it came from.</param>
/// <param name="Text">The bytes, decoded. Not a line: a frame can end mid-sentence.</param>
public sealed record LogChunk(LogStream Stream, string Text);

/// <summary>
/// Reads a container's log stream, de-framed when the daemon framed it.
/// </summary>
/// <remarks>
/// A container started without a TTY has its stdout and stderr multiplexed into one stream, and
/// each chunk is preceded by eight bytes: the stream number, three zero bytes, then the payload
/// length as a big-endian unsigned int. Rendered without stripping those, every chunk shows visible
/// control characters and the first character of each line is eaten. That is the bug this class
/// exists to not ship.
///
/// With a TTY there are no headers at all — the daemon hands over what the terminal would have
/// shown — so the same reader run in framed mode would consume eight characters of real output per
/// read. Which mode applies is not guessable from the bytes, so it is asked of the daemon
/// (<see cref="DockerApi.InspectAsync"/>) and passed in.
/// </remarks>
public sealed class LogFrames
{
    /// <summary>The size of a frame header, in bytes.</summary>
    public const int HeaderSize = 8;

    /// <summary>
    /// The largest payload a single frame may claim before the stream is treated as desynced.
    /// </summary>
    /// <remarks>
    /// The length is four bytes off the wire, so a frame can ask for four gigabytes. Reading it as
    /// a size and allocating it is how a log window becomes an out-of-memory crash: any stream that
    /// is not actually framed — a TTY container this client got wrong about — makes the first four
    /// bytes of ordinary text into a length. No real frame is anywhere near this, so a bigger one
    /// means the reader has lost the frame boundary and there is nothing to recover.
    /// </remarks>
    public const int MaximumFrameSize = 16 * 1024 * 1024;

    /// <summary>How much of an unframed stream is taken in one read.</summary>
    private const int RawReadSize = 8192;

    private readonly Stream _stream;
    private readonly bool _framed;
    private readonly byte[] _header = new byte[HeaderSize];
    private readonly byte[] _raw;

    // A decoder rather than Encoding.UTF8.GetString: a frame boundary can fall in the middle of a
    // multi-byte character, and decoding each frame independently turns that into two replacement
    // characters. The decoder holds the incomplete sequence until the next frame supplies the rest.
    private readonly Decoder _utf8 = Encoding.UTF8.GetDecoder();

    /// <summary>Construct a reader.</summary>
    /// <param name="stream">The response body from <see cref="DockerApi.LogsAsync"/>.</param>
    /// <param name="framed">
    /// <see langword="true"/> when the container has no TTY, which is when the daemon frames.
    /// </param>
    public LogFrames(Stream stream, bool framed)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _framed = framed;
        _raw = framed ? [] : new byte[RawReadSize];
    }

    /// <summary>
    /// The next chunk, or <see langword="null"/> when the stream ended.
    /// </summary>
    /// <param name="cancellation">Cancellation. Closing it is how a followed log is stopped.</param>
    /// <returns>The chunk, or nothing.</returns>
    public async Task<LogChunk?> ReadAsync(CancellationToken cancellation = default)
    {
        if (!_framed)
        {
            var got = await _stream.ReadAsync(_raw, cancellation).ConfigureAwait(false);

            // Everything a TTY writes is one stream; the daemon is handing over what a terminal
            // would have shown, and stderr is already interleaved into it.
            return got == 0 ? null : new LogChunk(LogStream.StdOut, Decode(_raw, got));
        }

        if (!await FillAsync(_header, HeaderSize, cancellation).ConfigureAwait(false))
        {
            return null;
        }

        var size = BinaryPrimitives.ReadUInt32BigEndian(_header.AsSpan(4, 4));
        if (size == 0)
        {
            // A legal frame carrying nothing. Returning an empty chunk rather than null keeps
            // "nothing left" as the one meaning of null.
            return new LogChunk(StreamOf(_header[0]), "");
        }

        if (size > MaximumFrameSize)
        {
            // Not a frame. Ending the stream is the only honest answer: every byte after this point
            // is being read at the wrong offset, so there is no way back to a boundary.
            return null;
        }

        var payload = new byte[size];
        return await FillAsync(payload, (int)size, cancellation).ConfigureAwait(false)
            ? new LogChunk(StreamOf(_header[0]), Decode(payload, (int)size))

            // A header that promised more than the stream had. The daemon was killed mid-frame;
            // there is no partial chunk worth showing, and the stream is over either way.
            : null;
    }

    /// <summary>
    /// Read every chunk until the stream ends.
    /// </summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The chunks, in order.</returns>
    public async IAsyncEnumerable<LogChunk> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellation = default)
    {
        while (await ReadAsync(cancellation).ConfigureAwait(false) is { } chunk)
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Fill <paramref name="count"/> bytes, because a stream read returns what has arrived and a
    /// frame header split across two TCP reads is ordinary rather than exceptional.
    /// </summary>
    private async Task<bool> FillAsync(byte[] buffer, int count, CancellationToken cancellation)
    {
        var filled = 0;
        while (filled < count)
        {
            var got = await _stream
                .ReadAsync(buffer.AsMemory(filled, count - filled), cancellation)
                .ConfigureAwait(false);
            if (got == 0)
            {
                return false;
            }

            filled += got;
        }

        return true;
    }

    private string Decode(byte[] buffer, int count)
    {
        var chars = new char[_utf8.GetCharCount(buffer, 0, count, flush: false)];
        var written = _utf8.GetChars(buffer, 0, count, chars, 0, flush: false);
        return new string(chars, 0, written);
    }

    /// <summary>
    /// Read the stream byte, treating anything unknown as ordinary output.
    /// </summary>
    /// <remarks>
    /// A daemon newer than the pinned API version numbering a third stream is a line this build
    /// cannot classify, not a line worth dropping.
    /// </remarks>
    private static LogStream StreamOf(byte number) => number switch
    {
        0 => LogStream.StdIn,
        2 => LogStream.StdErr,
        _ => LogStream.StdOut,
    };
}
