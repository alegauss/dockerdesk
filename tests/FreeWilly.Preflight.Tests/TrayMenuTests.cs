using FreeWilly.Core.Engine;
using FreeWilly.Tray;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The tray's context menu, which is now photographable and therefore assertable (DD67).
/// </summary>
/// <remarks>
/// These exist because the menu is built in one place for one reason: <c>--show-menu</c> shows the
/// same menu the tray wears, and a second one built for the camera would be a picture of a menu
/// nobody ships. So what is asserted here is the shape a capture is a picture of.
///
/// <para>On an STA thread, because a <c>ContextMenuStrip</c> is a control and WinForms refuses one
/// off an MTA thread — xUnit's are MTA.</para>
/// </remarks>
public sealed class TrayMenuTests
{
    /// <summary>Run a body on a thread WinForms will talk to, and surface whatever it threw.</summary>
    private static void OnUiThread(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException("the menu failed on its own thread", failure);
        }
    }

    private static TrayMenu Menu() => new(Nothing, Nothing, Nothing, Nothing);

    private static void Nothing()
    {
    }

    [Fact]
    public void The_menu_is_five_items_and_two_rules_in_the_order_a_photograph_shows_them() =>
        OnUiThread(() =>
        {
            // Short on purpose, and asserted so it stays short: a context menu that grows into a
            // second UI is how a tray app stops being glanceable. The order is part of the claim
            // because a photograph is a picture of the order.
            //
            // DD135 spent one item here, and it is placed with the two engine verbs rather than off
            // with the window because it qualifies them: what the engine does when nobody is asking.
            using var menu = Menu().Strip;

            Assert.Equal(7, menu.Items.Count);
            Assert.Equal(TrayMenu.StartText, menu.Items[0].Text);
            Assert.Equal(TrayMenu.StopText, menu.Items[1].Text);
            Assert.Equal(TrayMenu.OnLaunchText, menu.Items[2].Text);
            Assert.IsType<ToolStripSeparator>(menu.Items[3]);
            Assert.Equal(TrayMenu.WindowText, menu.Items[4].Text);
            Assert.IsType<ToolStripSeparator>(menu.Items[5]);
            Assert.Equal(TrayMenu.QuitText, menu.Items[6].Text);
        });

    [Fact]
    public void The_setting_opens_showing_what_is_actually_true() =>
        OnUiThread(() =>
        {
            // A box that always opened ticked would be a picture of the default rather than of the
            // user's answer, and the one thing this setting has to do is survive being turned off.
            using var off = new TrayMenu(Nothing, Nothing, Nothing, Nothing, _ => { }, false).Strip;
            using var on = new TrayMenu(Nothing, Nothing, Nothing, Nothing, _ => { }, true).Strip;

            Assert.False(((ToolStripMenuItem)off.Items[2]).Checked);
            Assert.True(((ToolStripMenuItem)on.Items[2]).Checked);
        });

    [Fact]
    public void Ticking_the_setting_reports_the_state_the_user_is_looking_at() =>
        OnUiThread(() =>
        {
            // CheckOnClick flips the tick before the handler runs, so what is reported is the new
            // answer and nothing has to negate anything — a setting written from what is on screen
            // cannot disagree with what is on screen.
            var told = new List<bool>();
            var menu = new TrayMenu(Nothing, Nothing, Nothing, Nothing, told.Add, true);
            using var strip = menu.Strip;

            ((ToolStripMenuItem)strip.Items[2]).PerformClick();
            ((ToolStripMenuItem)strip.Items[2]).PerformClick();

            Assert.Equal([false, true], told);
        });

    [Fact]
    public void The_setting_ticks_with_nothing_behind_it_so_the_camera_still_reaches_the_menu() =>
        OnUiThread(() =>
        {
            // L6 again. `--show-menu` builds this with no tray under it, and an item that needed a
            // live setting to exist would be an item no capture could photograph.
            using var strip = Menu().Strip;

            ((ToolStripMenuItem)strip.Items[2]).PerformClick();

            Assert.False(((ToolStripMenuItem)strip.Items[2]).Checked);
        });

    [Theory]
    [InlineData(EngineState.Stopped, true, false)]
    [InlineData(EngineState.Starting, true, true)]
    [InlineData(EngineState.Running, false, true)]
    public void What_can_be_asked_of_the_engine_is_what_the_menu_offers(
        EngineState state, bool canStart, bool canStop) =>
        OnUiThread(() =>
        {
            // Two of the three states differ here by one item's enabled flag and by nothing else,
            // which is exactly the difference a capture of each state is for.
            var menu = Menu();
            menu.Reflect(state);

            using var strip = menu.Strip;
            Assert.Equal(canStart, strip.Items[0].Enabled);
            Assert.Equal(canStop, strip.Items[1].Enabled);
        });

    [Fact]
    public void It_builds_with_nothing_to_do_which_is_what_lets_a_capture_reach_it() =>
        OnUiThread(() =>
        {
            // L6, and the whole of why the popup is reachable at all: the preview shows this menu
            // with no engine, no icon and no window behind it. A menu that needed a live tray to
            // exist could only be photographed on a machine that already had one.
            using var strip = Menu().Strip;
            Assert.NotNull(strip);
        });

    [Fact]
    public void An_item_with_nothing_behind_it_is_a_defect_here_rather_than_a_dead_click() =>
        OnUiThread(() =>
        {
            // A null passed for one of these used to be a menu entry that silently did nothing,
            // which is indistinguishable from a broken engine. The setting added in DD135 is the
            // deliberate exception and is checked nowhere here: a box that ticks and tells nobody
            // is a photograph, not a dead click, and it is what `--show-menu` builds.
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(null!, Nothing, Nothing, Nothing));
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(Nothing, null!, Nothing, Nothing));
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(Nothing, Nothing, null!, Nothing));
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(Nothing, Nothing, Nothing, null!));
        });
}
