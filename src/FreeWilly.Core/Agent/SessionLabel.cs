using System.Security.Cryptography;
using System.Text;

namespace FreeWilly.Core.Agent;

/// <summary>
/// The label that makes cleanup an undo rather than a whole-machine sweep.
/// </summary>
/// <remarks>
/// DD29. An agent that starts three containers and a volume to reproduce a defect has no way to take
/// them back: <c>prune</c> is scoped to the machine, cannot tell what this session made from what the
/// user made last week, and is therefore the one command nobody delegates. So the leftovers stay, and
/// the next session inherits a machine with a history it did not write.
///
/// <para>Docker already carries the mechanism — every object takes labels — so everything created
/// through <c>do</c> is stamped, and a reclaim scoped to that label removes exactly that set.</para>
///
/// <para><b>Where the id comes from.</b> Every <c>dockerdesk</c> call is a separate process, so an id
/// generated per invocation would put every object in a session of its own and make the reclaim
/// useless. <c>FREEWILLY_SESSION</c> names it instead: the boundary is drawn by whoever knows where it
/// is, which is the caller. With it unset there is still a label — derived from the working directory,
/// so nothing is ever created unlabelled — and the fallback says so in its own name, because a session
/// that is really "this folder, forever" should not be mistaken for a piece of work.</para>
/// </remarks>
public static class SessionLabel
{
    /// <summary>The label key every created object carries.</summary>
    public const string Key = "freewilly.session";

    /// <summary>What was stamped before the rename, and is still read (DD72).</summary>
    /// <remarks>
    /// A label is state on somebody's machine rather than spelling in a build, so a build that merely
    /// respelled this would compile, pass every test, and then answer <c>read changes --session</c>
    /// with nothing on a machine full of the caller's own containers. <c>do reclaim --session</c> is
    /// the worse half: finding nothing to remove reads as "there was nothing there" rather than as an
    /// error, and the leftovers stay.
    ///
    /// <para>The Engine API cannot relabel an existing object — labels are set at creation, and the
    /// only way to change one is to recreate the container, which is exactly what this surface refuses
    /// to do to somebody's work. So the migration is read-both, write-new, the way DD55 resolves an
    /// adopted install rather than rewriting it: a machine carries both keys for as long as the older
    /// objects live, and nothing is lost in the middle.</para>
    /// </remarks>
    public const string LegacyKey = "dockerdesk.session";

    /// <summary>The variable that names the session.</summary>
    public const string Variable = "FREEWILLY_SESSION";

    /// <summary>What a derived, directory-scoped id is prefixed with.</summary>
    /// <remarks>
    /// Visible on purpose. A caller reading <c>dir:8f21a0</c> in a reclaim plan can see that the scope
    /// is a folder rather than a task, which is the difference between an undo and a sweep.
    /// </remarks>
    public const string DerivedPrefix = "dir:";

    /// <summary>The session this process belongs to.</summary>
    /// <param name="named">What <see cref="Variable"/> says, or null.</param>
    /// <param name="workingDirectory">Where the call was made, for the fallback.</param>
    /// <returns>The id.</returns>
    public static string Resolve(string? named, string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        if (!string.IsNullOrWhiteSpace(named))
        {
            return Sanitise(named.Trim());
        }

        // Docker's own label values are free-form, so the derivation only has to be stable and short.
        var digest = SHA256.HashData(
            Encoding.UTF8.GetBytes(workingDirectory.TrimEnd('\\', '/').ToLowerInvariant()));
        return DerivedPrefix + Convert.ToHexStringLower(digest)[..6];
    }

    /// <summary>The session this process belongs to, read off the environment.</summary>
    /// <returns>The id.</returns>
    public static string Resolve() =>
        Resolve(Environment.GetEnvironmentVariable(Variable), Directory.GetCurrentDirectory());

    /// <summary>The labels to stamp on anything created.</summary>
    /// <param name="session">The session id.</param>
    /// <returns>The labels.</returns>
    public static IReadOnlyDictionary<string, string> For(string session) =>
        new Dictionary<string, string>(StringComparer.Ordinal) { [Key] = session };

    /// <summary>
    /// The session an object was stamped with, under either key, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The one place both spellings are read, so every caller that asks "whose is this?" sees both
    /// generations without each of them remembering to. <see cref="Key"/> wins where an object somehow
    /// carries both: the current key is the one this build writes, so it is the one that is true.
    /// </remarks>
    /// <param name="labels">The object's labels.</param>
    /// <returns>The session id it carries, or nothing.</returns>
    public static string? StampedOn(IReadOnlyDictionary<string, string>? labels)
    {
        if (labels is null)
        {
            return null;
        }

        return labels.TryGetValue(Key, out var current) ? current
            : labels.TryGetValue(LegacyKey, out var before) ? before
            : null;
    }

    /// <summary>Whether a set of labels says this object belongs to a session.</summary>
    /// <param name="labels">The object's labels.</param>
    /// <param name="session">The session id.</param>
    /// <returns><see langword="true"/> where it does.</returns>
    public static bool Owns(IReadOnlyDictionary<string, string>? labels, string session) =>
        StampedOn(labels) is { } stamped
        && string.Equals(stamped, session, StringComparison.Ordinal);

    /// <summary>Whether an id names a derived scope rather than a piece of work.</summary>
    /// <param name="session">The id.</param>
    /// <returns><see langword="true"/> where it was derived from a directory.</returns>
    public static bool IsDerived(string? session) =>
        session is not null && session.StartsWith(DerivedPrefix, StringComparison.Ordinal);

    /// <summary>
    /// A label value Docker will accept, and a caller can read back.
    /// </summary>
    /// <remarks>
    /// Whitespace and the characters a shell would eat are replaced rather than refused: an id is the
    /// caller's own word for their work, and refusing "repro #17" over a space would be this tool
    /// arguing about naming rather than doing its job.
    /// </remarks>
    private static string Sanitise(string named)
    {
        var clean = new StringBuilder(named.Length);
        foreach (var character in named)
        {
            clean.Append(char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':'
                ? character
                : '-');
        }

        return clean.Length > 64 ? clean.ToString(0, 64) : clean.ToString();
    }
}
