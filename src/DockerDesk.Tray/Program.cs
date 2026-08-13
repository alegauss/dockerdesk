using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;
using DockerDesk.Tray.Ui;

namespace DockerDesk.Tray;

/// <summary>
/// The tray icon. Where this tool lives between tasks, because "is Docker up?" should be a glance.
/// </summary>
internal sealed class TrayApplication : ApplicationContext
{
    private readonly NotifyIcon _icon = new();
    private readonly ToolStripMenuItem _start = new("&Start engine");
    private readonly ToolStripMenuItem _stop = new("Sto&p engine");
    private readonly ToolStripMenuItem _window = new("&Open window");
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

        // Short on purpose. A context menu that grows into a second UI is how a tray app stops
        // being glanceable; everything else belongs in the window.
        var menu = new ContextMenuStrip();
        _start.Click += (_, _) => StartEngine();
        _stop.Click += (_, _) => StopEngine();
        _window.Click += (_, _) => OpenWindow();
        menu.Items.Add(_start);
        menu.Items.Add(_stop);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_window);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("&Quit", null, (_, _) => Quit()));

        _icon.ContextMenuStrip = menu;
        _icon.Visible = true;
        Show(EngineState.Stopped);

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
    private void OpenWindow()
    {
        if (_open is not null)
        {
            _open.Activate();
            return;
        }

        // WPF needs an Application before a Window can resolve theme resources, and a WinForms
        // process has none. One is enough, and it must not shut this process down when the window
        // closes — the tray outlives the window.
        if (System.Windows.Application.Current is null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
            };
        }

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
        _icon.BalloonTipTitle = "DockerDesk";
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
        _start.Enabled = state is not EngineState.Running;
        _stop.Enabled = state is not EngineState.Stopped;

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
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplication(openWindow: route.OpenWindow));
            return 0;
        }

        // Every remaining surface writes, so the console comes first: attaching after something has
        // already printed means the first lines went to Stream.Null.
        Cli.ParentConsole.Attach();

        switch (route.Surface)
        {
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
