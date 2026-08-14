namespace FreeWilly.Tray.Cli;

/// <summary>
/// One tray per session, and what a second launch does instead of starting another (DD81).
/// </summary>
/// <remarks>
/// Nothing held this before, so every extra click was another process: another icon in the overflow,
/// another event stream open on the daemon, and another window. DD80 made that easier to reach —
/// a bare launch now opens a window, so a user who clicks twice gets two of everything.
///
/// <para><b>The tray surface only.</b> The console verbs stay concurrent: an agent running
/// <c>read context</c> while the tray is open must not be refused, so this is claimed inside the
/// tray branch of <c>Main</c> and never around the whole of it.</para>
///
/// <para><b>Local rather than global.</b> An unprefixed name lives in the session's own namespace,
/// so two people logged into one machine each get a tray — which is what they each installed.</para>
///
/// <para><b>The second instance reports nothing to the screen.</b> A double click from Explorer has
/// no console to print into, and a message box on every accidental double click would be worse than
/// the silence DD80 fixed. It signals the live instance, which raises its window, and exits zero:
/// that is the message, and it is what every Windows application does. Where a console is attached
/// the caller typed a command and expects prose, so one line goes to standard error there.</para>
/// </remarks>
internal sealed class SingleTray : IDisposable
{
    /// <summary>The name both halves agree on. Unprefixed, so it is this session's.</summary>
    private const string Name = "FreeWilly.tray";

    /// <summary>What a second launch sets to ask the live one to show itself.</summary>
    private const string RaiseName = "FreeWilly.tray.raise";

    private readonly Mutex _held;
    private readonly EventWaitHandle _raise;
    private readonly CancellationTokenSource _stopping = new();

    private SingleTray(Mutex held, EventWaitHandle raise)
    {
        _held = held;
        _raise = raise;
    }

    /// <summary>
    /// Take the one tray slot, or report that something else has it.
    /// </summary>
    /// <param name="only">The claim, which has to be disposed, or null.</param>
    /// <returns><see langword="true"/> where this process is the tray.</returns>
    internal static bool TryClaim(out SingleTray? only)
    {
        var mutex = new Mutex(initiallyOwned: false, Name);
        bool mine;
        try
        {
            mine = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // The previous holder died without releasing it. The wait still succeeded — this process
            // owns it now — and treating that as "somebody else has the tray" would leave a machine
            // that crashed once unable to start one again until it was restarted.
            mine = true;
        }

        if (!mine)
        {
            mutex.Dispose();
            only = null;
            return false;
        }

        only = new SingleTray(
            mutex, new EventWaitHandle(false, EventResetMode.AutoReset, RaiseName));
        return true;
    }

    /// <summary>Ask whatever holds the tray to show its window.</summary>
    /// <remarks>
    /// Opening the event by name rather than creating one: if it is not there the holder is between
    /// claiming the mutex and creating it, which is a window of microseconds, and a launch that
    /// silently did nothing is better than one that throws at a user who double-clicked.
    /// </remarks>
    internal static void RaiseTheLiveOne()
    {
        try
        {
            using var raise = EventWaitHandle.OpenExisting(RaiseName);
            _ = raise.Set();
        }
        catch (Exception exception) when (exception is WaitHandleCannotBeOpenedException
            or UnauthorizedAccessException)
        {
            // Nothing to signal, or another session's. Either way this process is not the tray and
            // has nothing useful left to do.
        }
    }

    /// <summary>Run <paramref name="raise"/> whenever a second launch asks for the window.</summary>
    /// <param name="raise">
    /// What shows the window. It is called off the UI thread, so it has to marshal — which
    /// <c>TrayApplication</c> already does with the context it keeps for the event stream.
    /// </param>
    internal void OnRaise(Action raise)
    {
        ArgumentNullException.ThrowIfNull(raise);

        // A background thread, so it cannot hold the process open by itself: quitting the tray ends
        // the message loop, and this must not outlive it.
        var listening = new Thread(() =>
        {
            var handles = new WaitHandle[] { _raise, _stopping.Token.WaitHandle };
            while (WaitHandle.WaitAny(handles) == 0)
            {
                raise();
            }
        })
        {
            IsBackground = true,
            Name = "freewilly-tray-raise",
        };

        listening.Start();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _stopping.Cancel();
        _held.ReleaseMutex();
        _held.Dispose();
        _raise.Dispose();
        _stopping.Dispose();
    }
}
