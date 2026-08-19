using FreeWilly.Core.Releases;

namespace FreeWilly.Tray;

/// <summary>
/// When the tray asks whether a newer release exists, and how often (DD154).
/// </summary>
/// <remarks>
/// One check shortly after launch and then one every six hours, which is claude-tray's cadence and is
/// chosen for the same reason: a release happens a few times a year, and anything more frequent is
/// spending somebody's rate-limit budget to learn nothing. Sixty unauthenticated requests an hour is
/// a shared NAT's whole allowance, so four a day per machine is the point.
///
/// <para><b>The setting is read on every tick rather than at construction.</b> Turning it off has to
/// stop the traffic without restarting anything, and turning it on has to start it — a watch that
/// captured the answer once would make the menu item a lie until the next launch. Off means no
/// request is made and no feed is even constructed.</para>
///
/// <para><b>It announces a version once.</b> The menu item stays offered for as long as the release
/// is newer, but a balloon every six hours about the same release is nagging, and this product's
/// whole argument is about not being the tool that does that.</para>
/// </remarks>
internal sealed class ReleaseWatch : IDisposable
{
    /// <summary>How long after a launch the first check happens.</summary>
    /// <remarks>
    /// Not immediately. The tray's constructor may be starting an engine and provisioning a
    /// distribution, and a release check is the least urgent thing this process does — it can wait
    /// until that has had the machine to itself.
    /// </remarks>
    internal static readonly TimeSpan FirstCheckAfter = TimeSpan.FromSeconds(20);

    /// <summary>How often it asks after that.</summary>
    internal static readonly TimeSpan Every = TimeSpan.FromHours(6);

    private readonly Func<bool> _wanted;
    private readonly Func<IReleaseFeed> _feed;
    private readonly Action<AvailableRelease, bool> _found;
    private readonly Lock _gate = new();
    private System.Threading.Timer? _timer;
    private Version? _announced;

    /// <summary>Construct a watch.</summary>
    /// <param name="wanted">Whether the user has turned the check on, asked at every tick.</param>
    /// <param name="found">
    /// What to do about a newer release, and whether this is the first tick to name that version —
    /// which is what separates offering it from announcing it. Called off the UI thread; the caller
    /// marshals.
    /// </param>
    /// <param name="feed">
    /// How to build a feed when one is wanted. A factory rather than a feed, so an install with the
    /// setting off never constructs the one thing here that touches the network.
    /// </param>
    internal ReleaseWatch(
        Func<bool> wanted, Action<AvailableRelease, bool> found, Func<IReleaseFeed>? feed = null)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        ArgumentNullException.ThrowIfNull(found);
        _wanted = wanted;
        _found = found;
        _feed = feed ?? (() => new GitHubReleaseFeed());
    }

    /// <summary>Start asking.</summary>
    internal void Start()
    {
        lock (_gate)
        {
            // System.Threading's and not WinForms': the check has no business on the UI thread, and
            // the callback is marshalled by whoever passed `found` rather than by the timer.
            _timer ??= new System.Threading.Timer(_ => Tick(), null, FirstCheckAfter, Every);
        }
    }

    /// <summary>Ask now, whatever the timer was going to do.</summary>
    /// <returns>The release, or <see langword="null"/> — including where the setting is off.</returns>
    /// <remarks>
    /// Awaitable because a test has to be able to observe one check without waiting twenty seconds
    /// for a timer. <see cref="Tick"/> is the same call with the result dropped.
    /// </remarks>
    internal async Task<AvailableRelease?> CheckAsync(CancellationToken cancellation = default)
    {
        if (!_wanted())
        {
            return null;
        }

        IReleaseFeed feed;
        try
        {
            feed = _feed();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // A client that could not even be constructed is the same answer as an unreachable host,
            // and for the same reason: nobody asked to be told about this.
            return null;
        }

        try
        {
            var release = await new ReleaseCheck(feed).NewerAsync(cancellation).ConfigureAwait(false);
            if (release is null)
            {
                return null;
            }

            bool first;
            lock (_gate)
            {
                first = _announced is null || release.Version > _announced;
                if (first)
                {
                    _announced = release.Version;
                }
            }

            _found(release, first);
            return release;
        }
        finally
        {
            (feed as IDisposable)?.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Tick() => _ = CheckAsync();
}
