namespace FreeWilly.Core.Engine;

/// <summary>
/// Where the journal is read from. The seam the Engine page is drawable through (DD165, L6).
/// </summary>
/// <remarks>
/// Every window in this project has to be renderable without the thing it is about, and for this
/// page the thing it is about is a file on the machine it is running on. A capture taken against
/// the real one is a picture of whatever that laptop's engine did that afternoon, which is neither
/// reviewable nor safe to put in a README — the same reason <c>SampleMachine</c> exists for the
/// lists.
/// </remarks>
public interface IEngineJournal
{
    /// <summary>
    /// Where the file is, spelled the way a user would type it.
    /// </summary>
    /// <remarks>
    /// On the page because the next thing somebody does with this is attach it to a bug report, and
    /// a log they can read but not find is a log they have to be talked through finding.
    /// </remarks>
    string Path { get; }

    /// <summary>Read what is kept, oldest first.</summary>
    /// <returns>The lines, or empty where there is no file yet.</returns>
    IReadOnlyList<string> Read();
}

/// <summary>The real journal, read off disk (DD165).</summary>
public sealed class EngineJournalFile : IEngineJournal
{
    /// <summary>Read the journal this install's own root holds.</summary>
    public EngineJournalFile()
        : this(new EnginePaths().HostLog)
    {
    }

    /// <summary>Read the journal at this path.</summary>
    /// <param name="path">Where the file is.</param>
    public EngineJournalFile(string path) => Path = path;

    /// <inheritdoc/>
    public string Path { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// Opened sharing everything, because the writers are still writing: the host appends whenever
    /// something happens and the tray appends beside it, and a reader that took the file exclusively
    /// would make the page being open the reason a line went missing (DD163).
    ///
    /// <para>The whole file rather than a tail from an offset, and the cap is what makes that
    /// honest: <see cref="EngineHostLog.KeptBytes"/> holds this to 64 KB, so reading all of it is a
    /// few thousand short lines. An offset would also be wrong here — the file is trimmed from the
    /// front, so a remembered position walks backwards through content that has moved.</para>
    ///
    /// <para>A missing file is empty and not an error. The engine has never been run on this
    /// machine, or it has been run and nothing has happened, which is the state DD137 deliberately
    /// leaves no file for.</para>
    /// </remarks>
    public IReadOnlyList<string> Read()
    {
        try
        {
            using var file = new FileStream(
                Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(file);

            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            return lines;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Same judgement the writer makes, for the same reason: this page failing to read a log
            // must not be worse than the log not being there. What the reader sees is the empty
            // state, which names the path — and a path is what somebody needs to find out why.
            return [];
        }
    }
}

/// <summary>
/// What the journal adds up to, above the log itself (DD165).
/// </summary>
/// <param name="Lines">How many lines are held.</param>
/// <param name="Restarts">How many times the host is recorded as having brought the engine back.</param>
/// <param name="Since">The stamp on the oldest line kept, or <see langword="null"/> where there is none.</param>
/// <remarks>
/// Derived from the file rather than asked of the host, and that is a deliberate limit rather than a
/// shortcut. The host is another process with no channel back to this one, and the alternative —
/// inventing one so a page can show a counter — is a great deal of machinery for a number the file
/// already contains.
/// </remarks>
public sealed record JournalDigest(int Lines, int Restarts, string? Since)
{
    /// <summary>Nothing has been written.</summary>
    public static readonly JournalDigest Nothing = new(0, 0, null);

    /// <summary>How many characters of a line the stamp occupies.</summary>
    /// <remarks>
    /// <c>yyyy-MM-dd HH:mm:ss</c>, as <see cref="EngineHostLog.Say"/> writes it. Read by width
    /// rather than parsed: this is shown back to a reader who is comparing it against Event Viewer,
    /// so the useful thing to do with it is quote it, and parsing would only introduce a way for a
    /// line to be rejected for being unreadable.
    /// </remarks>
    public const int StampWidth = 19;

    /// <summary>Read one off the lines a journal holds.</summary>
    /// <param name="lines">The lines, oldest first.</param>
    /// <returns>The digest.</returns>
    public static JournalDigest Of(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            return Nothing;
        }

        var restarts = lines.Count(line =>
            line.Contains(EngineRevival.RestartMark, StringComparison.Ordinal));

        var oldest = lines[0];
        var since = oldest.Length >= StampWidth ? oldest[..StampWidth] : null;

        return new JournalDigest(lines.Count, restarts, since);
    }

    /// <summary>
    /// The one line above the log, as the page shows it.
    /// </summary>
    /// <returns>The sentence.</returns>
    /// <remarks>
    /// The restart count is the number this page exists to put in front of somebody. A machine whose
    /// engine was brought back four times overnight and one that never lost it are different
    /// machines, and until the log had a reader they looked identical the morning after — which is
    /// the sentence DD137 wrote about the console and which stayed true of a file nobody opened.
    /// </remarks>
    public string Summary()
    {
        if (Lines == 0)
        {
            return "nothing recorded";
        }

        var restarts = Restarts switch
        {
            0 => "no restarts",
            1 => "1 restart",
            _ => $"{Restarts:N0} restarts",
        };

        var held = Lines == 1 ? "1 line" : $"{Lines:N0} lines";
        return Since is null ? $"{restarts} · {held}" : $"{restarts} since {Since} · {held}";
    }
}
