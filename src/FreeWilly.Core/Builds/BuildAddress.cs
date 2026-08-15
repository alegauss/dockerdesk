namespace FreeWilly.Core.Builds;

/// <summary>
/// The build a <c>docker-desktop://</c> link names (DD126).
/// </summary>
/// <remarks>
/// <b>Buildx prints a link this machine cannot open.</b> Every build ends with
/// <c>View build details: docker-desktop://dashboard/build/default/default/&lt;ref&gt;</c>. The line is
/// hardcoded in the binary this project pins and ships, and nothing configures it away — measured
/// against <c>DOCKER_CLI_HINTS</c>, <c>BUILDX_EXPERIMENTAL</c> and
/// <c>BUILDX_NO_DEFAULT_ATTESTATIONS</c>, and there is no flag on <c>buildx build</c> for it. So the
/// link arrives on a machine where that scheme is registered to nothing.
///
/// <para><b>Only the address is dead; the record is real.</b> The tail of the URL is exactly the ref
/// <c>buildx history</c> uses — <c>&lt;builder&gt;/&lt;node&gt;/&lt;id&gt;</c>, which is why this takes
/// everything after <c>/build/</c> rather than the last segment alone. So resolving the link is a
/// lookup and not an invention.</para>
///
/// <para><b>A bare ref is accepted too.</b> The verb this feeds is reachable by hand, and a user who
/// copied an id out of <c>history ls</c> should not have to wrap it in a URL to look at it.</para>
/// </remarks>
public static class BuildAddress
{
    /// <summary>The scheme buildx prints, and the one this install registers.</summary>
    public const string Scheme = "docker-desktop";

    /// <summary>What comes before the ref in a build link.</summary>
    private const string Marker = "/build/";

    /// <summary>
    /// The ref a link or a bare id names, or <see langword="null"/> where it names none.
    /// </summary>
    /// <param name="argument">A <c>docker-desktop://</c> URL, or a ref on its own.</param>
    /// <returns>Something like <c>default/default/i93abaotri2m3vdda5unxeimu</c>.</returns>
    /// <remarks>
    /// Null rather than an exception, and null for anything at all doubtful. This is reached from a
    /// registered protocol handler, which means the argument is whatever any process on the machine
    /// put in a link — so what it is asked is "does this name a build", and everything else is no.
    /// </remarks>
    public static string? RefIn(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return null;
        }

        var text = argument.Trim().Trim('"');

        if (!text.StartsWith(Scheme + "://", StringComparison.OrdinalIgnoreCase))
        {
            // A bare ref, which is what a hand-typed call carries. It has to look like one: a path
            // with no scheme, no backslash and no traversal, so a stray file name is not read as a
            // build that would then be looked up.
            return Plausible(text) ? text : null;
        }

        var marker = text.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            return null;
        }

        var tail = text[(marker + Marker.Length)..];

        // A query or a fragment is not part of the ref. Neither is written today, and a link that
        // grows one must not turn into a lookup for a ref that does not exist.
        var cut = tail.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            tail = tail[..cut];
        }

        tail = tail.Trim('/');

        return Plausible(tail) ? tail : null;
    }

    /// <summary>Whether a string is shaped like a ref this would pass to the CLI.</summary>
    /// <remarks>
    /// The ref reaches a subprocess argument, so what it may contain is stated here rather than
    /// assumed: the characters buildx actually uses, and nothing that would read as a second
    /// argument, a path or a traversal.
    /// </remarks>
    /// <param name="value">The candidate.</param>
    /// <returns><see langword="true"/> where it could be a ref.</returns>
    private static bool Plausible(string value) =>
        value.Length is > 0 and <= 200
        && !value.Contains("..", StringComparison.Ordinal)
        && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '/' or '-' or '_' or '.');
}
