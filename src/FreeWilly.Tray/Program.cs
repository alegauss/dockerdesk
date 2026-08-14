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
    private Icon? _worn;
    private bool _startRequested;
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

        // Built by TrayMenu rather than here, so the menu `--show-menu` photographs is this one and
        // not a second one built for the camera (DD67).
        _menu = new TrayMenu(StartEngine, StopEngine, OpenWindow, Quit);
        _icon.ContextMenuStrip = _menu.Strip;

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
        _startRequested = true;
        Show(EngineState.Starting);
        Complain(_holder.Start());
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
        Show(_shown);
        _icon.BalloonTipTitle = "FreeWilly";
        _icon.BalloonTipText = failure;
        _icon.ShowBalloonTip(8000);
    }

    private void StopEngine()
    {
        _startRequested = false;
        Show(EngineState.Stopped);
        Complain(_holder.Stop());
    }

    private void Show(EngineState state)
    {
        if (state is EngineState.Running)
        {
            _startRequested = false;
        }

        var changed = _shown != state;
        _shown = state;

        var next = StateIcon.Icon(state);
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
    /// Quit the tray and leave the engine exactly as it is.
    /// </summary>
    /// <remarks>
    /// The asymmetry is the point: the only thing that stops the engine is the menu item that says
    /// so. A container someone else is using does not die because an icon was closed.
    /// </remarks>
    private void Quit()
    {
        _icon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _events.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
    [STAThread]
    private static int Main(string[] args)
    {
        var route = Cli.CommandLine.Of(args);

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
                ApplicationConfiguration.Initialize();

                // Without this, no WPF window this process opens receives a single key press: the
                // pump below is WinForms' and WPF expects its own. See Ui/WpfInputBridge.cs — the
                // filter box was where it showed, and every capture of this window missed it.
                Ui.WpfInputBridge.Install();

                var tray = new TrayApplication(openWindow: route.OpenWindow);
                only!.OnRaise(tray.RaiseWindow);
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
