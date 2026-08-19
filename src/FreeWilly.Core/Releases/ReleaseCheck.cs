using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using FreeWilly.Core.Licensing;

namespace FreeWilly.Core.Releases;

/// <summary>Where the latest release's description comes from. The network, behind a seam.</summary>
/// <remarks>
/// The same shape as <c>IArtefactFetcher</c> and for the same reason: nothing in this assembly should
/// need a host to be reachable in order to be tested, and the parsing is where every defect this can
/// have actually lives.
/// </remarks>
public interface IReleaseFeed
{
    /// <summary>Read the latest release, as the JSON the host answers with.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The body, or throws.</returns>
    Task<string> LatestAsync(CancellationToken cancellation);
}

/// <summary>A release newer than this build, with the two files an apply needs.</summary>
/// <param name="Version">Its version, parsed from the tag.</param>
/// <param name="Tag">The tag as published, for anything that quotes it.</param>
/// <param name="InstallerName">The installer's file name, which is also its line in the sums file.</param>
/// <param name="InstallerUrl">Where the installer is.</param>
/// <param name="SumsUrl">Where the digest for it is.</param>
public sealed record AvailableRelease(
    Version Version,
    string Tag,
    string InstallerName,
    string InstallerUrl,
    string SumsUrl);

/// <summary>
/// Whether a newer release exists (DD154).
/// </summary>
/// <remarks>
/// An installed copy had no way to learn that a newer one had been published: nothing on the machine
/// looked at the release it came from, so a fix reached only whoever went to check. This reads one
/// release's description and compares its tag to the running build, and that is the whole of it —
/// downloading and applying is <see cref="ReleaseUpdate"/>'s.
///
/// <para><b>Silent on every failure.</b> Offline, behind a proxy that blocks the host, rate-limited,
/// or pointed at a repository with no releases yet: each of those answers "nothing newer", because
/// none of them is something the user asked to be told about. A check they did not initiate must not
/// be able to produce a complaint.</para>
///
/// <para><b>The asset name carries the version, so the lookup is a pattern.</b> claude-tray's
/// installer is always <c>ClaudeTray-Setup.exe</c> and can be matched by name; DD152 ships
/// <c>FreeWilly-Setup-&lt;x.y.z&gt;.exe</c>, and a release with two files answering that pattern is
/// refused rather than resolved by taking the first. Deliberately not built from the tag either: a
/// tag and a file name that disagree is exactly the mistake release.yml already refuses to publish,
/// and matching on the tag would hide it here instead of reporting nothing to install.</para>
/// </remarks>
public sealed partial class ReleaseCheck
{
    /// <summary>The one release this asks about.</summary>
    public const string LatestReleaseApi =
        "https://api.github.com/repos/alegauss/freewilly/releases/latest";

    /// <summary>The host that reaches, named on its own so a page can restate it.</summary>
    public const string Host = "api.github.com";

    /// <summary>The file the installer's digest is published in, beside it (DD15).</summary>
    public const string SumsAssetName = "SHA256SUMS.txt";

    private readonly IReleaseFeed _feed;
    private readonly Version _running;

    /// <summary>Construct a check.</summary>
    /// <param name="feed">Where the release description comes from.</param>
    /// <param name="running">
    /// The version to compare against. Defaults to this build's, which is what the tray passes; a
    /// test passes one of its own, because a suite that had to be re-run at every release would be
    /// asserting the version rather than the comparison.
    /// </param>
    public ReleaseCheck(IReleaseFeed feed, Version? running = null)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _feed = feed;
        _running = running ?? ThisBuild;
    }

    /// <summary>What this build calls itself, as a version rather than as a string.</summary>
    /// <remarks>
    /// Read through <see cref="BuildVersion"/> so there is still one source for it, and normalised
    /// the way <see cref="TryParseVersion"/> normalises a tag — otherwise a three-part tag would
    /// compare unequal to a four-part assembly version that means the same thing.
    /// </remarks>
    public static Version ThisBuild =>
        TryParseVersion(BuildVersion.Current, out var version) ? version : new Version(0, 0, 0);

    /// <summary>Ask whether there is something newer.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The release, or <see langword="null"/> — including for every failure.</returns>
    public async Task<AvailableRelease?> NewerAsync(CancellationToken cancellation = default)
    {
        try
        {
            var body = await _feed.LatestAsync(cancellation).ConfigureAwait(false);
            return NewerThan(body, _running);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // See the class remarks: a check nobody asked for cannot be allowed to complain, and
            // there is no remedy a user could apply to any of these.
            return null;
        }
    }

    /// <summary>Read a release description and say whether it beats a version.</summary>
    /// <param name="json">What the host answered.</param>
    /// <param name="running">The version to beat.</param>
    /// <returns>The release, or <see langword="null"/> where there is nothing to offer.</returns>
    public static AvailableRelease? NewerThan(string json, Version running)
    {
        ArgumentNullException.ThrowIfNull(running);
        var release = In(json);
        return release is not null && release.Version > running ? release : null;
    }

    /// <summary>Read a release description, whatever version it turns out to name.</summary>
    /// <param name="json">What the host answered.</param>
    /// <returns>
    /// The release, or <see langword="null"/> where the body is not one, names no version, offers no
    /// installer, offers more than one, or publishes no digest for it.
    /// </returns>
    public static AvailableRelease? In(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind is not JsonValueKind.Object)
            {
                return null;
            }

            var tag = root.TryGetProperty("tag_name", out var named) ? named.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag) || !TryParseVersion(tag, out var version))
            {
                return null;
            }

            if (!root.TryGetProperty("assets", out var assets)
                || assets.ValueKind is not JsonValueKind.Array)
            {
                return null;
            }

            string? installerName = null;
            string? installerUrl = null;
            string? sumsUrl = null;

            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.ValueKind is not JsonValueKind.Object
                    || !asset.TryGetProperty("name", out var nameOf)
                    || nameOf.GetString() is not { } name
                    || !asset.TryGetProperty("browser_download_url", out var urlOf)
                    || urlOf.GetString() is not { } url)
                {
                    continue;
                }

                if (string.Equals(name, SumsAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    sumsUrl = url;
                    continue;
                }

                if (!Installer().IsMatch(name))
                {
                    continue;
                }

                // Two files answering the pattern is a release nobody can resolve from here, and
                // guessing which one to run is the guess this check exists to avoid making.
                if (installerName is not null)
                {
                    return null;
                }

                installerName = name;
                installerUrl = url;
            }

            // No digest is the same answer as no installer. Verifying what is downloaded is not
            // optional here — every other artefact this tool fetches is checked against a pinned
            // digest, and an unverified .exe would be the one download it trusted blindly.
            return installerName is null || installerUrl is null || sumsUrl is null
                ? null
                : new AvailableRelease(version, tag, installerName, installerUrl, sumsUrl);
        }
    }

    /// <summary>Read a tag as a three-part version.</summary>
    /// <param name="tag">The tag, with or without its <c>v</c>.</param>
    /// <param name="version">What it means.</param>
    /// <returns><see langword="true"/> where it named a version.</returns>
    /// <remarks>
    /// Any leading non-digit run is skipped — the <c>v</c>, a stray dot, whitespace — so a
    /// tag-naming slip cannot silently stop update detection. Normalised to three parts because that
    /// is what <c>Directory.Build.props</c> states and what the installer's file name carries; a
    /// fourth part the assembly happens to have would otherwise make an equal version compare
    /// greater.
    /// </remarks>
    public static bool TryParseVersion(string tag, [NotNullWhen(true)] out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var text = tag.Trim();
        var start = 0;
        while (start < text.Length && !char.IsAsciiDigit(text[start]))
        {
            start++;
        }

        if (start == text.Length)
        {
            return false;
        }

        var parts = text[start..].Split('.');
        if (!int.TryParse(parts[0], out var major) || major < 0)
        {
            return false;
        }

        int Part(int index) =>
            index < parts.Length && int.TryParse(parts[index], out var value) && value >= 0
                ? value
                : 0;

        version = new Version(major, Part(1), Part(2));
        return true;
    }

    /// <summary>What DD152 names the installer, with the version it carries.</summary>
    /// <remarks>
    /// Anchored, and the version group is digits and dots only: <c>FreeWilly-Setup-1.2.3.exe</c>
    /// matches and a hand-attached <c>FreeWilly-Setup-notes.exe</c> does not.
    /// </remarks>
    [GeneratedRegex(
        @"^FreeWilly-Setup-\d+(\.\d+)*\.exe$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Installer();
}

/// <summary>An <see cref="IReleaseFeed"/> over the repository's own release API.</summary>
/// <remarks>
/// The only thing in this project that reaches <see cref="ReleaseCheck.Host"/>, and it is constructed
/// only where <c>TraySettings.CheckForReleases</c> is on — so an install nobody has changed anything
/// on never creates one.
/// </remarks>
public sealed class GitHubReleaseFeed : IReleaseFeed, IDisposable
{
    private readonly HttpClient _client;

    /// <summary>Construct a feed with its own client.</summary>
    public GitHubReleaseFeed()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
    {
    }

    /// <summary>Construct a feed over a supplied client.</summary>
    /// <param name="client">The client to use, owned by this instance.</param>
    public GitHubReleaseFeed(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;

        // The API answers 403 to a request with no user agent, which would read here as "nothing
        // newer" forever. The product and its version, and deliberately nothing else: there is no
        // token, no machine id and nothing about the user in this request.
        _client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("FreeWilly", BuildVersion.Current));
    }

    /// <inheritdoc/>
    public Task<string> LatestAsync(CancellationToken cancellation) =>
        _client.GetStringAsync(ReleaseCheck.LatestReleaseApi, cancellation);

    /// <inheritdoc/>
    public void Dispose() => _client.Dispose();
}
