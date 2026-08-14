using FreeWilly.Tray.Cli;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// One tray per session, and what a second launch does instead of starting another (DD81).
/// </summary>
/// <remarks>
/// These claim the real named objects, so they run in the console collection to keep them off the
/// same instant as anything else — the names are the product's and there is nothing to parameterise
/// without testing a different thing from the one that ships.
/// </remarks>
[Collection(ConsoleCollection.Name)]
public sealed class SingleTrayTests
{
    /// <summary>
    /// Try to claim from somewhere that is not this thread, which is what a second launch is.
    /// </summary>
    /// <remarks>
    /// A mutex is owned by a thread and is reentrant, so asking twice on one thread succeeds twice
    /// — which is not the question. The second launch is another process, and another thread is the
    /// nearest thing a test can be.
    /// </remarks>
    private static bool ClaimedElsewhere()
    {
        var got = false;
        var thread = new Thread(() =>
        {
            if (SingleTray.TryClaim(out var claim))
            {
                got = true;
                claim!.Dispose();
            }
        });

        thread.Start();
        thread.Join();
        return got;
    }

    [Fact]
    public void The_first_claim_wins_and_the_second_is_told_to_step_aside()
    {
        Assert.True(SingleTray.TryClaim(out var first));
        using (first)
        {
            // The failure this removes: every extra click used to be another process, another icon
            // in the overflow and another event stream open on one daemon.
            Assert.False(ClaimedElsewhere());
        }
    }

    [Fact]
    public void The_slot_is_free_again_once_the_tray_lets_it_go()
    {
        // Quitting the tray has to leave a machine able to start one, or the fix would be worse
        // than the defect.
        Assert.True(SingleTray.TryClaim(out var first));
        first!.Dispose();

        Assert.True(ClaimedElsewhere());
    }

    [Fact]
    public void A_second_launch_raises_the_live_one()
    {
        Assert.True(SingleTray.TryClaim(out var only));
        using (only)
        {
            using var raised = new ManualResetEventSlim(false);
            only!.OnRaise(() => raised.Set());

            SingleTray.RaiseTheLiveOne();

            Assert.True(
                raised.Wait(TimeSpan.FromSeconds(5)),
                "the live instance was never asked to show its window");
        }
    }

    [Fact]
    public void Raising_when_nothing_holds_the_tray_is_silent()
    {
        // A launch that found nothing to signal has nothing useful left to do, and throwing at
        // somebody who double-clicked would be worse than the silence being fixed.
        var exception = Record.Exception(SingleTray.RaiseTheLiveOne);

        Assert.Null(exception);
    }
}
