using FreeWilly.Core.Agent;
using FreeWilly.Core.Api;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// A delta over the daemon's own history: collapsed per object, and loud when it is incomplete (DD31).
/// </summary>
public sealed class ChangeFeedTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private static DockerEvent Moved(
        string action,
        string name = "shop-worker-1",
        string type = "container",
        string id = "aaaaaaaaaaaa0000",
        string? exitCode = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = name };
        if (exitCode is not null)
        {
            attributes["exitCode"] = exitCode;
        }

        return new DockerEvent
        {
            Type = type,
            Action = action,
            Actor = new EventActor { Id = id, Attributes = attributes },
            Time = Now.ToUnixTimeSeconds(),
        };
    }

    // ---- collapsed per object, not per event ---------------------------------------------------

    [Fact]
    public void A_crash_loop_is_one_line_and_carries_its_count_and_its_exit()
    {
        // Twelve events saying one thing. The line is what a caller was going to reduce them to, and
        // the exit code is what makes it a diagnosis rather than a notification.
        var delta = ChangeFeed.Collapse(
            [
                Moved("start"), Moved("die", exitCode: "137"),
                Moved("start"), Moved("die", exitCode: "137"),
                Moved("start"), Moved("die", exitCode: "137"),
                Moved("start"), Moved("die", exitCode: "137"),
            ],
            Now);

        var row = Assert.Single(delta.Rows);
        Assert.Equal("shop-worker-1", row.Name);
        Assert.Equal("restarted ×3, exited 137", row.What);
    }

    [Fact]
    public void One_start_is_not_a_restart()
    {
        var delta = ChangeFeed.Collapse([Moved("create"), Moved("start")], Now);

        Assert.Equal("running", Assert.Single(delta.Rows).What);
    }

    [Fact]
    public void Two_objects_are_two_rows_in_a_deterministic_order()
    {
        var delta = ChangeFeed.Collapse(
            [
                Moved("start", "shop-worker-1", id: "aaaa"),
                Moved("start", "shop-api-1", id: "bbbb"),
                Moved("create", "shop_data", type: "volume", id: "cccc"),
            ],
            Now);

        // Kind then name, so a payload caches and a diff between two calls means something.
        Assert.Equal(
            [("container", "shop-api-1"), ("container", "shop-worker-1"), ("volume", "shop_data")],
            delta.Rows.Select(r => (r.Kind, r.Name)).ToArray());
    }

    [Fact]
    public void The_noise_the_daemon_emits_is_not_in_the_delta()
    {
        // exec_create, exec_start and every health probe change nothing a caller could act on, and
        // including them turns a delta back into a log - which is the cost this verb exists to avoid.
        var delta = ChangeFeed.Collapse(
            [
                Moved("exec_create: /bin/sh"), Moved("exec_start: /bin/sh"),
                Moved("health_status: healthy"),
            ],
            Now);

        Assert.Empty(delta.Rows);
        Assert.Contains("(nothing moved)", ChangeFeed.Render(delta), StringComparison.Ordinal);
    }

    [Fact]
    public void A_renamed_container_is_called_what_it_is_called_now()
    {
        // The old name would send the next call to something that no longer answers to it.
        var delta = ChangeFeed.Collapse(
            [Moved("start", "old-name"), Moved("rename", "new-name")], Now);

        Assert.Equal("new-name", Assert.Single(delta.Rows).Name);
    }

    // ---- a bounded history has to say so -------------------------------------------------------

    [Fact]
    public void A_full_ring_is_reported_as_too_old_rather_than_as_a_delta()
    {
        // The failure mode of a delta that quietly skips is worse than no delta, because nothing
        // downstream can detect it.
        var many = Enumerable.Range(0, ChangeFeed.DaemonRing)
            .Select(i => Moved("start", $"c-{i}", id: $"id-{i}"))
            .ToList();

        var delta = ChangeFeed.Collapse(many, Now);

        Assert.True(delta.TooOld);
        var text = ChangeFeed.Render(delta);

        // First line, because a caller that stops reading after one has to stop on this one.
        Assert.StartsWith("too old", text, StringComparison.Ordinal);
        Assert.Contains("read context", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_history_that_fits_is_not_called_too_old()
    {
        var delta = ChangeFeed.Collapse(
            Enumerable.Range(0, ChangeFeed.DaemonRing - 1)
                .Select(i => Moved("start", $"c-{i}", id: $"id-{i}"))
                .ToList(),
            Now);

        Assert.False(delta.TooOld);
        Assert.DoesNotContain("too old", ChangeFeed.Render(delta), StringComparison.Ordinal);
    }

    // ---- the ceiling ---------------------------------------------------------------------------

    [Fact]
    public void A_busy_machine_is_truncated_with_a_count_and_never_in_silence()
    {
        // 256 events can collapse to a hundred objects, and a delta whose whole argument is that it is
        // cheaper than re-reading the pack cannot answer with something larger than the pack.
        var delta = ChangeFeed.Collapse(
            Enumerable.Range(0, 100).Select(i => Moved("start", $"container-{i:D3}", id: $"id-{i}")).ToList(),
            Now);

        var text = ChangeFeed.Render(delta);

        Assert.True(
            TokenEstimate.Of(text) <= ChangeFeed.CeilingTokens,
            $"{TokenEstimate.Of(text)} tokens against a ceiling of {ChangeFeed.CeilingTokens}");
        Assert.Contains("more object(s) moved", text, StringComparison.Ordinal);

        // The cursor survives the cut: it is what the next call needs, and a truncated payload that
        // dropped it would leave the caller unable to continue at all.
        Assert.Contains(delta.Cursor, text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_ceiling_in_code_is_the_ceiling_recorded_in_the_budget()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        string? found = null;
        while (here is not null && found is null)
        {
            var candidate = System.IO.Path.Combine(here.FullName, "agent-budget.json");
            found = File.Exists(candidate) ? candidate : null;
            here = here.Parent;
        }

        Assert.NotNull(found);
        using var budget = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(found));
        Assert.Equal(
            ChangeFeed.CeilingTokens,
            budget.RootElement.GetProperty("surface").GetProperty("shapes")
                .GetProperty("read changes").GetInt32());
    }

    // ---- the cursor ----------------------------------------------------------------------------

    [Fact]
    public void The_cursor_round_trips_through_the_text_it_was_printed_in()
    {
        var delta = ChangeFeed.Collapse([Moved("start")], Now);

        Assert.True(ChangeFeed.TryParseCursor(delta.Cursor, out var at, out var why));
        Assert.Null(why);
        Assert.Equal(Now, at);
    }

    [Fact]
    public void A_context_cursor_is_refused_by_name_and_not_by_shape()
    {
        // c: fingerprints the machine's state and carries no moment, so there is nothing to ask the
        // daemon about. The caller is one call away from the right cursor, and the refusal says which.
        Assert.False(ChangeFeed.TryParseCursor("c:231884", out _, out var why));
        Assert.NotNull(why);
        Assert.Contains("fingerprints the machine's state", why, StringComparison.Ordinal);
        Assert.Contains("read changes", why, StringComparison.Ordinal);
    }

    [Fact]
    public void Something_that_is_not_a_cursor_at_all_says_what_one_looks_like()
    {
        Assert.False(ChangeFeed.TryParseCursor("yesterday", out _, out var why));
        Assert.Contains(ChangeFeed.CursorPrefix, why!, StringComparison.Ordinal);
    }
}
