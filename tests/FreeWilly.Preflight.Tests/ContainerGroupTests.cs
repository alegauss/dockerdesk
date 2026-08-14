using FreeWilly.Core.Agent;
using FreeWilly.Core.Api;
using FreeWilly.Tray.Ui;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// A compose project as one thing rather than as four peer rows (DD106).
/// </summary>
/// <remarks>
/// The label that names the project is already on the list response, so what is under test is the
/// shaping and never a second call to the daemon. The hard parts are the three the design named: the
/// header's own key, the sort running twice, and a filter that must not leave a header with nothing
/// under it.
/// </remarks>
public sealed class ContainerGroupTests
{
    private static readonly ListShape ByState = new(ContainerRow.DefaultColumn, Descending: false);

    private static ContainerSummary Summary(
        string name, string state = "running", string? project = null, string image = "img:1") =>
        new()
        {
            Id = name + "-id",
            Names = ["/" + name],
            Image = image,
            State = state,
            Status = state == "running" ? "Up 4 minutes" : "Exited (0) 3 minutes ago",
            Ports = [],
            Labels = project is null
                ? null
                : new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ContextPack.ProjectLabel] = project,
                },
        };

    private static IReadOnlyList<ContainerRow> Rows(params ContainerSummary[] containers) =>
        [.. containers.Select(ContainerRow.From)];

    private static IReadOnlyList<ContainerRow> Grouped(
        IReadOnlyList<ContainerRow> rows, ListShape? shape = null, params string[] collapsed) =>
        ContainerRow.Grouped(rows, shape ?? ByState, collapsed.ToHashSet(StringComparer.Ordinal));

    // ---- the label is already there ------------------------------------------------------------

    [Fact]
    public void A_container_carries_the_project_its_label_names()
    {
        var row = ContainerRow.From(Summary("shop-api-1", project: "shop"));

        Assert.Equal("shop", row.Project);
        Assert.True(row.IsContainer);
        Assert.False(row.IsProject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_container_with_no_project_label_stays_a_top_level_row(string? project)
    {
        // Blank is absent. A label present and empty names no project, and a group headed by the
        // empty string is a group nobody can read — so it must not become one.
        var summary = Summary("lonely");
        if (project is not null)
        {
            summary = summary with
            {
                Labels = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ContextPack.ProjectLabel] = project,
                },
            };
        }

        var row = ContainerRow.From(summary);

        Assert.Null(row.Project);
        Assert.Equal(default, row.Indent);
    }

    // ---- the header ------------------------------------------------------------------------------

    [Fact]
    public void A_project_becomes_one_header_over_its_containers()
    {
        var shown = Grouped(Rows(
            Summary("shop-api-1", project: "shop"),
            Summary("shop-db-1", project: "shop"),
            Summary("shop-worker-1", "exited", "shop")));

        var header = shown[0];
        Assert.True(header.IsProject);
        Assert.Equal("shop", header.Name);
        Assert.Equal("2 of 3 running", header.ProjectCount);
        Assert.Equal(3, shown.Count(row => row.IsContainer));
        Assert.All(shown.Where(row => row.IsContainer), row => Assert.NotEqual(default, row.Indent));
    }

    [Fact]
    public void The_header_is_keyed_apart_from_every_container_so_the_fade_works_on_projects()
    {
        // LiveRows reconciles by id (DD70), so a header needs one of its own or a project arriving
        // is a row that appears with no fade — and worse, could collide with a container's id.
        var shown = Grouped(Rows(Summary("shop-api-1", project: "shop")));

        Assert.Equal("compose:shop", shown[0].Id);
        Assert.Equal(ContainerRow.ProjectId("shop"), shown[0].Id);
        Assert.Distinct(shown.Select(row => row.Id));
    }

    [Fact]
    public void A_header_answers_nothing_it_has_no_answer_for()
    {
        // The row is one template with a trigger, so the columns a project cannot fill must read as
        // blank rather than as a container with no image.
        var header = Grouped(Rows(Summary("shop-api-1", project: "shop")))[0];

        Assert.Equal("", header.Image);
        Assert.Equal("", header.State);
        Assert.Empty(header.Ports);
        Assert.Equal(default, header.Indent);

        // A shell is the one verb a project genuinely has none of, and a disabled control that says
        // why is a limitation while one that does not is a bug the user is left to guess at.
        Assert.False(header.CanShell);
        Assert.Contains("no shell", header.ShellReason, StringComparison.Ordinal);
    }

    // ---- one verb, the whole project (DD107) ---------------------------------------------------

    [Fact]
    public void A_project_with_something_up_is_opened_to_be_stopped()
    {
        var header = Grouped(Rows(
            Summary("shop-api-1", project: "shop"),
            Summary("shop-old-1", "exited", "shop")))[0];

        // Partly up offers Stop, because the button that finishes the job is the useful one.
        Assert.True(header.AnyUp);
        Assert.True(header.HasPrimary);
        Assert.Equal("Stop", header.PrimaryVerb);
        Assert.True(header.CanRestart);
    }

    [Fact]
    public void A_project_that_is_wholly_down_is_opened_to_be_started()
    {
        var header = Grouped(Rows(
            Summary("shop-api-1", "exited", "shop"),
            Summary("shop-db-1", "exited", "shop")))[0];

        Assert.False(header.AnyUp);
        Assert.Equal("Start", header.PrimaryVerb);
        Assert.False(header.CanStop);
        Assert.False(header.CanRestart);
    }

    [Fact]
    public void Removing_a_project_always_asks_and_the_question_is_about_the_count()
    {
        // The thing being agreed to is the number. A dialog naming one service while removing four
        // would be technically true and actively misleading — and the reasoning that lets a stopped
        // container go without a dialog does not survive being multiplied.
        var header = Grouped(Rows(
            Summary("shop-api-1", "exited", "shop"),
            Summary("shop-db-1", "exited", "shop"),
            Summary("shop-worker-1", "exited", "shop")))[0];

        Assert.True(ContainerAction.AsksBeforeRemoving(header));

        var prompt = ContainerAction.RemovalPrompt(header);
        Assert.Contains("All 3 containers", prompt, StringComparison.Ordinal);
        Assert.Contains("shop", prompt, StringComparison.Ordinal);

        // Volumes stay. `compose down -v` is a different act and a Remove button must not quietly
        // mean it.
        Assert.Contains("volumes stay", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_of_one_container_is_asked_about_in_the_singular() =>
        Assert.Contains(
            "All 1 container go",
            ContainerAction.RemovalPrompt(Grouped(Rows(Summary("only-1", project: "shop")))[0]),
            StringComparison.Ordinal);

    // ---- the wait a header borrows from its containers (DD108) ---------------------------------

    [Fact]
    public void A_header_waits_for_as_long_as_something_under_it_is_waiting()
    {
        // DD8's rule: a 204 from /stop means the daemon took the call, not that the container is
        // down. DD107 ended the header's wait when the last call was answered, so it stopped saying
        // Stopping… while every row under it went on saying it for the daemon's grace period.
        var activity = new RowActivity();
        var rows = Rows(
            Summary("shop-api-1", project: "shop"),
            Summary("shop-db-1", project: "shop"));

        activity.Began("shop-api-1-id", ContainerVerb.Stop);
        var dressed = rows.Select(activity.Dress).ToList();
        var header = ContainerRow.WithProjectWait(
            Grouped(dressed)[0],
            dressed);

        Assert.True(header.IsPending);
        Assert.Equal(ContainerAction.PendingLabel(ContainerVerb.Stop), header.Pending);
    }

    [Fact]
    public void A_header_stops_waiting_when_its_last_container_does()
    {
        var activity = new RowActivity();
        var rows = Rows(Summary("shop-api-1", project: "shop"));

        activity.Began("shop-api-1-id", ContainerVerb.Stop);

        // The event the daemon sends when the container is actually down, which is the one thing
        // that ends a wait anywhere in this window.
        activity.Settled("shop-api-1-id");

        var dressed = rows.Select(activity.Dress).ToList();

        Assert.False(ContainerRow.WithProjectWait(Grouped(dressed)[0], dressed).IsPending);
    }

    [Fact]
    public void Two_different_things_under_one_header_are_not_named_as_either()
    {
        // A container clicked on its own while the project is stopping is a second verb under one
        // header, and naming either of them would be picking one at random.
        var activity = new RowActivity();
        var rows = Rows(
            Summary("shop-api-1", project: "shop"),
            Summary("shop-db-1", project: "shop"));

        activity.Began("shop-api-1-id", ContainerVerb.Stop);
        activity.Began("shop-db-1-id", ContainerVerb.Restart);
        var dressed = rows.Select(activity.Dress).ToList();

        Assert.Equal(
            ContainerAction.MixedLabel,
            ContainerRow.WithProjectWait(Grouped(dressed)[0], dressed).Pending);
    }

    [Fact]
    public void Another_projects_wait_is_not_this_ones()
    {
        var activity = new RowActivity();
        var rows = Rows(Summary("a-1", project: "aa"), Summary("b-1", project: "bb"));

        activity.Began("b-1-id", ContainerVerb.Stop);
        var dressed = rows.Select(activity.Dress).ToList();
        var shown = Grouped(dressed);

        var aa = ContainerRow.WithProjectWait(shown.First(row => row.Name == "aa"), dressed);
        var bb = ContainerRow.WithProjectWait(shown.First(row => row.Name == "bb"), dressed);

        Assert.False(aa.IsPending);
        Assert.True(bb.IsPending);
    }

    [Fact]
    public void A_loose_container_waiting_is_nobodys_project()
    {
        var activity = new RowActivity();
        var rows = Rows(Summary("loose-1"), Summary("shop-api-1", project: "shop"));

        activity.Began("loose-1-id", ContainerVerb.Stop);
        var dressed = rows.Select(activity.Dress).ToList();
        var header = Grouped(dressed).First(row => row.IsProject);

        Assert.False(ContainerRow.WithProjectWait(header, dressed).IsPending);
    }

    [Fact]
    public void A_container_row_is_handed_back_untouched()
    {
        // The derivation is about headers. A container already carries its own wait and reading its
        // siblings' onto it would be the defect this fixes, pointed the other way.
        var row = ContainerRow.From(Summary("shop-api-1", project: "shop"));

        Assert.Same(row, ContainerRow.WithProjectWait(row, [row]));
    }

    [Fact]
    public void A_refusal_the_reader_has_not_answered_for_survives_the_wait_ending()
    {
        // The two are independent on a header: some containers can still be stopping while another
        // has already come back refused, and the row says both.
        var activity = new RowActivity();
        var rows = Rows(
            Summary("shop-api-1", project: "shop"),
            Summary("shop-db-1", project: "shop"));

        activity.Began("shop-api-1-id", ContainerVerb.Stop);
        activity.Failed(ContainerRow.ProjectId("shop"), "1 of 2 were refused — the row that failed says why.");

        var dressed = rows.Select(activity.Dress).ToList();
        var header = ContainerRow.WithProjectWait(activity.Dress(Grouped(dressed)[0]), dressed);

        Assert.True(header.IsPending);
        Assert.True(header.HasFailure);
        Assert.Contains("1 of 2", header.Failure!, StringComparison.Ordinal);
    }

    [Fact]
    public void Pressing_the_button_again_clears_the_count_without_claiming_a_wait()
    {
        // Acknowledged, not Began: the header's wait is the children's, and marking it pending here
        // would put a second, staler answer beside the one that is actually correct.
        var activity = new RowActivity();
        var id = ContainerRow.ProjectId("shop");

        activity.Failed(id, "1 of 2 were refused — the row that failed says why.");
        activity.Acknowledged(id);

        var header = activity.Dress(Grouped(Rows(Summary("shop-api-1", project: "shop")))[0]);

        Assert.False(header.HasFailure);
        Assert.False(header.IsPending);
    }

    [Fact]
    public void A_container_is_still_asked_about_exactly_as_it_was()
    {
        // The half of this that must not have moved: DD107 gave a project a dialog and changed
        // nothing about the one a container already had.
        var row = ContainerRow.From(Summary("lonely", "exited"));

        Assert.False(ContainerAction.AsksBeforeRemoving(row));
        Assert.Contains("Remove lonely?", ContainerAction.RemovalPrompt(row), StringComparison.Ordinal);
    }

    // ---- the sort runs twice ---------------------------------------------------------------------

    [Fact]
    public void A_project_sits_where_the_container_leading_it_would_sit()
    {
        // The default column is STATE, which puts running first. Ordering projects alphabetically
        // instead would file a wholly stopped project above a running container and make the one
        // column everybody scans stop meaning anything.
        var shown = Grouped(Rows(
            Summary("aaa-old-1", "exited", "aaa"),
            Summary("zzz-live-1", project: "zzz"),
            Summary("mmm-loose-1")));

        // `aaa` is first alphabetically and last here, because the container leading it is stopped.
        // Under it, `zzz` and the loose row are both running and fall back to the name tie-break.
        Assert.Equal(
            ["mmm-loose-1-id", "compose:zzz", "zzz-live-1-id", "compose:aaa", "aaa-old-1-id"],
            shown.Select(row => row.Id));
    }

    [Fact]
    public void The_sort_also_runs_inside_a_project()
    {
        var shown = Grouped(Rows(
            Summary("shop-z-1", project: "shop"),
            Summary("shop-a-1", "exited", "shop"),
            Summary("shop-b-1", project: "shop")));

        // Running first, then alphabetical inside each group — the same rule the flat list follows.
        Assert.Equal(
            ["compose:shop", "shop-b-1-id", "shop-z-1-id", "shop-a-1-id"],
            shown.Select(row => row.Id));
    }

    [Fact]
    public void A_loose_container_is_never_swept_into_a_group()
    {
        var shown = Grouped(Rows(Summary("alone"), Summary("shop-api-1", project: "shop")));

        var loose = Assert.Single(shown, row => row.Id == "alone-id");
        Assert.Null(loose.Project);
        Assert.Equal(default, loose.Indent);
        Assert.Single(shown, row => row.IsProject);
    }

    // ---- the filter ------------------------------------------------------------------------------

    [Fact]
    public void A_project_whose_containers_all_went_leaves_no_header_behind()
    {
        // A header with nothing under it is worse than no header: it claims a project is on screen
        // and shows none of it.
        var shown = Grouped(
            Rows(Summary("shop-api-1", project: "shop"), Summary("lonely", image: "redis:7")),
            ByState.Narrowed("redis"));

        Assert.DoesNotContain(shown, row => row.IsProject);
        Assert.Equal(["lonely-id"], shown.Select(row => row.Id));
    }

    [Fact]
    public void A_header_counts_what_is_under_it_and_not_what_the_daemon_knows()
    {
        // The count describes the rows on screen. One that described the whole project would be a
        // number a reader cannot check against what they are looking at.
        var shown = Grouped(
            Rows(
                Summary("shop-api-1", project: "shop", image: "shop/api:1"),
                Summary("shop-db-1", project: "shop", image: "postgres:16"),
                Summary("shop-old-1", "exited", "shop", image: "shop/api:1")),
            ByState.Narrowed("postgres"));

        Assert.Equal("1 of 1 running", shown[0].ProjectCount);
        Assert.Equal(["compose:shop", "shop-db-1-id"], shown.Select(row => row.Id));
    }

    [Fact]
    public void Typing_the_project_name_keeps_the_project()
    {
        // The one word a reader knows a group by must not be the one word that empties the list.
        var shown = Grouped(
            Rows(Summary("api-1", project: "shop"), Summary("elsewhere")),
            ByState.Narrowed("shop"));

        Assert.Equal(["compose:shop", "api-1-id"], shown.Select(row => row.Id));
    }

    // ---- collapsing --------------------------------------------------------------------------------

    [Fact]
    public void A_collapsed_project_keeps_its_header_and_drops_its_children()
    {
        var rows = Rows(
            Summary("shop-api-1", project: "shop"),
            Summary("shop-db-1", project: "shop"),
            Summary("loose-1"));

        var shown = Grouped(rows, ByState, "shop");

        // The header keeps the place its leading container earned, folded or not — both are running
        // here, so the name tie-break puts the loose row above it.
        Assert.Equal(["loose-1-id", "compose:shop"], shown.Select(row => row.Id));

        var header = shown[1];
        Assert.True(header.Collapsed);

        // Still counts the whole project: folding it away is what makes the count the only thing
        // left saying anything about it.
        Assert.Equal("2 of 2 running", header.ProjectCount);
    }

    [Fact]
    public void The_chevron_says_which_way_the_project_is()
    {
        var open = Grouped(Rows(Summary("shop-api-1", project: "shop")))[0];
        var shut = Grouped(Rows(Summary("shop-api-1", project: "shop")), ByState, "shop")[0];

        Assert.NotEqual(open.Chevron, shut.Chevron);
        Assert.Equal("", open.Chevron);
        Assert.Equal("", shut.Chevron);
    }

    [Fact]
    public void Collapsing_one_project_leaves_another_open()
    {
        var shown = Grouped(
            Rows(Summary("a-1", project: "aa"), Summary("b-1", project: "bb")),
            ByState,
            "aa");

        Assert.Equal(["compose:aa", "compose:bb", "b-1-id"], shown.Select(row => row.Id));
    }

    // ---- and the flat list is unchanged ------------------------------------------------------------

    [Fact]
    public void A_machine_with_no_compose_project_is_the_list_it_always_was()
    {
        // The grouping must cost nothing where there is nothing to group: a header over a machine
        // that runs no compose project would be a level of hierarchy invented for one row.
        var rows = Rows(Summary("b"), Summary("a", "exited"), Summary("c"));

        Assert.Equal(
            ContainerRow.Shaped(rows, ByState).Select(row => row.Id),
            Grouped(rows).Select(row => row.Id));
    }
}
