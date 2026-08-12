using DockerDesk.Core.Api;
using DockerDesk.Tray.Ui;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// What surrounds the four endpoints, tested without a window: which buttons a row offers, what a
/// wait looks like, what the question says, and where a failure lands.
/// </summary>
public sealed class ContainerActionTests
{
    private static ContainerRow Row(string state, string name = "web") =>
        ContainerRow.From(new ContainerSummary
        {
            Id = "a1b2c3d4e5f60718293a",
            Names = [$"/{name}"],
            State = state,
        });

    // ---- which buttons a row offers -------------------------------------------------------------

    [Fact]
    public void A_running_row_offers_stop_restart_and_remove_but_not_start()
    {
        var row = Row("running");

        Assert.False(row.CanStart);
        Assert.True(row.CanStop);
        Assert.True(row.CanRestart);
        Assert.True(row.CanRemove);
    }

    [Fact]
    public void An_exited_row_offers_start_and_remove_and_nothing_that_would_be_refused()
    {
        var row = Row("exited");

        Assert.True(row.CanStart);
        Assert.False(row.CanStop);
        Assert.False(row.CanRestart);
        Assert.True(row.CanRemove);
    }

    [Theory]
    [InlineData("paused")]
    [InlineData("restarting")]
    public void A_container_that_is_up_but_not_running_is_not_offered_start(string state)
    {
        // Neither is "running" and neither is stopped. A Start button here is one whose only
        // possible outcome is a 409 from the daemon.
        var row = Row(state);

        Assert.False(row.CanStart);
        Assert.True(row.CanStop);
    }

    [Fact]
    public void A_row_with_an_action_in_flight_offers_none_of_the_four()
    {
        var row = Row("running") with { Pending = "Stopping…" };

        Assert.True(row.IsPending);
        Assert.False(row.CanStart);
        Assert.False(row.CanStop);
        Assert.False(row.CanRestart);
        Assert.False(row.CanRemove);
    }

    // ---- the wait --------------------------------------------------------------------------------

    [Theory]
    [InlineData(ContainerVerb.Start, "Starting")]
    [InlineData(ContainerVerb.Stop, "Stopping")]
    [InlineData(ContainerVerb.Restart, "Restarting")]
    [InlineData(ContainerVerb.Remove, "Removing")]
    public void Every_verb_has_a_word_the_row_shows_while_it_waits(ContainerVerb verb, string word)
    {
        Assert.StartsWith(word, ContainerAction.PendingLabel(verb), StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_goes_pending_the_moment_the_button_is_pressed()
    {
        var activity = new RowActivity();
        var row = Row("running");

        activity.Began(row.Id, ContainerVerb.Stop);

        Assert.Equal("Stopping…", activity.Dress(row).Pending);
    }

    [Fact]
    public void The_event_stream_is_what_ends_the_wait_and_not_the_response()
    {
        // A 204 from /stop says the daemon took the call, not that the container is down. The `die`
        // event says that, and it can be the full ten-second grace period later.
        var activity = new RowActivity();
        var row = Row("running");
        activity.Began(row.Id, ContainerVerb.Stop);

        activity.Settled(row.Id);

        Assert.Null(activity.Dress(row).Pending);
        Assert.False(activity.Dress(row).IsPending);
    }

    [Fact]
    public void A_container_that_left_the_list_stops_being_waited_on()
    {
        var activity = new RowActivity();
        var row = Row("exited");
        activity.Began(row.Id, ContainerVerb.Remove);

        activity.Prune(["some-other-container"]);

        Assert.Null(activity.Dress(row).Pending);
    }

    // ---- failures land where the click was ------------------------------------------------------

    [Fact]
    public void A_refusal_ends_the_wait_and_says_the_engine_s_words_on_the_row()
    {
        var activity = new RowActivity();
        var row = Row("exited");
        activity.Began(row.Id, ContainerVerb.Start);

        activity.Failed(row.Id, "Bind for 0.0.0.0:8080 failed: port is already allocated");

        var dressed = activity.Dress(row);
        Assert.Null(dressed.Pending);
        Assert.True(dressed.HasFailure);
        Assert.Contains("port is already allocated", dressed.Failure!, StringComparison.Ordinal);
    }

    [Fact]
    public void Pressing_a_button_again_clears_the_failure_that_was_showing()
    {
        var activity = new RowActivity();
        var row = Row("exited");
        activity.Failed(row.Id, "port is already allocated");

        activity.Began(row.Id, ContainerVerb.Start);

        Assert.False(activity.Dress(row).HasFailure);
    }

    [Fact]
    public void A_failure_belongs_to_one_row_and_not_to_the_list()
    {
        var activity = new RowActivity();
        var other = ContainerRow.From(new ContainerSummary { Id = "ff00112233445566", State = "exited" });
        activity.Failed(Row("exited").Id, "port is already allocated");

        Assert.False(activity.Dress(other).HasFailure);
    }

    // ---- the question --------------------------------------------------------------------------

    [Fact]
    public void Removing_a_running_container_asks_first_because_that_is_the_kill()
    {
        Assert.True(ContainerAction.AsksBeforeRemoving(Row("running")));
    }

    [Fact]
    public void Removing_a_stopped_container_does_not_ask_because_it_is_cheap_to_recreate()
    {
        Assert.False(ContainerAction.AsksBeforeRemoving(Row("exited")));
        Assert.False(ContainerAction.AsksBeforeRemoving(Row("created")));
    }

    [Fact]
    public void The_question_names_the_container_and_says_what_is_lost()
    {
        // "Are you sure?" is a question nobody has the information to answer.
        var prompt = ContainerAction.RemovalPrompt(Row("running", "hopeful_wozniak"));

        Assert.Contains("hopeful_wozniak", prompt, StringComparison.Ordinal);
        Assert.Contains("killed", prompt, StringComparison.Ordinal);
        Assert.Contains("volume", prompt, StringComparison.Ordinal);
    }
}
