using System.Globalization;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// Writing the figures the measurement produced back into <c>agent-budget.json</c> (DD147).
/// </summary>
/// <remarks>
/// DD144 is why this exists. DD101 changed the shaped mounts row, the task got two tokens cheaper,
/// the file was told one, and every run of the suite failed for the life of that commit and eleven
/// after it — on a clean checkout, with nothing else wrong. Nothing had drifted; a figure had been
/// typed.
///
/// <para>DD78 cannot catch that and never could. Making the assertion exact binds the recorded
/// number tightly to the measurement, which is the whole value of it, but only once somebody has
/// written down a number the measurement produced. An exact gate over a typo is red forever, and a
/// red gate is one nobody reads — which is where the old 15% band left things by the opposite
/// route.</para>
///
/// <para><b>Textual, and that is not laziness.</b> The file is two thirds prose: `about` blocks
/// arguing what each figure means, a `caveat` carrying the history of every number in it. A
/// System.Text.Json round trip would reformat all of it and drop nothing but the indentation the
/// argument is laid out in, which is a diff nobody can review — and reviewing the diff is the whole
/// point of writing it.</para>
///
/// <para><b>Never automatic.</b> The write happens when it is asked for and not when the numbers
/// disagree. A run that silently rewrote the file whenever it did not match is not a gate at
/// all.</para>
/// </remarks>
internal static class BudgetFile
{
    /// <summary>The variable that asks for the write.</summary>
    internal const string RecordVariable = "FREEWILLY_RECORD_BUDGET";

    /// <summary>Whether this run was asked to rewrite the file.</summary>
    internal static bool Recording =>
        Environment.GetEnvironmentVariable(RecordVariable) is "1";

    /// <summary>The budget, found by walking up from the test binary.</summary>
    internal static string Path()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null)
        {
            var candidate = System.IO.Path.Combine(here.FullName, "agent-budget.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            here = here.Parent;
        }

        throw new InvalidOperationException(
            "agent-budget.json was not found above " + AppContext.BaseDirectory);
    }

    /// <summary>One figure the file records.</summary>
    /// <param name="Section">The top-level object, <c>baseline</c> or <c>surface</c>.</param>
    /// <param name="Block">The object inside it, <c>measured</c> or <c>ratio</c>.</param>
    /// <param name="Key">The figure's name.</param>
    /// <param name="Value">What the measurement produced, formatted as the file writes it.</param>
    internal sealed record Figure(string Section, string Block, string Key, string Value);

    /// <summary>Rewrite these figures in place, and say which ones moved.</summary>
    /// <param name="figures">Every figure the gate binds.</param>
    /// <returns>One line per figure that changed; empty where the file already agreed.</returns>
    internal static IReadOnlyList<string> Record(IEnumerable<Figure> figures)
    {
        ArgumentNullException.ThrowIfNull(figures);

        var path = Path();
        var text = File.ReadAllText(path);
        var moved = new List<string>();

        foreach (var figure in figures)
        {
            var at = Locate(text, figure);
            var was = Read(text, at);
            if (was == figure.Value)
            {
                continue;
            }

            text = text.Remove(at.Start, at.Length).Insert(at.Start, figure.Value);
            moved.Add(
                $"{figure.Section}.{figure.Block}.{figure.Key}: {was} -> {figure.Value}");
        }

        if (moved.Count > 0)
        {
            File.WriteAllText(path, text);
        }

        return moved;
    }

    /// <summary>Where a figure's value sits in the text.</summary>
    private static (int Start, int Length) Locate(string text, Figure figure)
    {
        // Narrowed one nesting level at a time rather than matched in one pattern. `calls` and
        // `tokens` each appear in both sections and in both blocks, so a search that did not walk
        // down would write the surface's figure over the baseline's and pass.
        var section = Index(text, $"\"{figure.Section}\"", 0, figure);
        var block = Index(text, $"\"{figure.Block}\"", section, figure);
        var key = Index(text, $"\"{figure.Key}\"", block, figure);

        var colon = text.IndexOf(':', key);
        if (colon < 0)
        {
            throw new InvalidOperationException($"{Describe(figure)} has no value after it");
        }

        var start = colon + 1;
        while (start < text.Length && text[start] == ' ')
        {
            start++;
        }

        var end = start;
        while (end < text.Length && (char.IsAsciiDigit(text[end]) || text[end] == '.'))
        {
            end++;
        }

        if (end == start)
        {
            throw new InvalidOperationException($"{Describe(figure)} is not a number");
        }

        return (start, end - start);
    }

    private static string Read(string text, (int Start, int Length) at) =>
        text.Substring(at.Start, at.Length);

    private static int Index(string text, string needle, int from, Figure figure)
    {
        var at = text.IndexOf(needle, from, StringComparison.Ordinal);
        if (at < 0)
        {
            throw new InvalidOperationException(
                $"{Describe(figure)}: agent-budget.json no longer carries {needle}");
        }

        return at;
    }

    private static string Describe(Figure figure) =>
        $"{figure.Section}.{figure.Block}.{figure.Key}";

    /// <summary>An integer, as the file writes one.</summary>
    internal static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A ratio, rounded the way the file records one.</summary>
    internal static string Ratio(double value) =>
        Math.Round(value, 1).ToString("0.0#", CultureInfo.InvariantCulture);
}
