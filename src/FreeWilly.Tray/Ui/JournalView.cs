using System.Collections.ObjectModel;
using System.Text;
using FreeWilly.Core.Engine;

namespace FreeWilly.Tray.Ui;

/// <summary>One line of the engine's journal, split where the window draws it (DD165).</summary>
/// <param name="Stamp">The clock, which is the column the eye runs down.</param>
/// <param name="Said">What happened, which is what is read either side of a gap.</param>
public sealed record JournalLine(string Stamp, string Said)
{
    /// <summary>Split one line as <see cref="EngineHostLog.Say"/> wrote it.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The two halves.</returns>
    /// <remarks>
    /// By width and not by parsing, for the reason <see cref="JournalDigest.StampWidth"/> gives: the
    /// stamp is shown back to somebody comparing it against Event Viewer, so quoting it is the whole
    /// job. A line too short to carry one is not rejected — it goes through whole, in the sentence
    /// column, because a file with something unexpected in it is exactly when this page is open.
    /// </remarks>
    public static JournalLine Of(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.Length > JournalDigest.StampWidth
            ? new JournalLine(
                line[..JournalDigest.StampWidth],
                line[JournalDigest.StampWidth..].TrimStart())
            : new JournalLine(string.Empty, line);
    }
}

/// <summary>
/// The journal as the page holds it: an observable list kept in step with a file that is appended
/// to and trimmed from the front (DD165).
/// </summary>
/// <remarks>
/// <b>Appending rather than rebinding is the whole of this class.</b> A list re-bound wholesale on
/// every read throws the scroll position back to the top, which makes reading a log that is still
/// being written impossible — the failure <see cref="LogBuffer.Lines"/> documents, arriving here
/// through a different door. So a read whose lines begin with the ones already held adds only the
/// tail, and nothing else moves.
///
/// <para><b>And it has to survive the file being cut.</b> <see cref="EngineHostLog"/> keeps the
/// newest 64 KB and drops what is older, so the oldest line on screen can simply stop existing
/// between two reads. That is not an append and cannot be treated as one — the prefix check below is
/// what tells the two apart, and a mismatch rebuilds.</para>
/// </remarks>
public sealed class JournalView
{
    private readonly ObservableCollection<JournalLine> _lines = [];
    private List<string> _raw = [];

    /// <summary>The lines, oldest first, and the collection the page binds to.</summary>
    public ObservableCollection<JournalLine> Lines => _lines;

    /// <summary>Whether there is nothing to show.</summary>
    public bool IsEmpty => _lines.Count == 0;

    /// <summary>How many times the view has been rebuilt rather than appended to.</summary>
    /// <remarks>
    /// Counted so a test can assert on it. "The right lines are on screen" passes for a rebuild as
    /// readily as for an append, and the difference between them is the only thing this class is
    /// for — a rebuild is a reader losing their place.
    /// </remarks>
    public int Rebuilds { get; private set; }

    /// <summary>Take a fresh read of the file.</summary>
    /// <param name="now">Every line the journal holds, oldest first.</param>
    /// <returns><see langword="true"/> where anything changed.</returns>
    public bool Update(IReadOnlyList<string> now)
    {
        ArgumentNullException.ThrowIfNull(now);

        if (Unchanged(now))
        {
            return false;
        }

        if (Continues(now))
        {
            for (var i = _raw.Count; i < now.Count; i++)
            {
                _lines.Add(JournalLine.Of(now[i]));
            }
        }
        else
        {
            // The file was trimmed, replaced, or is being read for the first time. Nothing here can
            // be salvaged into an append: the lines held describe content that has moved.
            Rebuilds++;
            _lines.Clear();
            foreach (var line in now)
            {
                _lines.Add(JournalLine.Of(line));
            }
        }

        _raw = [.. now];
        return true;
    }

    /// <summary>Everything on screen as one block of text, which is what the clipboard takes.</summary>
    /// <returns>The lines, newline-separated.</returns>
    /// <remarks>
    /// The raw lines and not the split ones. What somebody pastes into a bug report should be what
    /// the file says, byte for byte — a copy that re-joined the two columns with the window's own
    /// spacing would be this page's rendering of the log rather than the log.
    /// </remarks>
    public string ToText()
    {
        var text = new StringBuilder();
        foreach (var line in _raw)
        {
            text.Append(line).Append('\n');
        }

        return text.ToString();
    }

    private bool Unchanged(IReadOnlyList<string> now)
    {
        if (now.Count != _raw.Count)
        {
            return false;
        }

        for (var i = 0; i < now.Count; i++)
        {
            if (!string.Equals(now[i], _raw[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether this read is the held one with more on the end.</summary>
    /// <param name="now">The fresh read.</param>
    /// <returns><see langword="true"/> where every held line is still there, in place.</returns>
    /// <remarks>
    /// Every line compared and not just the first, which costs a pass over a few thousand short
    /// strings once a second and buys the one thing that matters: a file trimmed by exactly the
    /// lines that were added since the last read has the same length and a different middle, and a
    /// cheaper check would append a duplicate tail onto content that had already scrolled away.
    /// </remarks>
    private bool Continues(IReadOnlyList<string> now)
    {
        if (now.Count < _raw.Count)
        {
            return false;
        }

        for (var i = 0; i < _raw.Count; i++)
        {
            if (!string.Equals(now[i], _raw[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
