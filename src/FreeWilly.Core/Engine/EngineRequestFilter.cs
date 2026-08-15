using System.Text;

namespace FreeWilly.Core.Engine;

/// <summary>
/// The client-to-daemon half of the relay, reading enough HTTP to respell a bind source (DD125).
/// </summary>
/// <remarks>
/// <b>Everything not understood is forwarded byte for byte.</b> This sits under every Docker client on
/// the machine, so the failure to design against is not "a bind was missed" — it is a stream this
/// corrupted. Every decision below therefore falls the same way: a head too long to be a head, a
/// framing that does not parse, a body over the cap, a create payload that is not JSON — each one
/// stops the parsing and hands the rest of the connection to a raw copy. The daemon then answers for
/// itself, which is the behaviour of the relay before this existed.
///
/// <para><b>Only <c>POST …/containers/create</c> is ever buffered.</b> A <c>POST /build</c> carries a
/// whole context tar and a <c>PUT …/archive</c> carries a file; holding either in memory to look for
/// a bind would trade a fixed defect for a new one. Those are framed and streamed, never collected.
/// </para>
///
/// <para><b>An upgrade ends the parsing for good.</b> <c>attach</c>, <c>exec</c> and the websocket
/// endpoints hand the connection to the container and it stops being HTTP at all; the request is
/// forwarded and the rest of the socket is copied raw for as long as it lives.</para>
///
/// <para><b>The response direction is untouched.</b> Nothing in a response names a bind source that
/// this had to change, and a second parser is a second thing to get wrong.</para>
/// </remarks>
public static class EngineRequestFilter
{
    /// <summary>How much of a create payload is collected before it is forwarded unread.</summary>
    /// <remarks>
    /// Far above any real create — they are kilobytes — and finite because the number is what stops
    /// a malformed <c>Content-Length</c> from being an allocation. A payload over this is forwarded
    /// untranslated rather than refused, which is exactly what happened before DD125.
    /// </remarks>
    internal const int MaxCollectedBody = 16 * 1024 * 1024;

    /// <summary>How long a request head may be before this stops trying to parse the connection.</summary>
    internal const int MaxHeadLength = 128 * 1024;

    /// <summary>Forward this connection, respelling the bind sources it carries.</summary>
    /// <param name="from">The client.</param>
    /// <param name="to">The daemon.</param>
    /// <param name="cancellation">Stops the copy.</param>
    public static async Task PumpAsync(Stream from, Stream to, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var reader = new Reader(from);

        while (!cancellation.IsCancellationRequested)
        {
            var head = await reader.ReadHeadAsync(cancellation).ConfigureAwait(false);
            if (head is null)
            {
                // Either the client hung up cleanly, or what it sent is not a head this understands.
                await reader.CopyRestAsync(to, cancellation).ConfigureAwait(false);
                return;
            }

            var request = RequestHead.Parse(head);
            if (request is null)
            {
                await to.WriteAsync(head, cancellation).ConfigureAwait(false);
                await to.FlushAsync(cancellation).ConfigureAwait(false);
                await reader.CopyRestAsync(to, cancellation).ConfigureAwait(false);
                return;
            }

            await ForwardAsync(reader, to, head, request, cancellation).ConfigureAwait(false);

            if (request.Upgrades)
            {
                await reader.CopyRestAsync(to, cancellation).ConfigureAwait(false);
                return;
            }
        }
    }

    private static async Task ForwardAsync(
        Reader reader, Stream to, byte[] head, RequestHead request, CancellationToken cancellation)
    {
        if (request.IsContainerCreate
            && !request.Chunked
            && request.ContentLength is > 0 and <= MaxCollectedBody)
        {
            var body = await reader
                .ReadExactlyAsync((int)request.ContentLength.Value, cancellation)
                .ConfigureAwait(false);

            if (body is not null && ContainerCreateRewrite.TryRewrite(body, out var rewritten))
            {
                // The head is rebuilt only for the one header whose value the new body changed.
                // Everything else the client sent travels exactly as it wrote it.
                var patched = request.WithContentLength(head, rewritten.Length);
                await to.WriteAsync(patched, cancellation).ConfigureAwait(false);
                await to.WriteAsync(rewritten, cancellation).ConfigureAwait(false);
                await to.FlushAsync(cancellation).ConfigureAwait(false);
                return;
            }

            await to.WriteAsync(head, cancellation).ConfigureAwait(false);
            if (body is not null)
            {
                await to.WriteAsync(body, cancellation).ConfigureAwait(false);
            }

            await to.FlushAsync(cancellation).ConfigureAwait(false);
            return;
        }

        await to.WriteAsync(head, cancellation).ConfigureAwait(false);
        await to.FlushAsync(cancellation).ConfigureAwait(false);

        if (request.Chunked)
        {
            await reader.CopyChunkedAsync(to, cancellation).ConfigureAwait(false);
        }
        else if (request.ContentLength is > 0)
        {
            await reader.CopyExactlyAsync(to, request.ContentLength.Value, cancellation)
                .ConfigureAwait(false);
        }
    }

    /// <summary>The few things about a request head that decide what happens to its body.</summary>
    internal sealed record RequestHead
    {
        /// <summary>Whether this is the one request whose body is worth reading.</summary>
        internal bool IsContainerCreate { get; init; }

        /// <summary>The declared body length, or null where none was declared.</summary>
        internal long? ContentLength { get; init; }

        /// <summary>Whether the body is chunk-framed rather than length-framed.</summary>
        internal bool Chunked { get; init; }

        /// <summary>Whether this request turns the connection into something that is not HTTP.</summary>
        internal bool Upgrades { get; init; }

        /// <summary>Read a head, or answer null for anything that is not one.</summary>
        /// <param name="head">The bytes up to and including the blank line.</param>
        /// <returns>What was understood, or null.</returns>
        internal static RequestHead? Parse(byte[] head)
        {
            string text;
            try
            {
                text = Encoding.ASCII.GetString(head);
            }
            catch (ArgumentException)
            {
                return null;
            }

            var lines = text.Split("\r\n");
            if (lines.Length == 0)
            {
                return null;
            }

            var start = lines[0].Split(' ');
            if (start.Length < 3)
            {
                return null;
            }

            long? contentLength = null;
            var chunked = false;
            var upgrades = false;

            foreach (var line in lines.Skip(1))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                var name = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();

                if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    // An unparseable length is not a length. Leaving it null sends the request
                    // through the streaming path, which forwards what is there and invents nothing.
                    contentLength = long.TryParse(value, out var declared) && declared >= 0
                        ? declared
                        : null;
                }
                else if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    chunked = value.Contains("chunked", StringComparison.OrdinalIgnoreCase);
                }
                else if (name.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                {
                    upgrades = value.Contains("upgrade", StringComparison.OrdinalIgnoreCase);
                }
            }

            return new RequestHead
            {
                IsContainerCreate = IsCreate(start[0], start[1]),
                ContentLength = contentLength,
                Chunked = chunked,
                Upgrades = upgrades,
            };
        }

        /// <summary>Whether a method and target name the create endpoint.</summary>
        /// <remarks>
        /// The version prefix is whatever the client negotiated — <c>/v1.51/containers/create</c> and
        /// a bare <c>/containers/create</c> are the same endpoint — so the path is matched by its
        /// tail rather than by a version this would have to keep up with. The query string carries
        /// <c>?name=</c> and says nothing about the body.
        /// </remarks>
        private static bool IsCreate(string method, string target)
        {
            if (!method.Equals("POST", StringComparison.Ordinal))
            {
                return false;
            }

            var query = target.IndexOf('?');
            var path = query < 0 ? target : target[..query];

            return path.EndsWith("/containers/create", StringComparison.Ordinal);
        }

        /// <summary>The same head with one header's value replaced.</summary>
        /// <remarks>
        /// Rebuilt from the original text rather than re-emitted from parsed fields: a head this
        /// re-serialised would drop every header this does not model, and the client's own
        /// <c>User-Agent</c>, <c>Content-Type</c> and version negotiation are not this relay's to
        /// edit. Only the one line whose value is now wrong is touched.
        /// </remarks>
        /// <param name="head">The head as it arrived.</param>
        /// <param name="length">What the body now measures.</param>
        /// <returns>The head to forward.</returns>
        internal byte[] WithContentLength(byte[] head, int length)
        {
            var lines = Encoding.ASCII.GetString(head).Split("\r\n");
            for (var index = 0; index < lines.Length; index++)
            {
                var colon = lines[index].IndexOf(':');
                if (colon > 0
                    && lines[index][..colon].Trim()
                        .Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    lines[index] = $"Content-Length: {length}";
                }
            }

            return Encoding.ASCII.GetBytes(string.Join("\r\n", lines));
        }
    }

    /// <summary>
    /// A read-ahead over the client stream, so a head can be found without consuming the body behind
    /// it and so nothing already read is lost when the parsing gives up.
    /// </summary>
    private sealed class Reader(Stream stream)
    {
        private byte[] _buffer = new byte[16 * 1024];
        private int _start;
        private int _end;

        private int Available => _end - _start;

        /// <summary>The bytes up to and including the blank line, or null where there is no head.</summary>
        internal async Task<byte[]?> ReadHeadAsync(CancellationToken cancellation)
        {
            var searched = 0;
            while (true)
            {
                var terminator = Find(searched);
                if (terminator >= 0)
                {
                    return Take(terminator + 4);
                }

                // Overlap by three, so a terminator straddling two reads is still found.
                searched = Math.Max(0, Available - 3);

                if (Available >= MaxHeadLength)
                {
                    return null;
                }

                if (!await FillAsync(cancellation).ConfigureAwait(false))
                {
                    return null;
                }
            }
        }

        /// <summary>Exactly this many bytes, or null where the client hung up first.</summary>
        internal async Task<byte[]?> ReadExactlyAsync(int count, CancellationToken cancellation)
        {
            while (Available < count)
            {
                if (!await FillAsync(cancellation).ConfigureAwait(false))
                {
                    return null;
                }
            }

            return Take(count);
        }

        /// <summary>Forward exactly this many bytes without collecting them.</summary>
        internal async Task CopyExactlyAsync(Stream to, long count, CancellationToken cancellation)
        {
            var left = count;
            while (left > 0)
            {
                if (Available == 0 && !await FillAsync(cancellation).ConfigureAwait(false))
                {
                    return;
                }

                var take = (int)Math.Min(Available, left);
                await to.WriteAsync(_buffer.AsMemory(_start, take), cancellation).ConfigureAwait(false);
                _start += take;
                left -= take;
            }

            await to.FlushAsync(cancellation).ConfigureAwait(false);
        }

        /// <summary>
        /// Forward a chunk-framed body, ending after the terminal chunk and its trailer.
        /// </summary>
        /// <remarks>
        /// The framing is forwarded exactly as it arrived — sizes, extensions and trailer included —
        /// because this is reading only to know where the body ends, not to change it. Anything
        /// unparseable falls through to a raw copy of the remainder, which is this class's rule
        /// everywhere.
        /// </remarks>
        internal async Task CopyChunkedAsync(Stream to, CancellationToken cancellation)
        {
            while (true)
            {
                var line = await ReadLineAsync(cancellation).ConfigureAwait(false);
                if (line is null)
                {
                    await CopyRestAsync(to, cancellation).ConfigureAwait(false);
                    return;
                }

                await to.WriteAsync(line, cancellation).ConfigureAwait(false);

                var text = Encoding.ASCII.GetString(line).Trim();
                var semicolon = text.IndexOf(';');
                var size = semicolon < 0 ? text : text[..semicolon];

                if (!int.TryParse(
                        size,
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var length)
                    || length < 0)
                {
                    await CopyRestAsync(to, cancellation).ConfigureAwait(false);
                    return;
                }

                if (length == 0)
                {
                    // The trailer, up to and including the blank line that ends the message.
                    while (true)
                    {
                        var trailer = await ReadLineAsync(cancellation).ConfigureAwait(false);
                        if (trailer is null)
                        {
                            await CopyRestAsync(to, cancellation).ConfigureAwait(false);
                            return;
                        }

                        await to.WriteAsync(trailer, cancellation).ConfigureAwait(false);
                        if (trailer.Length == 2)
                        {
                            await to.FlushAsync(cancellation).ConfigureAwait(false);
                            return;
                        }
                    }
                }

                // The chunk and the CRLF that closes it.
                await CopyExactlyAsync(to, length + 2, cancellation).ConfigureAwait(false);
            }
        }

        /// <summary>Everything left, buffered and unread alike, with no parsing at all.</summary>
        internal async Task CopyRestAsync(Stream to, CancellationToken cancellation)
        {
            try
            {
                if (Available > 0)
                {
                    await to.WriteAsync(_buffer.AsMemory(_start, Available), cancellation)
                        .ConfigureAwait(false);
                    _start = _end;
                    await to.FlushAsync(cancellation).ConfigureAwait(false);
                }

                var buffer = new byte[16 * 1024];
                while (!cancellation.IsCancellationRequested)
                {
                    var read = await stream.ReadAsync(buffer, cancellation).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return;
                    }

                    await to.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);
                    await to.FlushAsync(cancellation).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                // One side going away ends the copy, which is how a finished connection is torn down.
            }
        }

        /// <summary>One CRLF-terminated line, the terminator included.</summary>
        private async Task<byte[]?> ReadLineAsync(CancellationToken cancellation)
        {
            var from = 0;
            while (true)
            {
                for (var index = _start + from; index + 1 < _end; index++)
                {
                    if (_buffer[index] == (byte)'\r' && _buffer[index + 1] == (byte)'\n')
                    {
                        return Take(index - _start + 2);
                    }
                }

                from = Math.Max(0, Available - 1);

                if (Available >= MaxHeadLength
                    || !await FillAsync(cancellation).ConfigureAwait(false))
                {
                    return null;
                }
            }
        }

        private int Find(int from)
        {
            for (var index = _start + from; index + 3 < _end; index++)
            {
                if (_buffer[index] == (byte)'\r' && _buffer[index + 1] == (byte)'\n'
                    && _buffer[index + 2] == (byte)'\r' && _buffer[index + 3] == (byte)'\n')
                {
                    return index - _start;
                }
            }

            return -1;
        }

        private byte[] Take(int count)
        {
            var taken = _buffer.AsSpan(_start, count).ToArray();
            _start += count;
            return taken;
        }

        private async Task<bool> FillAsync(CancellationToken cancellation)
        {
            Compact();

            if (_end == _buffer.Length)
            {
                Array.Resize(ref _buffer, _buffer.Length * 2);
            }

            var read = await stream
                .ReadAsync(_buffer.AsMemory(_end, _buffer.Length - _end), cancellation)
                .ConfigureAwait(false);

            if (read == 0)
            {
                return false;
            }

            _end += read;
            return true;
        }

        private void Compact()
        {
            if (_start == 0)
            {
                return;
            }

            Array.Copy(_buffer, _start, _buffer, 0, Available);
            _end -= _start;
            _start = 0;
        }
    }
}
