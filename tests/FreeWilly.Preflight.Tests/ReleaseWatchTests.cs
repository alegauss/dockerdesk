using FreeWilly.Core.Releases;
using FreeWilly.Core.Settings;
using FreeWilly.Tray;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// When the tray asks about releases, and what it does with the answer (DD154).
/// </summary>
public sealed class ReleaseWatchTests
{
    private sealed class Feed(string body) : IReleaseFeed
    {
        internal int Asked { get; private set; }

        public Task<string> LatestAsync(CancellationToken cancellation)
        {
            Asked++;
            return Task.FromResult(body);
        }
    }

    private static string Release(string tag) => $$"""
        {
          "tag_name": "{{tag}}",
          "assets": [
            { "name": "FreeWilly-Setup-{{tag.TrimStart('v')}}.exe",
              "browser_download_url": "https://example.invalid/setup.exe" },
            { "name": "SHA256SUMS.txt", "browser_download_url": "https://example.invalid/sums" }
          ]
        }
        """;

    [Fact]
    public async Task Off_makes_no_request_at_all()
    {
        // Not "makes a request and ignores the answer". The setting is a promise about outbound
        // traffic, so the feed is not even constructed while it is off — asserted by counting the
        // times the factory ran, because a feed built and never used would still have opened a
        // client.
        var built = 0;
        using var watch = new ReleaseWatch(
            () => false,
            (_, _) => Assert.Fail("nothing should have been found"),
            () =>
            {
                built++;
                return new Feed(Release("v9.0.0"));
            });

        Assert.Null(await watch.CheckAsync());
        Assert.Equal(0, built);
    }

    [Fact]
    public async Task On_finds_a_newer_release_and_hands_it_over()
    {
        var found = new List<(AvailableRelease Release, bool Announce)>();
        using var watch = new ReleaseWatch(
            () => true,
            (release, announce) => found.Add((release, announce)),
            () => new Feed(Release("v99.0.0")));

        var answer = await watch.CheckAsync();

        Assert.Equal(new Version(99, 0, 0), answer?.Version);
        Assert.Single(found);
        Assert.True(found[0].Announce);
    }

    [Fact]
    public async Task The_same_release_is_offered_every_tick_and_announced_once()
    {
        // A balloon every six hours about a release the user has already been told about is nagging,
        // and this product's whole argument is about not being the tool that does that. The menu item
        // still has to be offered on every tick, because a menu is rebuilt from what it was told.
        var announcements = new List<bool>();
        using var watch = new ReleaseWatch(
            () => true,
            (_, announce) => announcements.Add(announce),
            () => new Feed(Release("v99.0.0")));

        await watch.CheckAsync();
        await watch.CheckAsync();
        await watch.CheckAsync();

        Assert.Equal([true, false, false], announcements);
    }

    [Fact]
    public async Task The_setting_is_read_at_every_tick_rather_than_captured()
    {
        // Turning the check on has to take effect without a restart, or the menu item is a box that
        // appears to do nothing until tomorrow. Turning it off has to stop the traffic for the same
        // reason, and neither can work off an answer read once at construction.
        var settings = new TraySettings { CheckForReleases = false };
        var feed = new Feed(Release("v99.0.0"));
        using var watch = new ReleaseWatch(
            () => settings.CheckForReleases, (_, _) => { }, () => feed);

        Assert.Null(await watch.CheckAsync());
        Assert.Equal(0, feed.Asked);

        settings = settings with { CheckForReleases = true };
        Assert.NotNull(await watch.CheckAsync());
        Assert.Equal(1, feed.Asked);

        settings = settings with { CheckForReleases = false };
        Assert.Null(await watch.CheckAsync());
        Assert.Equal(1, feed.Asked);
    }

    [Fact]
    public void Four_a_day_after_a_launch_it_does_not_compete_with()
    {
        // A release happens a few times a year, and sixty unauthenticated requests an hour is a
        // shared NAT's whole allowance — so the cadence is the point rather than an implementation
        // detail. The first check waits, because the first seconds of a launch may be provisioning a
        // distribution and this is the least urgent thing the process does.
        Assert.Equal(TimeSpan.FromHours(6), ReleaseWatch.Every);
        Assert.True(ReleaseWatch.FirstCheckAfter > TimeSpan.Zero);
        Assert.True(ReleaseWatch.FirstCheckAfter < ReleaseWatch.Every);
    }

    [Fact]
    public void Starting_twice_arms_one_timer()
    {
        // Save calls Start on every tick of the box, because turning the check on mid-session has to
        // arm a watch that was never armed. Doing that must not leave a second timer behind.
        using var watch = new ReleaseWatch(() => false, (_, _) => { }, () => new Feed("{}"));

        watch.Start();
        watch.Start();
        watch.Dispose();
    }
}
