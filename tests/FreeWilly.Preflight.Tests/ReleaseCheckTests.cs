using FreeWilly.Core.Licensing;
using FreeWilly.Core.Releases;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// Whether a newer release exists, and every way that question is answered "no" (DD154).
/// </summary>
/// <remarks>
/// The parsing is where every defect this can have lives, and none of it needs a host: the seam takes
/// a string, so a release description is a test fixture. What is asserted here is mostly refusal — an
/// ambiguous installer, a release with no digest, a body that is not JSON — because a check that
/// guessed would end up running an executable nobody chose.
/// </remarks>
public sealed class ReleaseCheckTests
{
    /// <summary>An <see cref="IReleaseFeed"/> answering with text, or throwing.</summary>
    private sealed class Feed(string? body) : IReleaseFeed
    {
        public Task<string> LatestAsync(CancellationToken cancellation) =>
            body is null
                ? Task.FromException<string>(new HttpRequestException("pretend the proxy said no"))
                : Task.FromResult(body);
    }

    /// <summary>A release description in the shape the API answers with.</summary>
    private static string Release(string tag, params string[] assetNames)
    {
        var assets = string.Join(
            ",\n",
            assetNames.Select(name =>
                $$""" { "name": "{{name}}", "browser_download_url": "https://example.invalid/{{name}}" } """));

        return $$"""
            { "tag_name": "{{tag}}", "assets": [ {{assets}} ] }
            """;
    }

    private const string Installer = "FreeWilly-Setup-2.0.0.exe";

    [Fact]
    public void A_newer_tag_is_offered_with_both_files_an_apply_needs()
    {
        // The ordinary case, and the only one that ends in something the user can act on.
        var found = ReleaseCheck.NewerThan(
            Release("v2.0.0", Installer, ReleaseCheck.SumsAssetName), new Version(1, 0, 0));

        Assert.NotNull(found);
        Assert.Equal(new Version(2, 0, 0), found.Version);
        Assert.Equal("v2.0.0", found.Tag);
        Assert.Equal(Installer, found.InstallerName);
        Assert.EndsWith(Installer, found.InstallerUrl, StringComparison.Ordinal);
        Assert.EndsWith(ReleaseCheck.SumsAssetName, found.SumsUrl, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("v1.0.0")]
    [InlineData("v0.9.9")]
    public void The_release_this_build_already_is_or_is_past_is_not_an_update(string tag) =>
        Assert.Null(ReleaseCheck.NewerThan(
            Release(tag, Installer, ReleaseCheck.SumsAssetName), new Version(1, 0, 0)));

    [Fact]
    public void Two_files_answering_the_installer_pattern_are_refused_rather_than_resolved()
    {
        // DD152's asset name carries the version, so the lookup is a pattern rather than a name —
        // and a pattern that matched twice would otherwise be resolved by taking whichever came
        // first out of the array, which is a guess about which .exe to run on somebody's machine.
        Assert.Null(ReleaseCheck.NewerThan(
            Release("v2.0.0", Installer, "FreeWilly-Setup-2.0.1.exe", ReleaseCheck.SumsAssetName),
            new Version(1, 0, 0)));
    }

    [Fact]
    public void A_release_with_no_digest_published_for_it_offers_nothing()
    {
        // Not a smaller failure than a missing installer. Every artefact this tool fetches is checked
        // against a pinned digest, and an update with nothing to check against would be the one
        // download the product ran blind.
        Assert.Null(ReleaseCheck.NewerThan(Release("v2.0.0", Installer), new Version(1, 0, 0)));
    }

    [Fact]
    public void An_installer_whose_name_is_not_a_version_is_not_the_installer()
    {
        // The pattern is anchored and its version group is digits and dots, so a file somebody
        // attached by hand does not become the thing this runs silently.
        Assert.Null(ReleaseCheck.NewerThan(
            Release("v2.0.0", "FreeWilly-Setup-nightly.exe", ReleaseCheck.SumsAssetName),
            new Version(1, 0, 0)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("""{ "message": "Not Found" }""")]
    [InlineData("""{ "tag_name": "release-candidate", "assets": [] }""")]
    [InlineData("""{ "tag_name": "v2.0.0" }""")]
    public void Anything_that_is_not_a_release_reads_as_nothing_newer(string body) =>
        Assert.Null(ReleaseCheck.NewerThan(body, new Version(1, 0, 0)));

    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("v.1.2.3", 1, 2, 3)]
    [InlineData("  v1.2  ", 1, 2, 0)]
    [InlineData("v4", 4, 0, 0)]
    [InlineData("v1.2.3.4", 1, 2, 3)]
    public void A_tag_naming_slip_does_not_silently_stop_update_detection(
        string tag, int major, int minor, int build)
    {
        // The leading non-digit run is skipped, and the answer is three parts — which is what
        // Directory.Build.props states and what the installer's file name carries. A fourth part the
        // assembly happens to have would otherwise make an equal version compare greater.
        Assert.True(ReleaseCheck.TryParseVersion(tag, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v")]
    [InlineData("nightly")]
    public void A_tag_that_names_no_version_is_not_read_as_one(string tag) =>
        Assert.False(ReleaseCheck.TryParseVersion(tag, out _));

    [Fact]
    public void This_build_compares_as_the_version_it_says_it_is()
    {
        // BuildVersion is the one source for it, and the comparison needs it normalised the same way
        // a tag is — otherwise a release that matches this build would look newer than it.
        Assert.True(ReleaseCheck.TryParseVersion(BuildVersion.Current, out var stated));
        Assert.Equal(stated, ReleaseCheck.ThisBuild);
    }

    [Fact]
    public async Task An_unreachable_host_answers_nothing_newer_rather_than_throwing()
    {
        // Offline, blocked by a proxy, rate-limited, or pointed at a repository with no releases. A
        // check the user did not initiate must not be able to produce a complaint, and there is no
        // remedy any of these would let them apply.
        Assert.Null(await new ReleaseCheck(new Feed(null), new Version(1, 0, 0)).NewerAsync());
    }

    [Fact]
    public async Task A_reachable_host_with_something_newer_says_so()
    {
        var found = await new ReleaseCheck(
                new Feed(Release("v3.1.0", "FreeWilly-Setup-3.1.0.exe", ReleaseCheck.SumsAssetName)),
                new Version(1, 0, 0))
            .NewerAsync();

        Assert.Equal(new Version(3, 1, 0), found?.Version);
    }

    [Fact]
    public void It_names_one_host_and_one_release()
    {
        // The site restates both, so they are read from here rather than typed twice (DD157's law).
        Assert.Contains(ReleaseCheck.Host, ReleaseCheck.LatestReleaseApi, StringComparison.Ordinal);
        Assert.EndsWith("/releases/latest", ReleaseCheck.LatestReleaseApi, StringComparison.Ordinal);
    }
}
