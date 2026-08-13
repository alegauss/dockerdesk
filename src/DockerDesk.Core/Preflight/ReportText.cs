using System.Text;

namespace DockerDesk.Core.Preflight;

/// <summary>Renders a <see cref="PreflightReport"/> as the report a person reads.</summary>
/// <remarks>
/// A pure function of the report, so the rendering of a machine that fails is testable on a
/// machine that does not.
/// </remarks>
public static class ReportText
{
    /// <summary>What is printed in the verdict column, widest first for alignment.</summary>
    private static string Tag(Verdict verdict) => verdict switch
    {
        Verdict.Pass => "ok",
        Verdict.Fail => "FAIL",
        Verdict.Warn => "warn",
        Verdict.Unknown => "?",
        _ => "?",
    };

    /// <summary>Render the whole report, ending in a newline.</summary>
    /// <param name="report">The report.</param>
    /// <param name="heading">
    /// The first line. Defaults to the machine preflight's, because that is what this renderer was
    /// written for; a report about something else has to say so or the text is about the wrong subject.
    /// </param>
    /// <param name="summary">
    /// The closing line. Defaults to <see cref="Summary(PreflightReport)"/>, which talks about an
    /// install — the wrong sentence for a report that was never about one.
    /// </param>
    /// <returns>The text to print.</returns>
    /// <remarks>
    /// The two parameters are DD26. Pointing this renderer at a container reused the vocabulary as
    /// intended and inherited the machine's framing with it: a container diagnosis came back headed
    /// "what this machine can host" and closed with "Nothing has been copied to disk", which was read
    /// off a real capture and is a report describing the wrong thing.
    /// </remarks>
    public static string Render(
        PreflightReport report, string? heading = null, string? summary = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var text = new StringBuilder();
        text.AppendLine(heading ?? "DockerDesk preflight — what this machine can host");
        text.AppendLine();

        // ASCII tags in a fixed-width column: the report is the first thing a user sees, and it is
        // seen through whatever code page their console happens to be in.
        var titleWidth = report.Checks.Count == 0
            ? 0
            : report.Checks.Max(check => check.Title.Length);

        foreach (var check in report.Checks)
        {
            text.Append("  [").Append(Tag(check.Verdict).PadRight(4)).Append("]  ")
                .Append(check.Title.PadRight(titleWidth)).Append("  ")
                .AppendLine(check.Detail);

            if (check.Remedy is { } remedy && check.Verdict is not Verdict.Pass)
            {
                // The arrow marks the remedy once. Repeating it on every wrapped line reads as
                // several actions where there is one, which is the opposite of the point.
                var first = true;
                foreach (var line in Wrap(remedy, 74))
                {
                    text.Append(' ', 11).Append(first ? "-> " : "   ").AppendLine(line);
                    first = false;
                }
            }
        }

        text.AppendLine();
        text.AppendLine(summary ?? Summary(report));
        return text.ToString();
    }

    /// <summary>The one sentence a caller acts on.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The summary line.</returns>
    public static string Summary(PreflightReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.CanHostEngine)
        {
            return "This machine can host a container engine.";
        }

        var count = report.Blockers.Count;
        var rows = count == 1 ? "1 row blocks" : $"{count} rows block";
        return $"{rows} an install. Nothing has been copied to disk.";
    }

    /// <summary>Break <paramref name="text"/> on spaces at <paramref name="width"/>.</summary>
    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }
}
