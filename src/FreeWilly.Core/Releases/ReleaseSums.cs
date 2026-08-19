namespace FreeWilly.Core.Releases;

/// <summary>
/// The digest a release published for one of its files (DD15, DD154).
/// </summary>
/// <remarks>
/// <c>release.yml</c> writes <c>SHA256SUMS.txt</c> in the <c>&lt;hash&gt;  &lt;name&gt;</c> shape
/// <c>sha256sum -c</c> reads, so that a download whose integrity cannot be checked is not what this
/// project asks anybody to trust. This is the other end of that: the same file, read by the thing
/// that downloads the installer, so the pinned-digest rule every other artefact obeys covers the one
/// artefact that is this tool itself.
///
/// <para><b>A missing line is not a zero-length answer.</b> Every failure here answers
/// <see langword="null"/>, and the caller treats that as "do not run it" rather than as "no digest to
/// check" — which is the only reading that keeps the rule from being optional.</para>
/// </remarks>
public static class ReleaseSums
{
    /// <summary>The digest one file has, according to a sums file.</summary>
    /// <param name="text">The contents of <c>SHA256SUMS.txt</c>.</param>
    /// <param name="fileName">The name to look for, as it appears in the file.</param>
    /// <returns>
    /// The digest in lower-case hex, or <see langword="null"/> where the file does not name it, names
    /// it more than once with different answers, or names it with something that is not a SHA-256.
    /// </returns>
    public static string? DigestFor(string text, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string? found = null;
        foreach (var line in text.Split('\n'))
        {
            // The two-space separator is what sha256sum writes, but a hand-edited file may have one
            // or a tab, and refusing those would refuse a correct digest over whitespace. Splitting
            // on any run of it and requiring exactly two fields keeps that latitude without letting
            // a line with a comment on the end through.
            var fields = line.Split(
                (char[])[' ', '\t', '\r', '*'], StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);
            if (fields.Length is not 2
                || !string.Equals(fields[1], fileName, StringComparison.OrdinalIgnoreCase)
                || !IsSha256(fields[0]))
            {
                continue;
            }

            var digest = fields[0].ToLowerInvariant();

            // Two lines naming the same file is a sums file nobody can act on. Identical ones are
            // harmless duplication; disagreeing ones are the case this refuses.
            if (found is not null && !string.Equals(found, digest, StringComparison.Ordinal))
            {
                return null;
            }

            found = digest;
        }

        return found;
    }

    private static bool IsSha256(string candidate) =>
        candidate.Length == 64 && candidate.All(char.IsAsciiHexDigit);
}
