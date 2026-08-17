using FreeWilly.Core.Api;
using FreeWilly.Core.Engine;
using FreeWilly.Tray.Ui;

namespace FreeWilly.Tray;

/// <summary>
/// The tray icon. Where this tool lives between tasks, because "is Docker up?" should be a glance.
/// </summary>
internal sealed class TrayApplication : ApplicationContext
{
    private readonly NotifyIcon _icon = new();
    private readonly TrayMenu _menu;
    private readonly EngineHolder _holder;
    private readonly DockerApi _api = new();
    private readonly EngineEvents _events;
    private readonly SynchronizationContext _ui;
    private readonly TrayScale _scale;
    private readonly EnginePaths _paths = new();
    private Icon? _worn;
    private EngineOnLaunch _onLaunch;
    private bool _startRequested;
    private bool _engineToldToStop;
    private CancellationTokenSource? _landing;
    private EngineState _shown = EngineState.Stopped;

    /// <summary>Construct the tray.</summary>
    /// <param name="openWindow">
    /// Whether to show the window straight away. A shortcut wants this, and so does a user whose
    /// icon Windows filed into the overflow, where the menu is a click away rather than in sight.
    /// </param>
    internal TrayApplication(bool openWindow = false)
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _holder = new EngineHolder(EngineHolder.ThisProcess(), new DetachedLauncher());

        // Read before the menu is built, because the box has to open showing what is true (DD135).
        _onLaunch = EngineOnLaunch.Read(_paths.Settings);

        // Built by TrayMenu rather than here, so the menu `--show-menu` photographs is this one and
        // not a second one built for the camera (DD67).
        _menu = new TrayMenu(
            StartEngine, StopEngine, OpenWindow, Quit, SetStartWithTheTray,
            _onLaunch.StartWithTheTray);
        _icon.ContextMenuStrip = _menu.Strip;

        // The primary button, which the icon answered with nothing at all until DD140. NotifyIcon
        // raises the context menu on the secondary button by itself and does nothing whatsoever on
        // the other, so the one surface this product keeps on screen all day ignored the click every
        // tray tool teaches a user to make — which reads as an icon that is broken rather than as a
        // menu hiding under the other button.
        //
        // MouseClick and not Click, because Click fires for the secondary button too: the same
        // gesture has already raised the menu, and the window would arrive behind the popup that
        // asked for it. A double click reaches this twice and is harmless — the second is the
        // activate branch of OpenWindow, which is what a second launch does anyway (DD81).
        _icon.MouseClick += (_, click) =>
        {
            if (click.Button is MouseButtons.Left)
            {
                OpenWindow();
            }
        };

        _scale = new TrayScale(() => _ui.Post(_ => Show(_shown), null));

        // The image and the tooltip BEFORE visibility, and the order is the whole of DD82. Setting
        // Visible is what emits the shell's notify-add, and Windows persists what that call carried:
        // with the holder still empty the add went out with no icon flag and an empty string, and
        // although the very next line repaired the image with a modify, the tooltip Windows had
        // already stored stayed empty. Measured — this executable's notify-icon settings entry held a
        // zero-length tooltip beside an icon snapshot that decoded fine.
        //
        // It matters because of where the icon lives. DD21 established that Windows files a
        // first-seen icon into the overflow and that nothing here can promote it out, and the
        // overflow flyout labels each entry with exactly that persisted tooltip — so the one surface
        // a user has to read to find this tool was the one naming nothing.
        Show(EngineState.Stopped);
        _icon.Visible = true;

        // The size the icon was just drawn at is only right for the display it was drawn on, and that
        // display changes without the process restarting — a dock, an undock, the scale slider. The
        // watch fires on a thread of its own, so the redraw is posted through the same context the
        // event stream uses; calling Show from there would touch NotifyIcon off the UI thread (DD99).
        _scale.Watch();

        // The exits nobody thinks of as quitting (DD129). A logoff and a shutdown tear this process
        // down without Quit ever running, and the detached `--run` it launched has no parent to
        // notice — so the virtual machine outlived the session that asked for it, which is the same
        // held memory DD128 removed from one exit only. A kill gets no notice and this does not
        // pretend otherwise; what it buys is that signing out at the end of a day leaves nothing up.
        Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;

        // The indicator is the event loop's own connection state: connected exactly when the engine
        // is answering. No timer, and no second definition of "running".
        _events = new EngineEvents(new DockerApiEventSource(_api));
        _events.StateChanged += stream => _ui.Post(_ => Show(TrayState.For(stream, _startRequested)), null);

        // The window is a view of the engine, so what makes it correct is the same stream. Only the
        // events that change a container list ask for a read; the rest would be a poll in disguise.
        // The id travels with it: this event is also what confirms an action the user is waiting on.
        _events.Received += e =>
        {
            if (e.ChangesTheContainerList)
            {
                _ui.Post(_ => _ = _open?.RefreshAsync(e.Id), null);
            }

            // Images are their own list with their own reasons to change, and the window decides
            // whether anybody is looking at them.
            if (e.ChangesTheImageList)
            {
                _ui.Post(_ => _ = _open?.RefreshImagesIfShowingAsync(), null);
            }
        };
        _events.Start();

        if (openWindow)
        {
            OpenWindow();
        }

        // Last in the constructor, and that ordering is load-bearing (DD135). StartEngine complains
        // through the balloon and draws through Show, so both the icon and the menu have to exist
        // before it runs; the event stream has to be started too, or the start that lands has
        // nothing watching to turn the dot green.
        //
        // Through the same method the menu item uses rather than a quiet second path. A start that
        // cannot land — no distribution registered — then says so here exactly as it would if the
        // user had pressed it, which is the one case where starting unasked must not fail silently.
        if (_onLaunch.StartWithTheTray)
        {
            StartEngine();
        }
    }

    /// <summary>Turn "start the engine with the tray" on or off, and remember it (DD135).</summary>
    /// <param name="wanted">What the box now says.</param>
    /// <remarks>
    /// Deliberately does not start or stop anything. The setting is about the next launch, and a
    /// tick that also started an engine would make the box a verb — there are already two of those
    /// directly above it, and a user who wanted the engine now would have pressed one.
    /// </remarks>
    private void SetStartWithTheTray(bool wanted)
    {
        _onLaunch = _onLaunch with { StartWithTheTray = wanted };
        _onLaunch.Write(_paths.Settings);
    }

    private Ui.MainWindow? _open;

    /// <summary>Show the window, or bring the one already open to the front.</summary>
    /// <summary>Show the window because a second launch asked for it (DD81).</summary>
    /// <remarks>
    /// Called from the thread waiting on the signal, so it marshals onto the UI thread through the
    /// same context the event stream already posts through. Activating from a background thread is
    /// how a window ends up behind the one that asked for it.
    /// </remarks>
    internal void RaiseWindow() => _ui.Post(_ => OpenWindow(), null);

    /// <summary>Close the tray because <c>--quit</c> asked for it (DD121).</summary>
    /// <remarks>
    /// Posted for the same reason <see cref="RaiseWindow"/> is: the signal arrives on a background
    /// thread, and <see cref="Quit"/> hides the notify icon and ends the message loop — both of which
    /// belong to the UI thread. It is the menu item's own exit and not a second one, which since
    /// DD128 is what makes this enough on its own: the engine comes down with the tray, so an
    /// uninstaller that used to have to run <c>--stop</c> as a second step no longer does.
    /// </remarks>
    internal void QuitFromSignal() => _ui.Post(_ => Quit(), null);

    /// <summary>
    /// Open the build a link left in the handoff, raising the window for it (DD126).
    /// </summary>
    /// <remarks>
    /// Posted for the reason <see cref="RaiseWindow"/> is: the signal arrives on a background thread
    /// and every line below touches the window. Taking the ref is what clears it, so a link that
    /// arrives while the window is already open does not leave one behind for the next launch.
    ///
    /// <para>Silent where there is nothing waiting. The signal and the file are written by two
    /// separate calls, and a raced or failed write should leave the window as it was rather than
    /// clearing whatever build is on screen.</para>
    /// </remarks>
    internal void ShowBuildFromSignal() => _ui.Post(_ => ShowPendingBuild(), null);

    /// <summary>Open one build, named directly rather than through the handoff (DD126).</summary>
    /// <param name="reference">The ref, already read out of the link.</param>
    /// <remarks>
    /// The path for the launch that opened the link itself: the ref never left this process, so
    /// there is no file to take. Posted like the signal's, because it runs before the message loop
    /// has started and the window has to be created on it.
    /// </remarks>
    internal void ShowBuild(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        _ui.Post(_ => Open(reference), null);
    }

    private void ShowPendingBuild()
    {
        if (new FreeWilly.Core.Builds.BuildHandoff().Take() is { } reference)
        {
            Open(reference);
        }
    }

    private void Open(string reference)
    {
        OpenWindow();
        _open?.ShowBuild(reference);
    }

    private void OpenWindow()
    {
        if (_open is not null)
        {
            // Restore before activating: a minimised window that is only activated stays minimised,
            // so a second launch would look like nothing happened — which is the whole failure
            // DD81 exists to remove.
            if (_open.WindowState is System.Windows.WindowState.Minimized)
            {
                _open.WindowState = System.Windows.WindowState.Normal;
            }

            _ = _open.Activate();
            return;
        }

        // WPF needs an Application carrying the chrome before a Window can resolve it, and a WinForms
        // process has none. How that is made is Ui.Theme's business and not this one's (DD34).
        Ui.Theme.Apply();

        _open = new Ui.MainWindow(_api, () => _shown, StartEngine);
        _open.Closed += (_, _) => _open = null;
        _open.Show();
        _ = _open.RefreshAsync();
    }

    private void StartEngine()
    {
        // Asked before anything is shown (DD120). A machine with no distribution has a start that
        // can only fail, and the failure used to be invisible: `--run` printed its refusal onto a
        // hidden console and exited, leaving the dot breathing Starting until the tray was quit.
        var refusal = TrayState.WhyAStartWouldNotLand(
            _paths.DistributionRegistered, _paths.DistributionName);
        if (refusal is not null)
        {
            Complain(refusal);
            return;
        }

        _startRequested = true;
        Show(EngineState.Starting);

        var failure = _holder.Start();
        if (failure is not null)
        {
            Complain(failure);
            return;
        }

        WatchTheStart();
    }

    /// <summary>
    /// Stop claiming a start is coming once it has had longer than the engine gives itself.
    /// </summary>
    /// <remarks>
    /// The stream is still the only definition of running, and nothing here polls the engine — this
    /// decides one thing the stream cannot, which is how long a request may outlive the evidence for
    /// it. Every way a launched start dies quietly ends up here: the daemon exits, the pipe is taken
    /// by something else, the distribution will not boot.
    ///
    /// <para>A timer that is cancelled rather than one that is checked, so a start that lands leaves
    /// nothing running behind it. The continuation posts through the same context the event stream
    /// uses, because <see cref="Show"/> touches the NotifyIcon and that is the UI thread's.</para>
    /// </remarks>
    private void WatchTheStart()
    {
        StopWatchingTheStart();

        var watch = new CancellationTokenSource();
        _landing = watch;
        _ = Task.Delay(TrayState.StartBudget, watch.Token).ContinueWith(
            _ => _ui.Post(_ => GiveUpOnTheStart(), null),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    private void StopWatchingTheStart()
    {
        _landing?.Cancel();
        _landing?.Dispose();
        _landing = null;
    }

    private void GiveUpOnTheStart()
    {
        // The engine may have landed between the delay elapsing and this reaching the UI thread, and
        // a stop pressed in the same window is the same question. Either way the request is over and
        // there is nothing to complain about.
        if (!_startRequested)
        {
            return;
        }

        Complain(TrayState.StartDidNotLand(TrayState.StartBudget, _paths.DistributionName));
    }

    /// <summary>
    /// Say what went wrong without dying of it.
    /// </summary>
    /// <remarks>
    /// A balloon rather than a dialog: the tray has no window of its own to parent one to, and a
    /// modal from an icon in the corner is a jump scare. Silence is not the alternative — an
    /// unhandled exception in a click handler was the previous behaviour, and it took the tray with
    /// it, so pressing a menu item made the icon disappear.
    /// </remarks>
    private void Complain(string? failure)
    {
        if (failure is null)
        {
            return;
        }

        _startRequested = false;
        StopWatchingTheStart();
        Show(_shown);
        _icon.BalloonTipTitle = "FreeWilly";
        _icon.BalloonTipText = failure;
        _icon.ShowBalloonTip(8000);
    }

    private void StopEngine()
    {
        _startRequested = false;
        StopWatchingTheStart();
        Show(EngineState.Stopped);
        Complain(_holder.Stop());
    }

    private void Show(EngineState state)
    {
        if (state is EngineState.Running)
        {
            _startRequested = false;

            // It landed, so the budget has nothing left to decide. Cancelled rather than left to
            // elapse into a check that finds nothing: a tray left open all day would otherwise hold
            // one of these per start it ever made.
            StopWatchingTheStart();
        }

        var changed = _shown != state;
        _shown = state;

        // Asked here rather than inside Icon, so what the watch compares against is the size that
        // actually reached the shell and not a size something intended to use (DD99).
        var next = StateIcon.Icon(state, _scale.Drawing());
        _icon.Icon = next;

        // The previous icon owns an unmanaged handle from GetHicon; replacing it without destroying
        // it leaks one per state change, and this changes state whenever the engine does.
        _worn?.Dispose();
        _worn = next;

        _icon.Text = StateIcon.TooltipFor(state);
        _menu.Reflect(state);

        // The window shows the engine state too, and its empty state depends on it.
        if (changed)
        {
            _ = _open?.RefreshAsync();
        }
    }

    /// <summary>
    /// Quit the tray and take the engine with it (DD128).
    /// </summary>
    /// <remarks>
    /// This used to leave the engine exactly as it was, and the asymmetry was stated as the point: a
    /// container someone else is using should not die because an icon was closed. Measured against
    /// the complaint this project exists about, that trade costs more than it saves — a running
    /// engine holds a WSL2 virtual machine, and the only way to get those gigabytes back was to
    /// remember a second menu item before pressing this one. Somebody who quits has said they are
    /// done.
    ///
    /// <para>It runs the stop the menu item already runs, and deliberately not a second spelling of
    /// it: <see cref="EngineHolder.Stop"/> launches <c>--stop</c>, which stops serving the pipe,
    /// kills the daemon and terminates the distribution. That process is detached and outlives this
    /// one by design, so the icon still goes at once and nothing here waits on a virtual machine
    /// shutting down.</para>
    ///
    /// <para><b>The virtual machine is left to WSL rather than shut down from here.</b> Terminating
    /// the distribution this tool owns is the whole of what this install is entitled to do;
    /// <c>wsl --shutdown</c> would take every other distribution on the machine with it, which is
    /// somebody's Ubuntu shell in another window. With ours gone WSL powers the machine down itself
    /// once nothing else is using it, so the memory comes back either way — the difference is only
    /// whether this tool reaches outside what it owns to make it happen sooner.</para>
    ///
    /// <para>The failure is swallowed rather than shown. Every surface that reports one is on its
    /// way out of existence — the balloon needs an icon that is about to be hidden, and a modal
    /// would hold open the exit the user just asked for.</para>
    /// </remarks>
    private void Quit()
    {
        StopTheEngine();
        _icon.Visible = false;
        ExitThread();
    }

    /// <summary>Windows is ending the session, so leave nothing behind (DD129).</summary>
    /// <param name="sender">Unused.</param>
    /// <param name="ending">Unused; a logoff and a shutdown are answered the same way.</param>
    /// <remarks>
    /// A spawn and never a wait. This runs inside a window Windows may close at any moment, and
    /// <see cref="EngineHolder.Stop"/> is a <see cref="System.Diagnostics.Process"/> start that
    /// returns long before the
    /// distribution is down — which is exactly what makes it safe to do here. Blocking on the
    /// virtual machine shutting down would risk being killed halfway and is no more effective.
    ///
    /// <para>The icon and the message loop are left alone. Windows is already taking them, and
    /// touching UI from this callback would be doing it from the wrong thread on the way out.</para>
    /// </remarks>
    private void OnSessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs ending) =>
        StopTheEngine();

    /// <summary>Ask the engine to stop, at most once over this tray's life (DD129).</summary>
    /// <remarks>
    /// Guarded because there are now two callers and a session can reach both: Windows raises
    /// <c>SessionEnding</c> and a tray that then processes its own quit would
    /// launch a second <c>--stop</c> against a distribution already terminated. Harmless, but it is
    /// a process spawned during a shutdown for no reason, and the false one would report a failure
    /// nobody could act on.
    /// </remarks>
    private void StopTheEngine()
    {
        if (_engineToldToStop)
        {
            return;
        }

        _engineToldToStop = true;
        _ = _holder.Stop();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopWatchingTheStart();
            _events.DisposeAsync().AsTask().GetAwaiter().GetResult();

            // SystemEvents is static and holds its subscribers alive, so leaving these attached is a
            // leak that outlives the tray they were for — the session hook (DD129) as much as the
            // scale watch, and it is the same static object holding both.
            Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding;
            _scale.Dispose();
            _api.Dispose();
            _icon.Dispose();
            _worn?.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// The one entry point, for the one executable.
/// </summary>
/// <remarks>
/// Every face of this tool is behind here: the tray with no arguments, and each console verb behind
/// its own. What that buys is DD14's whole shape — one file to publish, one to sign, one to install
/// and one to hand somebody. It also removes a failure the tray could not do anything about: it used
/// to look for <c>dockerdesk-engine.exe</c> beside itself, and a copy that arrived without it had a
/// Start engine menu item that could only apologise.
/// </remarks>
internal static class Program
{
    /// <summary>
    /// A ref this process must open itself, where the handoff file could not be written (DD126).
    /// </summary>
    /// <remarks>
    /// The file is how a ref reaches a tray in another process, and it is also the ordinary path
    /// here — the tray takes it a moment later. This field is only the fallback for a machine whose
    /// root is not writable, where the ref never leaves this process and so needs no file at all.
    /// </remarks>
    private static string? _pendingBuild;

    [STAThread]
    private static int Main(string[] args)
    {
        var route = Cli.CommandLine.Of(args);

        // A build link, which is the shell invoking this install's handler for docker-desktop://
        // (DD126). Resolved before the tray branch because it may end in either place: a running tray
        // is told to open it, and a machine with none becomes one showing it.
        if (route.Surface is Cli.Surface.OpenBuild)
        {
            if (FreeWilly.Core.Builds.BuildAddress.RefIn(route.Arguments[0]) is not { } reference)
            {
                Cli.ParentConsole.Attach();
                Console.Error.WriteLine(
                    $"{Cli.CommandLine.ExecutableName}: that is not a build link.");
                return 2;
            }

            // Written before the signal, so the tray that hears it has something to take.
            var handoff = new FreeWilly.Core.Builds.BuildHandoff();

            if (handoff.Leave(reference) && Cli.SingleTray.AskTheLiveOneToShowABuild())
            {
                return 0;
            }

            // Nothing was listening, so this process becomes the tray and opens the build itself.
            // Falling through rather than exiting is what makes a link work on a machine where the
            // tray is not running, which is most machines most of the time.
            //
            // The file is taken back first. It was written for a reader that turned out not to
            // exist, and leaving it would make the next ordinary launch open a build nobody asked
            // for — the ref travels in this process from here, which needs no file.
            handoff.Take();
            route = route with { Surface = Cli.Surface.Tray, OpenWindow = true };
            _pendingBuild = reference;
        }

        if (route.Surface is Cli.Surface.Tray)
        {
            // One tray per session (DD81). A second launch raises the window of the one already
            // running and exits: two icons and two event streams on one daemon is what every extra
            // click used to buy, and DD80 made that easier to reach by opening a window on a bare
            // launch. The guard is here rather than around the whole of Main because the console
            // verbs stay concurrent — an agent reading while the tray is open must not be refused.
            if (!Cli.SingleTray.TryClaim(out var only))
            {
                Cli.SingleTray.RaiseTheLiveOne();

                // Only where somebody typed a command. From Explorer this attaches to nothing and
                // the line goes nowhere, which is the right amount of noise for a double click.
                Cli.ParentConsole.Attach();
                Console.Error.WriteLine(
                    $"{Cli.CommandLine.ExecutableName}: already running — raised its window.");
                return 0;
            }

            using (only)
            {
                // The user's DOCKER_CONFIG, repaired before anything is drawn (DD124). The installer
                // writes it beside the PATH entry, so a fresh install arrives correct; this is what
                // reaches an install made before DD124, and what puts the value back if something
                // else cleared it. It writes nothing unless this install is on the user's PATH, and
                // nothing when the value is already right — so the common case is one registry read.
                _ = new DockerConfigEntry().Ensure();

                ApplicationConfiguration.Initialize();

                // Without this, no WPF window this process opens receives a single key press: the
                // pump below is WinForms' and WPF expects its own. See Ui/WpfInputBridge.cs — the
                // filter box was where it showed, and every capture of this window missed it.
                Ui.WpfInputBridge.Install();

                var tray = new TrayApplication(openWindow: route.OpenWindow);
                only!.OnRaise(tray.RaiseWindow);
                only.OnQuit(tray.QuitFromSignal);
                only.OnBuild(tray.ShowBuildFromSignal);

                // The link this launch arrived on, if it arrived on one (DD126). Only then: reading
                // the handoff on every start would make a file left by an earlier run open a build
                // on an ordinary launch.
                if (_pendingBuild is { } direct)
                {
                    tray.ShowBuild(direct);
                }

                Application.Run(tray);
            }

            return 0;
        }

        // Every remaining surface writes, so the console comes first: attaching after something has
        // already printed means the first lines went to Stream.Null.
        Cli.ParentConsole.Attach();

        switch (route.Surface)
        {
            case Cli.Surface.Agent:
                return Cli.AgentSurface.Run(route.Arguments);
            case Cli.Surface.CaptureWindow:
                return Cli.WindowCapture.Run(route.Arguments);
            case Cli.Surface.ShowMenu:
                return Cli.MenuPreview.Run(route.Arguments);
            case Cli.Surface.Quit:
                return Cli.QuitCommand.Run(route.Arguments);
            case Cli.Surface.Preflight:
                return Cli.PreflightCommand.Run(route.Arguments);
            case Cli.Surface.Engine:
                return Cli.EngineCommand.Run(route.Arguments);
            case Cli.Surface.Version:
                Console.Out.WriteLine(Core.Licensing.BuildVersion.Current);
                return 0;
            case Cli.Surface.Help:
                Console.Out.Write(Cli.CommandLine.HelpText);
                return 0;
            default:
                Console.Error.WriteLine(
                    $"{Cli.CommandLine.ExecutableName}: "
                    + $"unknown argument {string.Join(' ', route.Arguments)}");
                Console.Error.Write(Cli.CommandLine.HelpText);
                return 2;
        }
    }
}
