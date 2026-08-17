using FreeWilly.Core.Engine;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// Whether a lost engine gets another attempt, and how long the host waits first (DD136).
/// </summary>
public sealed class EngineRevivalTests
{
    private static EngineStatus Gone() =>
        new(EngineState.Stopped, "the daemon exited") { Conclusive = true };

    [Fact]
    public void A_fresh_host_owes_the_engine_an_attempt()
    {
        var revival = new EngineRevival();

        Assert.True(revival.WorthAnotherTry);
        Assert.Equal(0, revival.Failures);
        Assert.Equal(0, revival.Revivals);
    }

    [Fact]
    public void The_wait_grows_with_each_failure_rather_than_hammering_a_busy_machine()
    {
        // A machine still settling after a resume is made slower by being asked four times a second
        // for the thing it is busy doing.
        var revival = new EngineRevival();
        var waits = new List<TimeSpan>();

        for (var i = 0; i < EngineRevival.Attempts; i++)
        {
            waits.Add(revival.Wait);
            revival.Failed();
        }

        Assert.Equal(EngineRevival.FirstWait, waits[0]);
        for (var i = 1; i < waits.Count; i++)
        {
            Assert.True(
                waits[i] >= waits[i - 1],
                $"the wait shrank from {waits[i - 1]} to {waits[i]} at attempt {i}");
        }
    }

    [Fact]
    public void The_wait_is_capped_so_a_fixed_machine_is_not_left_waiting_minutes()
    {
        // The back-off exists to stop hammering a busy machine, not to punish a slow one: a user who
        // repaired whatever was wrong should not sit in front of a working machine watching nothing.
        var revival = new EngineRevival();

        for (var i = 0; i < 40; i++)
        {
            revival.Failed();
            Assert.True(
                revival.Wait <= EngineRevival.LongestWait,
                $"the wait reached {revival.Wait}, past the {EngineRevival.LongestWait} cap");
        }

        Assert.True(revival.Wait > TimeSpan.Zero, "the wait overflowed into nothing");
    }

    [Fact]
    public void Running_out_of_attempts_ends_it_rather_than_retrying_forever()
    {
        // An engine that cannot come up — a corrupted distribution, a full disk, a pipe another
        // daemon has taken — is a fact the user needs. A loop that hides it behind another retry
        // turns that fact into a machine quietly doing nothing.
        var revival = new EngineRevival();

        for (var i = 0; i < EngineRevival.Attempts; i++)
        {
            Assert.True(revival.WorthAnotherTry, $"gave up early, at attempt {i}");
            revival.Failed();
        }

        Assert.False(revival.WorthAnotherTry);
    }

    [Fact]
    public void An_engine_that_came_back_gets_the_full_budget_again()
    {
        // The failures counted are the consecutive ones. A laptop suspended twice a day for a week
        // must not run out of attempts on the Friday because of what happened on the Monday.
        var revival = new EngineRevival();
        for (var i = 0; i < EngineRevival.Attempts - 1; i++)
        {
            revival.Failed();
        }

        revival.Revived();

        Assert.True(revival.WorthAnotherTry);
        Assert.Equal(0, revival.Failures);
        Assert.Equal(EngineRevival.FirstWait, revival.Wait);
        Assert.Equal(1, revival.Revivals);
    }

    [Fact]
    public void Giving_up_says_how_many_times_it_tried()
    {
        // A host that came down after five attempts and one that came down without trying at all are
        // different machines to be sitting in front of, and the detail alone does not tell them apart.
        var revival = new EngineRevival();
        while (revival.WorthAnotherTry)
        {
            revival.Failed();
        }

        var said = revival.WhyItGaveUp(Gone());

        Assert.Contains($"{EngineRevival.Attempts} attempts", said, StringComparison.Ordinal);
        Assert.Contains("the daemon exited", said, StringComparison.Ordinal);
    }

    [Fact]
    public void The_whole_budget_covers_a_resume_without_outlasting_a_users_patience()
    {
        // The two numbers the shape has to satisfy at once. Too short and a laptop that takes its
        // time coming back is declared dead; too long and the tray sits amber while somebody waits.
        var revival = new EngineRevival();
        var total = TimeSpan.Zero;
        while (revival.WorthAnotherTry)
        {
            total += revival.Wait;
            revival.Failed();
        }

        Assert.True(total >= TimeSpan.FromSeconds(30), $"only {total} spent before giving up");
        Assert.True(total <= TimeSpan.FromMinutes(3), $"{total} is longer than anybody waits");
    }
}
