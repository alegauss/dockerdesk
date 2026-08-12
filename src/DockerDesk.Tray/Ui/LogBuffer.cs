using System.Collections.ObjectModel;
using System.Text;
using DockerDesk.Core.Api;

namespace DockerDesk.Tray.Ui;

/// <summary>One line of a container's log, as the window shows it.</summary>
/// <param name="Text">The line, without its newline.</param>
/// <param name="IsError">Whether it came off stderr, which is where a failure usually says why.</param>
public sealed record LogLine(string Text, bool IsError);

/// <summary>What a log window says when there is nothing to show.</summary>
/// <param name="Headline">One line.</param>
/// <param name="Detail">One sentence under it.</param>
public sealed record LogEmptyState(string Headline, string Detail)
{
    /// <summary>
    /// What to show, or <see langword="null"/> when there are lines and the log itself is the view.
    /// </summary>
    /// <remarks>
    /// Three different silences. A container that never wrote anything, a log still open and quiet,
    /// and a stream that could not be read are three answers, and a blank box is none of them —
    /// the last one especially, where the user is looking at a failure and being shown nothing.
    /// </remarks>
    /// <param name="empty">Whether nothing has arrived.</param>
    /// <param name="ended">Whether the stream is over.</param>
    /// <param name="failure">What went wrong, in the engine's words, or nothing.</param>
    /// <returns>The empty state, or nothing.</returns>
    public static LogEmptyState? For(bool empty, bool ended, string? failure)
    {
        if (failure is not null)
        {
            return new LogEmptyState("The log could not be read", failure);
        }

        if (!empty)
        {
            return null;
        }

        return ended
            ? new LogEmptyState(
                "Nothing in the log",
                "This container wrote nothing to stdout or stderr before the stream ended.")
            : new LogEmptyState(
                "Waiting for output",
                "The log is open and the container has not written anything yet.");
    }
}

/// <summary>
/// What a log window holds: the last <see cref="MaximumLines"/> lines, and no more.
/// </summary>
/// <remarks>
/// The cap is the whole point. A chatty container emits megabytes a minute, and a window somebody
/// left open overnight against an unbounded collection is the leak that gets a tool called heavy.
/// Dropping is from the front, because the end of a log is the part being read.
///
/// A single line is capped too. A cap counted only in lines is not a memory bound — one process
/// printing a megabyte with no newline in it would sit under any line count.
/// </remarks>
public sealed class LogBuffer
{
    /// <summary>How many lines are kept.</summary>
    public const int MaximumLines = 5000;

    /// <summary>How long one line may be before the rest of it is dropped.</summary>
    public const int MaximumLineLength = 4000;

    private readonly ObservableCollection<LogLine> _lines = [];
    private readonly StringBuilder _partial = new();
    private bool _partialIsError;

    /// <summary>
    /// The lines, oldest first, and the collection the window binds to.
    /// </summary>
    /// <remarks>
    /// Observable rather than rebuilt: a list re-bound wholesale on every chunk throws the scroll
    /// position back to the top, which makes reading a log that is still being written impossible.
    /// It is also the only copy — the clipboard reads this one.
    /// </remarks>
    public ObservableCollection<LogLine> Lines => _lines;

    /// <summary>How many lines have been dropped off the front.</summary>
    public int Dropped { get; private set; }

    /// <summary>Whether anything at all has arrived, including a line still being written.</summary>
    public bool IsEmpty => _lines.Count == 0 && _partial.Length == 0;

    /// <summary>
    /// Take one chunk. Chunks are not lines: a frame can end mid-sentence, so the tail is held
    /// until the newline that finishes it arrives.
    /// </summary>
    /// <param name="chunk">What the reader produced.</param>
    public void Append(LogChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        // A stream switch ends the line in progress. stdout and stderr are two writers into one
        // stream, and holding a half-written stdout line open while stderr arrives would splice
        // two different lines together.
        var isError = chunk.Stream is LogStream.StdErr;
        if (_partial.Length > 0 && isError != _partialIsError)
        {
            Flush();
        }

        _partialIsError = isError;

        foreach (var character in chunk.Text)
        {
            if (character == '\n')
            {
                Flush();
                continue;
            }

            // A lone carriage return is what a progress bar redraws with, and \r\n is what a
            // container built on Windows writes. Neither belongs in a line of text.
            if (character == '\r')
            {
                continue;
            }

            if (_partial.Length < MaximumLineLength)
            {
                _partial.Append(character);
            }
        }
    }

    /// <summary>
    /// End the line in progress, if there is one. What a stream that closed without a final
    /// newline leaves behind is still a line somebody wants to read.
    /// </summary>
    public void Close() => Flush();

    /// <summary>
    /// Everything in the buffer as one block of text, which is what the clipboard takes.
    /// </summary>
    /// <remarks>
    /// The line in progress is included. A copy that silently omitted the last line because no
    /// newline had arrived yet would drop the most recent thing on screen, which is the line
    /// somebody is most likely copying for.
    /// </remarks>
    /// <returns>The lines, newline-separated.</returns>
    public string ToText()
    {
        var text = new StringBuilder();
        foreach (var line in _lines)
        {
            text.Append(line.Text).Append('\n');
        }

        if (_partial.Length > 0)
        {
            text.Append(_partial).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// The line under the log: how much is held, how much was dropped, and whether it is moving.
    /// </summary>
    /// <remarks>
    /// The drop count is stated rather than hidden. A window that silently discards the first
    /// thousand lines of a log somebody is scrolling back through is lying about what it holds, and
    /// the one moment that matters is when the answer is in the part that went.
    /// </remarks>
    /// <param name="following">Whether the view is pinned to the end.</param>
    /// <param name="ended">Whether the stream is over.</param>
    /// <returns>One line.</returns>
    public string Status(bool following, bool ended)
    {
        var held = _lines.Count + (_partial.Length > 0 ? 1 : 0);
        var parts = new List<string> { held == 1 ? "1 line" : $"{held:N0} lines" };
        if (Dropped > 0)
        {
            parts.Add($"{Dropped:N0} dropped, the buffer keeps the last {MaximumLines:N0}");
        }

        parts.Add(ended ? "the stream ended" : following ? "following" : "not following");
        return string.Join(" · ", parts);
    }

    private void Flush()
    {
        _lines.Add(new LogLine(_partial.ToString(), _partialIsError));
        _partial.Clear();

        while (_lines.Count > MaximumLines)
        {
            _lines.RemoveAt(0);
            Dropped++;
        }
    }
}
