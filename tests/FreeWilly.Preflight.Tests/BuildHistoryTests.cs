using FreeWilly.Core.Builds;
using FreeWilly.Core.Fixtures;
using FreeWilly.Tray.Ui;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// Reading the build history the pinned Buildx answers with, and shaping it into rows (DD126).
/// </summary>
public class BuildHistoryTests
{
    /// <summary>Two records exactly as `buildx history ls --format json` printed them.</summary>
    /// <remarks>
    /// Copied from the pinned plugin's real output rather than written by hand: the shape being
    /// parsed is upstream's, and a fixture invented here would keep passing after upstream changed
    /// it. Note the snake case, and that it is one object per line with no enclosing array.
    /// </remarks>
    private const string RealOutput = """
        {"cached_steps":0,"completed_at":"2026-08-15T00:49:56.157691628Z","completed_steps":5,"created_at":"2026-08-15T00:49:55.723951336Z","name":"webnextaem/skeleton/author","ref":"default/default/i93abaotri2m3vdda5unxeimu","status":"Completed","total_steps":5}
        {"cached_steps":1,"completed_at":"2026-08-15T00:49:54.513759719Z","completed_steps":11,"created_at":"2026-08-15T00:48:06.776499584Z","name":"webnextaem/skeleton/base","ref":"default/default/6g4actvxnjml7k6qe6pz54c3l","status":"Completed","total_steps":11}
        """;

    [Fact]
    public void The_streamed_list_is_read_one_object_per_line()
    {
        // Not a JSON array — buildx streams it, so a whole-document parse fails on the second
        // record. This is the shape, and it is the reason ReadList exists at all.
        var builds = BuildHistory.ReadList(RealOutput);

        Assert.Equal(2, builds.Count);
        Assert.Equal("webnextaem/skeleton/author", builds[0].Name);
        Assert.Equal("default/default/i93abaotri2m3vdda5unxeimu", builds[0].Reference);
        Assert.Equal("i93abaotri2m3vdda5unxeimu", builds[0].Id);
        Assert.Equal("Completed", builds[0].Status);
        Assert.Equal(5, builds[0].TotalSteps);
        Assert.Equal(1, builds[1].CachedSteps);
    }

    [Fact]
    public void The_duration_is_the_span_between_the_two_timestamps()
    {
        var builds = BuildHistory.ReadList(RealOutput);

        // 00:48:06.776 → 00:49:54.513 is 1m 47.7s, which is what the CLI's own table prints.
        Assert.Equal(107.7, builds[1].Duration!.Value.TotalSeconds, precision: 1);
    }

    [Fact]
    public void One_unreadable_line_is_one_missing_row_and_not_an_empty_page()
    {
        var builds = BuildHistory.ReadList(
            "{ not json at all\n" + RealOutput + "\n\n  \n");

        Assert.Equal(2, builds.Count);
    }

    [Fact]
    public void A_record_with_no_ref_is_not_a_build()
    {
        // The ref is what the detail is looked up by, so a row without one is a row nothing can be
        // done with.
        Assert.Empty(BuildHistory.ReadList("""{"name":"x","status":"Completed"}"""));
    }

    [Fact]
    public void Nothing_at_all_reads_as_no_builds()
    {
        Assert.Empty(BuildHistory.ReadList(""));
        Assert.Empty(BuildHistory.ReadList("\n\n"));
    }

    [Fact]
    public void A_running_build_has_no_duration_to_report()
    {
        // The column must not invent a number for a build that has not finished.
        var running = Assert.Single(BuildHistory.ReadList(
            """{"name":"x","ref":"a/b/c","status":"Running","created_at":"2026-08-15T00:00:00Z"}"""));

        Assert.Null(running.Duration);
        Assert.Equal("—", BuildRow.From([running])[0].DurationText);
    }

    [Fact]
    public void The_fixture_covers_the_states_the_page_renders_differently()
    {
        // L6's whole point: the tones and the empty-ish cases are reachable without building
        // anything. If a status stops being covered here, a capture stops exercising that tone.
        var rows = BuildRow.From(new SampleBuilds().Recent());

        Assert.Contains(rows, row => row.Tone is RowTone.Good);
        Assert.Contains(rows, row => row.Tone is RowTone.Warn);
        Assert.Contains(rows, row => row.Tone is RowTone.Bad);
        Assert.Contains(rows, row => row.Duration is null);
        Assert.Contains(rows, row => row.CachedSteps == 0);
        Assert.All(rows, row => Assert.StartsWith(SampleMachine.Prefix, row.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void The_fixtures_detail_is_the_same_build_as_its_row()
    {
        // Two hand-written halves would disagree the moment one was edited, and the page shows them
        // together.
        var history = new SampleBuilds();
        foreach (var summary in history.Recent())
        {
            var record = history.Inspect(summary.Reference);
            Assert.NotNull(record);
            Assert.Equal(summary.Name, record!.Name);
            Assert.Equal(summary.Status, record.Status);
            Assert.Equal(summary.TotalSteps, record.TotalSteps);
        }
    }

    [Fact]
    public void The_fixture_answers_nothing_for_a_build_it_does_not_have()
    {
        // The state the page draws as "that build is not in the history", which is what a link to a
        // pruned record reaches.
        Assert.Null(new SampleBuilds().Inspect("default/default/nosuchbuild"));
    }
}
