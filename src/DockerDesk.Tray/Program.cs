using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;

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

    internal TrayApplication()
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _holder = new EngineHolder(EngineHolder.BesideThisProcess(), new DetachedLauncher());

        // Short on purpose. A context menu that grows into a second UI is how a tray app stops
        // being glanceable; everything else belongs in the window.
        var menu = new ContextMenuStrip();
        _start.Click += (_, _) => StartEngine();
        _stop.Click += (_, _) => StopEngine();
        _window.Enabled = false;
        _window.ToolTipText = "The window is not built yet.";
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
        _events.Start();
    }

    private void StartEngine()
    {
        _startRequested = true;
        Show(EngineState.Starting);
        _holder.Start();
    }

    private void StopEngine()
    {
        _startRequested = false;
        Show(EngineState.Stopped);
        _holder.Stop();
    }

    private void Show(EngineState state)
    {
        if (state is EngineState.Running)
        {
            _startRequested = false;
        }

        var next = StateIcon.Icon(state);
        _icon.Icon = next;

        // The previous icon owns an unmanaged handle from GetHicon; replacing it without destroying
        // it leaks one per state change, and this changes state whenever the engine does.
        _worn?.Dispose();
        _worn = next;

        _icon.Text = StateIcon.TooltipFor(state);
        _start.Enabled = state is not EngineState.Running;
        _stop.Enabled = state is not EngineState.Stopped;
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

/// <summary>The entry point.</summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplication());
    }
}
