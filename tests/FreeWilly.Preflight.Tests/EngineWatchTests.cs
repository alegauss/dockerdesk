using FreeWilly.Core.Engine;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// A run of quiet polls and not one of them is what stops the engine host (DD133).
/// </summary>
public sealed class EngineWatchTests
{
    private static EngineStatus Running() =>
        new(EngineState.Running, @"the engine answered on \\.\pipe\docker_engine", "1.44");

    private static EngineStatus Quiet() =>
        new(EngineState.Starting, "the daemon is running and no answer within 3s");

    [Fact]
    public void One_missed_poll_does_not_take_the_engine_down()
    {
        // The whole of DD133. Before this, the single Starting below was enough to dispose the
        // relay and terminate the distribution — mid-build, against a daemon that was fine and had
        // only lost a race for process creation with the build's own wsl.exe children.
        var watch = new EngineWatch();

        Assert.True(watch.KeepServing(Quiet()));
        Assert.Equal(1, watch.QuietPolls);
    }

    [Fact]
    public void An_answer_clears_the_run_of_silence()
    {
        // The engine proved itself by replying, so what came before it is not evidence of anything.
        // Without this a host that was merely busy would accumulate quiet polls across a whole day
        // and come down on the sixth, hours apart, for no reason a reader could reconstruct.
        var watch = new EngineWatch();

        for (var i = 0; i < EngineWatch.ToleratedQuietPolls - 1; i++)
        {
            Assert.True(watch.KeepServing(Quiet()));
        }

        Assert.True(watch.KeepServing(Running()));
        Assert.Equal(0, watch.QuietPolls);

        Assert.True(watch.KeepServing(Quiet()));
    }

    [Fact]
    public void Unbroken_silence_still_ends_the_watch()
    {
        // The tolerance is not a refusal to ever come down. A `--stop` from another process, or a
        // distribution terminated by hand, has to bring the pipe down with it — a relay left serving
        // nothing is the defect the poll loop was added for in the first place.
        var watch = new EngineWatch();

        for (var i = 0; i < EngineWatch.ToleratedQuietPolls - 1; i++)
        {
            Assert.True(watch.KeepServing(Quiet()));
        }

        Assert.False(watch.KeepServing(Quiet()));
        Assert.Equal(EngineWatch.ToleratedQuietPolls, watch.QuietPolls);
    }

    [Fact]
    public void A_stopped_answer_is_tolerated_exactly_like_a_quiet_one()
    {
        // Stopped reads like the certain one — the daemon is gone — but the status arrives there
        // through `wsl --list`, which on a saturated machine is as capable of being slow as the ping
        // was. No non-Running answer has a cause that cannot be load, so none is acted on alone.
        var watch = new EngineWatch();
        var stopped = new EngineStatus(EngineState.Stopped, "the daemon is not running");

        Assert.True(watch.KeepServing(stopped));
        Assert.Equal(1, watch.QuietPolls);
    }

    [Fact]
    public void The_line_it_prints_says_how_many_times_the_engine_was_asked()
    {
        // Without the count this is the line --run printed before DD133, and that line was
        // indistinguishable from the false alarm it usually was.
        var watch = new EngineWatch();
        var last = Quiet();
        while (watch.KeepServing(last))
        {
        }

        var said = watch.WhyItStopped(last);

        Assert.Contains($"{EngineWatch.ToleratedQuietPolls} polls in a row", said, StringComparison.Ordinal);
        Assert.Contains(last.Detail, said, StringComparison.Ordinal);
    }

    [Fact]
    public void The_tolerance_outlasts_the_window_a_caller_was_promised()
    {
        // DD133 asked whether "is the engine ready" could be an answer that survives the next thirty
        // seconds. At the two seconds between polls and the three the ping is given, the tolerance
        // has to reach that far or the question is still open.
        var worstPoll = TimeSpan.FromSeconds(2) + TimeSpan.FromSeconds(3);

        Assert.True(
            worstPoll * EngineWatch.ToleratedQuietPolls >= TimeSpan.FromSeconds(30),
            "the run of quiet polls is shorter than the thirty seconds DD133 named");
    }
}
