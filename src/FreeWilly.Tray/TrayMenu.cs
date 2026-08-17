using FreeWilly.Core.Engine;

namespace FreeWilly.Tray;

/// <summary>
/// The tray's context menu, built in one place so the thing photographed is the thing shipped.
/// </summary>
/// <remarks>
/// DD67. This was six lines inside <c>TrayApplication</c>'s constructor, which is fine until
/// something other than the tray needs the same menu — and something does: no popup this product
/// draws had ever been photographed, because a menu exists only while it is open and nothing opened
/// one. A second menu built for the camera would photograph a menu nobody ships.
///
/// <para><b>It takes actions and knows the engine only as a state</b> (L6). Handed no-ops it still
/// builds, which is what lets <c>--show-menu</c> put one on the screen with no engine, no icon and
/// no window behind it — the same law that lets the window draw without a daemon. The setting added
/// in DD135 keeps that law by being optional: no delegate means a box that ticks and tells
/// nobody.</para>
///
/// <para><b>Short on purpose.</b> A context menu that grows into a second UI is how a tray app stops
/// being glanceable; everything else belongs in the window. DD135 spent one item of that budget, and
/// the reason it is affordable is that it qualifies a verb already here rather than introducing a
/// place to go: the menu is still the two things you can do to the engine, plus how it behaves when
/// you are not asking, plus the window and the way out.</para>
/// </remarks>
internal sealed class TrayMenu
{
    /// <summary>What each item says, in one place, so a test can hold the shipped menu to it.</summary>
    internal const string StartText = "&Start engine";

    /// <inheritdoc cref="StartText"/>
    internal const string StopText = "Sto&p engine";

    /// <inheritdoc cref="StartText"/>
    internal const string OnLaunchText = "Start engine &with FreeWilly";

    /// <inheritdoc cref="StartText"/>
    internal const string WindowText = "&Open window";

    /// <inheritdoc cref="StartText"/>
    internal const string QuitText = "&Quit";

    private readonly ToolStripMenuItem _start = new(StartText);
    private readonly ToolStripMenuItem _stop = new(StopText);
    private readonly ToolStripMenuItem _onLaunch = new(OnLaunchText) { CheckOnClick = true };
    private readonly ToolStripMenuItem _window = new(WindowText);

    /// <summary>Build the menu.</summary>
    /// <param name="startEngine">What the first item does.</param>
    /// <param name="stopEngine">What the second does.</param>
    /// <param name="openWindow">What the fourth does.</param>
    /// <param name="quit">What the last does.</param>
    /// <param name="setOnLaunch">
    /// What the third does, given the box's new state — or <see langword="null"/> where nothing is
    /// behind it, which is how <c>--show-menu</c> photographs a menu with no tray under it (DD135).
    /// </param>
    /// <param name="onLaunch">Whether the box starts ticked.</param>
    internal TrayMenu(
        Action startEngine,
        Action stopEngine,
        Action openWindow,
        Action quit,
        Action<bool>? setOnLaunch = null,
        bool onLaunch = EngineOnLaunch.ShipsOn)
    {
        ArgumentNullException.ThrowIfNull(startEngine);
        ArgumentNullException.ThrowIfNull(stopEngine);
        ArgumentNullException.ThrowIfNull(openWindow);
        ArgumentNullException.ThrowIfNull(quit);

        _start.Click += (_, _) => startEngine();
        _stop.Click += (_, _) => stopEngine();
        _window.Click += (_, _) => openWindow();

        // CheckOnClick flips the tick before this runs, so the item's own state is the new answer
        // and nothing here has to negate anything. A setting written from what the user is looking
        // at cannot disagree with it.
        _onLaunch.Checked = onLaunch;
        _onLaunch.CheckedChanged += (_, _) => setOnLaunch?.Invoke(_onLaunch.Checked);

        Strip = new ContextMenuStrip();
        Strip.Items.Add(_start);
        Strip.Items.Add(_stop);

        // With the two verbs it qualifies rather than off with the window, because it is a third
        // thing to say about the engine and not a third place to go.
        Strip.Items.Add(_onLaunch);
        Strip.Items.Add(new ToolStripSeparator());
        Strip.Items.Add(_window);
        Strip.Items.Add(new ToolStripSeparator());
        Strip.Items.Add(new ToolStripMenuItem(QuitText, null, (_, _) => quit()));
    }

    /// <summary>The menu itself, for whatever is going to show it.</summary>
    internal ContextMenuStrip Strip { get; }

    /// <summary>Say what the engine is doing, by what can be asked of it.</summary>
    /// <param name="state">What the engine is doing.</param>
    internal void Reflect(EngineState state)
    {
        _start.Enabled = state is not EngineState.Running;
        _stop.Enabled = state is not EngineState.Stopped;
    }
}
