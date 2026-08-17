namespace FreeWilly.Tray.Cli;

/// <summary>
/// One engine host per session, and what a second <c>--run</c> does instead of joining it (DD133).
/// </summary>
/// <remarks>
/// <see cref="SingleTray"/> held the tray to one process from DD81 and nothing held <c>--run</c>,
/// which is the half that could do real damage. A second one was not refused and did not fail
/// either: <see cref="Core.Engine.EngineLifecycle.StartAsync"/> finds the pipe already answering and
/// returns Running without launching a daemon or starting a relay, so the duplicate serves nothing
/// at all — and then polls, on a timer, with the authority to run <c>wsl --terminate</c> on the
/// distribution the first one is serving. A process that contributes nothing and can take the engine
/// down is the worst shape available, and it was reachable by clicking Start engine twice.
///
/// <para><b>Session-local, like the tray's.</b> The contended object is really the machine-wide
/// <c>\\.\pipe\docker_engine</c>, so a global name would be the honest scope — but creating one
/// needs a privilege a standard user does not have, and the pipe's own single-account ACL already
/// refuses the other user this would be protecting against. What is left is two hosts under one
/// login, which is the case that was actually observed and which this name covers.</para>
///
/// <para><b>No signals.</b> The tray's claim carries named events because a second launch has
/// something to ask of the first; a second engine host has nothing to ask. The one already serving
/// the pipe is the whole of the answer, so this is a claim and a release and no more.</para>
/// </remarks>
internal sealed class SingleEngine : IDisposable
{
    /// <summary>The name both halves agree on. Unprefixed, so it is this session's.</summary>
    /// <remarks>
    /// Internal for the reason <see cref="SingleTray.Name"/> is: the suite claims this very object,
    /// and the message it prints when a running engine already holds it has to name the object
    /// rather than a second spelling of it.
    /// </remarks>
    internal const string Name = "FreeWilly.engine";

    private readonly Mutex _held;

    private SingleEngine(Mutex held) => _held = held;

    /// <summary>
    /// Take the one engine-host slot, or report that something else has it.
    /// </summary>
    /// <param name="only">The claim, which has to be disposed, or null.</param>
    /// <returns><see langword="true"/> where this process is the engine host.</returns>
    internal static bool TryClaim(out SingleEngine? only)
    {
        var mutex = new Mutex(initiallyOwned: false, Name);
        bool mine;
        try
        {
            mine = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // The previous host died without releasing it — which for this one is the ordinary
            // ending, since a machine that loses power mid-build leaves exactly this. The wait
            // still succeeded and this process owns it now; reading it as "somebody else is
            // serving" would leave a machine that crashed once unable to start its engine again
            // until the user logged out.
            mine = true;
        }

        if (!mine)
        {
            mutex.Dispose();
            only = null;
            return false;
        }

        only = new SingleEngine(mutex);
        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _held.ReleaseMutex();
        _held.Dispose();
    }
}
