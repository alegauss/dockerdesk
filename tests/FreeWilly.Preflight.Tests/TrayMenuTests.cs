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
    public void The_menu_is_four_items_and_two_rules_in_the_order_a_photograph_shows_them() =>
        OnUiThread(() =>
        {
            // Short on purpose, and asserted so it stays short: a context menu that grows into a
            // second UI is how a tray app stops being glanceable. The order is part of the claim
            // because a photograph is a picture of the order.
            using var menu = Menu().Strip;

            Assert.Equal(6, menu.Items.Count);
            Assert.Equal(TrayMenu.StartText, menu.Items[0].Text);
            Assert.Equal(TrayMenu.StopText, menu.Items[1].Text);
            Assert.IsType<ToolStripSeparator>(menu.Items[2]);
            Assert.Equal(TrayMenu.WindowText, menu.Items[3].Text);
            Assert.IsType<ToolStripSeparator>(menu.Items[4]);
            Assert.Equal(TrayMenu.QuitText, menu.Items[5].Text);
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
            // Four actions, four items. A null passed for one of them used to be a menu entry that
            // silently did nothing, which is indistinguishable from a broken engine.
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(null!, Nothing, Nothing, Nothing));
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(Nothing, null!, Nothing, Nothing));
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(Nothing, Nothing, null!, Nothing));
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(Nothing, Nothing, Nothing, null!));
        });
}
