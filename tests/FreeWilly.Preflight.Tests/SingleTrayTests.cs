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
    /// Stand aside where the product itself is holding the object these tests claim (DD103).
    /// </summary>
    /// <remarks>
    /// The consequence of claiming the real names, which is right and which nobody wrote down: the
    /// suite cannot be run on a machine where the product is running, and that is every machine
    /// that uses it. Three tests failed and none of them said so —
    /// <see cref="The_first_claim_wins_and_the_second_is_told_to_step_aside"/> reported that a claim
    /// succeeded when it should not have, and the cause was a tray in the notification area left
    /// over from a smoke test.
    ///
    /// <para><b>It fails rather than skipping, and that was a decision.</b> The task offered both,
    /// and skipping is the friendlier of the two on a developer's machine — but a skipped test is
    /// one nobody reads, and xUnit v2 has no supported way to ask for one anyway: <c>Assert.Skip</c>
    /// and <c>SkipException</c> are v3, and reaching for the dynamic-skip token by hand would be a
    /// magic string that stops working silently. So the failure stays and the message becomes the
    /// remedy, which is the half that was actually missing.</para>
    ///
    /// <para>The mutex is unprefixed and therefore session-local, so this is never about another
    /// user's tray. <c>TryClaim</c> answering false is the whole detection; what was missing was
    /// reading it before the assertions rather than through them.</para>
    /// </remarks>
    private static void RequireTheTraySlot()
    {
        if (SingleTray.TryClaim(out var probe))
        {
            probe!.Dispose();
            return;
        }

        // Named as an unmade assertion rather than a wrong one. Reported as a failure this reads
        // "FreeWilly is running", which is the sentence the three failures did not say.
        Assert.Fail(
            $"FreeWilly is running on this session and holds {SingleTray.Name}, which is the very "
            + "object these tests claim — so nothing below was actually asserted. Quit it from the "
            + "tray and re-run.");
    }

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
    public void A_running_tray_is_named_by_the_failure_rather_than_left_to_an_assertion()
    {
        // DD103 itself, asserted. Before this the reader of a red suite was told that a claim
        // succeeded when it should not have, and the cause — a tray in the notification area, left
        // over from a smoke test — appeared in no message. What is worth holding is not that it
        // fails but what it says while failing, because that sentence is the remedy.
        RequireTheTraySlot();

        using var taken = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        // From another thread and held there: a mutex is owned by a thread and is reentrant, so a
        // claim made on this one would let the probe straight through and test nothing.
        var holder = new Thread(() =>
        {
            var mine = SingleTray.TryClaim(out var claim);
            taken.Set();
            release.Wait();
            if (mine)
            {
                claim!.Dispose();
            }
        });

        holder.Start();
        Assert.True(taken.Wait(TimeSpan.FromSeconds(5)), "the stand-in tray never claimed the slot");

        var failure = Record.Exception(RequireTheTraySlot);

        release.Set();
        holder.Join();

        Assert.NotNull(failure);
        Assert.Contains("FreeWilly is running", failure.Message, StringComparison.Ordinal);
        Assert.Contains(SingleTray.Name, failure.Message, StringComparison.Ordinal);
        Assert.Contains("Quit it from the tray", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_first_claim_wins_and_the_second_is_told_to_step_aside()
    {
        RequireTheTraySlot();

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
        RequireTheTraySlot();

        // Quitting the tray has to leave a machine able to start one, or the fix would be worse
        // than the defect.
        Assert.True(SingleTray.TryClaim(out var first));
        first!.Dispose();

        Assert.True(ClaimedElsewhere());
    }

    [Fact]
    public void A_second_launch_raises_the_live_one()
    {
        RequireTheTraySlot();

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
    public void The_quit_signal_reaches_the_live_one_and_is_not_the_raise()
    {
        RequireTheTraySlot();

        Assert.True(SingleTray.TryClaim(out var only));
        using (only)
        {
            using var asked = new ManualResetEventSlim(false);
            using var raised = new ManualResetEventSlim(false);
            only!.OnRaise(() => raised.Set());
            only.OnQuit(() => asked.Set());

            Assert.True(SingleTray.AskTheLiveOneToQuit());

            Assert.True(
                asked.Wait(TimeSpan.FromSeconds(5)),
                "the live instance was never asked to close");

            // Two named objects rather than one, and this is why: an auto-reset event carries no
            // payload, so a single handle would make "show yourself" and "close yourself" the same
            // signal — and the uninstaller would put a window on screen on its way to deleting it.
            Assert.False(raised.IsSet, "asking the tray to quit also asked it to show its window");
        }
    }

    [Fact]
    public void Asking_a_machine_with_no_tray_to_quit_is_not_a_failure()
    {
        // What the uninstaller runs on a machine where nobody ever opened the tray. It asked for a
        // machine with no tray on it and that is what it has, so `--quit` reports success — exit 1
        // is kept for the one answer the uninstaller has to act on, which is a tray that stayed.
        RequireTheTraySlot();

        Assert.False(SingleTray.AskTheLiveOneToQuit());
    }

    [Fact]
    public void The_wait_answers_yes_only_once_the_slot_is_actually_free()
    {
        // The half that makes the verb usable from an uninstaller. The signal only says the request
        // was delivered; what has to be true before a delete is attempted is that the process is
        // gone, and the slot is released on the way out — so the mutex is what is watched.
        RequireTheTraySlot();

        using var taken = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        // Held from another thread, because a mutex is owned by a thread and is reentrant: a claim
        // made on this one would let the wait straight through and assert nothing.
        var holder = new Thread(() =>
        {
            var mine = SingleTray.TryClaim(out var claim);
            taken.Set();
            release.Wait();
            if (mine)
            {
                claim!.Dispose();
            }
        });

        holder.Start();
        Assert.True(taken.Wait(TimeSpan.FromSeconds(5)), "the stand-in tray never claimed the slot");

        // Short, because this asserts that it waits rather than how long it is willing to.
        Assert.False(
            SingleTray.WaitUntilTheTrayIsGone(TimeSpan.FromMilliseconds(300)),
            "the wait reported the tray gone while something still held the slot");

        release.Set();
        holder.Join();

        Assert.True(
            SingleTray.WaitUntilTheTrayIsGone(TimeSpan.FromSeconds(5)),
            "the wait never noticed the slot come free");
    }

    [Fact]
    public void Quitting_when_nothing_holds_the_tray_leaves_the_slot_claimable()
    {
        // The probe the wait makes has to give the slot straight back, or a tray relaunched in the
        // same second would find it taken by something that only wanted to look.
        RequireTheTraySlot();

        Assert.True(SingleTray.WaitUntilTheTrayIsGone(TimeSpan.FromSeconds(1)));
        Assert.True(ClaimedElsewhere());
    }

    [Fact]
    public void Raising_when_nothing_holds_the_tray_is_silent()
    {
        // The fourth, and it did not fail — which is worse. Its premise is in its name, and with a
        // tray running the premise is false: the call below reaches a live instance, asserts nothing
        // about the silence it claims to test, and puts that instance's window on screen in the
        // middle of a test run.
        RequireTheTraySlot();

        // A launch that found nothing to signal has nothing useful left to do, and throwing at
        // somebody who double-clicked would be worse than the silence being fixed.
        var exception = Record.Exception(SingleTray.RaiseTheLiveOne);

        Assert.Null(exception);
    }
}
