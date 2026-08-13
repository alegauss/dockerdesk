using FreeWilly.Core.Agent;
using FreeWilly.Core.Api;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The log contract: a cursor, a level, a dedup and a ceiling (DD27).
/// </summary>
/// <remarks>
/// DD23 measured one 200-line tail at 4170 estimated tokens, second only to re-discovery, and a
/// container that restarts eight times writes the same trace eight times. Each argument here is a test,
/// and so is the thing each one must not do: a filter must not hide the answer, a dedup must not reorder
/// the log, and a ceiling must not cut in silence.
/// </remarks>
public sealed class LogDigestTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 13, 9, 16, 0, TimeSpan.Zero);

    private static LogChunk Out(string text) => new(LogStream.StdOut, text);

    private static LogChunk Err(string text) => new(LogStream.StdErr, text);

    /// <summary>A line as the daemon writes it with timestamps=1.</summary>
    private static string Stamped(int second, string text) =>
        T0.AddSeconds(second).ToString("O") + " " + text + "\n";

    // ---- splitting ------------------------------------------------------------------------------

    [Fact]
    public void A_line_split_across_two_frames_is_still_one_line()
    {
        // A frame can end mid-line, so carrying text per stream is what stops a stack trace being cut
        // wherever the daemon happened to flush.
        var lines = LogDigest.Split([Out("hello, "), Out("world\n")]);

        var line = Assert.Single(lines);
        Assert.Equal("hello, world", line.Text);
    }

    [Fact]
    public void The_two_streams_do_not_bleed_into_each_other()
    {
        var lines = LogDigest.Split([Out("stdout "), Err("stderr line\n"), Out("line\n")]);

        Assert.Equal(2, lines.Count);
        Assert.Equal("stderr line", lines.Single(l => l.Stream == LogStream.StdErr).Text);
        Assert.Equal("stdout line", lines.Single(l => l.Stream == LogStream.StdOut).Text);
    }

    [Fact]
    public void A_timestamp_is_taken_off_the_front_and_kept()
    {
        var lines = LogDigest.Split([Out(Stamped(0, "started"))]);

        var line = Assert.Single(lines);
        Assert.Equal("started", line.Text);
        Assert.Equal(T0, line.Timestamp);
    }

    [Fact]
    public void A_line_with_no_timestamp_is_still_a_line()
    {
        var line = Assert.Single(LogDigest.Split([Out("no stamp here\n")]));

        Assert.Null(line.Timestamp);
        Assert.Equal("no stamp here", line.Text);
    }

    [Fact]
    public void A_trailing_line_with_no_newline_is_not_dropped() =>
        Assert.Single(LogDigest.Split([Out("the last line had no newline")]));

    // ---- the level filter, which must not hide the answer ----------------------------------------

    [Theory]
    [InlineData("ERROR failed to bind", LogLevel.Error)]
    [InlineData("2026-08-13 WARN slow query", LogLevel.Warn)]
    [InlineData("[info] listening on 8080", LogLevel.Info)]
    [InlineData("DEBUG cache miss", LogLevel.Debug)]
    [InlineData("FATAL: error while starting", LogLevel.Fatal)]
    [InlineData("\tat java.base/sun.nio.ch.Net.bind0(Native Method)", LogLevel.Unknown)]
    [InlineData("", LogLevel.Unknown)]
    public void A_line_is_read_for_what_it_says_it_is(string text, LogLevel expected) =>
        // FATAL beats error in that one on purpose: most severe first, or a fatal line reads as an error.
        Assert.Equal(expected, LogDigest.LevelOf(text));

    [Fact]
    public void A_level_is_only_read_from_the_front_of_a_line() =>
        // Matching anywhere would make --level error keep prose about errors and drop the trace under it.
        Assert.Equal(
            LogLevel.Unknown,
            LogDigest.LevelOf("a line of quite ordinary prose that goes on for a while and only then mentions error"));

    [Fact]
    public void A_line_whose_level_is_unknown_survives_the_filter()
    {
        // The whole point: a stack trace's continuation lines say nothing about severity, and dropping
        // them would leave the caller an error with no trace. --level error means "errors, and anything
        // that did not say".
        var lines = LogDigest.Split(
        [
            Out(Stamped(0, "INFO listening")),
            Err(Stamped(1, "ERROR failed to bind")),
            Err(Stamped(2, "\tat shop.api.Server.start(Server.java:88)")),
        ]);

        var result = LogDigest.Render(lines, new LogQuery(MinimumLevel: LogLevel.Error));

        Assert.DoesNotContain("listening", result.Text, StringComparison.Ordinal);
        Assert.Contains("failed to bind", result.Text, StringComparison.Ordinal);
        Assert.Contains("Server.java:88", result.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("error", LogLevel.Error)]
    [InlineData("WARN", LogLevel.Warn)]
    [InlineData("warning", LogLevel.Warn)]
    [InlineData("fatal", LogLevel.Fatal)]
    public void A_level_a_caller_typed_is_read(string text, LogLevel expected)
    {
        Assert.True(LogDigest.TryParseLevel(text, out var level));
        Assert.Equal(expected, level);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_is_not_a_level(string? text) =>
        Assert.False(LogDigest.TryParseLevel(text, out _));

    // ---- dedup ----------------------------------------------------------------------------------

    [Fact]
    public void An_identical_repeat_collapses_to_a_count()
    {
        // The measured case: a restart loop writes the same trace once per restart, and forty-seven
        // copies of an answer is the same answer at forty-seven times the price.
        var chunks = new List<LogChunk>();
        for (var i = 0; i < 47; i++)
        {
            chunks.Add(Err(Stamped(i, "java.net.BindException: Address already in use")));
        }

        var result = LogDigest.Render(LogDigest.Split(chunks), new LogQuery(Dedup: true));

        Assert.Equal(1, result.Lines);
        Assert.Contains("× 47", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeats_that_are_not_adjacent_still_collapse()
    {
        // A restart loop separates its copies with everything else each run printed, so adjacency is the
        // wrong rule.
        var result = LogDigest.Render(
            LogDigest.Split(
            [
                Err(Stamped(0, "BindException")),
                Out(Stamped(1, "starting")),
                Err(Stamped(2, "BindException")),
                Out(Stamped(3, "starting")),
                Err(Stamped(4, "BindException")),
            ]),
            new LogQuery(Dedup: true));

        Assert.Equal(2, result.Lines);
        Assert.Contains("× 3", result.Text, StringComparison.Ordinal);
        Assert.Contains("× 2", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_first_occurrence_keeps_its_place_so_the_order_is_still_the_logs()
    {
        var result = LogDigest.Render(
            LogDigest.Split(
            [
                Out(Stamped(0, "first")),
                Out(Stamped(1, "second")),
                Out(Stamped(2, "first")),
            ]),
            new LogQuery(Dedup: true));

        var lines = result.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("first", lines[0], StringComparison.Ordinal);
        Assert.Contains("second", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_text_on_the_two_streams_is_not_the_same_line()
    {
        // stdout and stderr saying the same thing is two facts, and one of them is where a failure went.
        var result = LogDigest.Render(
            LogDigest.Split([Out(Stamped(0, "same")), Err(Stamped(1, "same"))]),
            new LogQuery(Dedup: true));

        Assert.Equal(2, result.Lines);
    }

    [Fact]
    public void Without_dedup_every_copy_is_returned()
    {
        var result = LogDigest.Render(
            LogDigest.Split([Out(Stamped(0, "same")), Out(Stamped(1, "same"))]),
            new LogQuery());

        Assert.Equal(2, result.Lines);
        Assert.DoesNotContain("×", result.Text, StringComparison.Ordinal);
    }

    // ---- the ceiling, and never in silence -------------------------------------------------------

    [Fact]
    public void A_budget_truncates_and_says_how_many_lines_went()
    {
        // A payload that quietly drops the end reads exactly like a log that ended, which is the one
        // failure this must not have.
        var chunks = Enumerable.Range(0, 200)
            .Select(i => Out(Stamped(i, $"line {i} with enough text on it to cost a few tokens each")))
            .ToList();

        var result = LogDigest.Render(LogDigest.Split(chunks), new LogQuery(BudgetTokens: 200));

        Assert.True(result.Dropped > 0);
        Assert.Contains("truncated", result.Text, StringComparison.Ordinal);
        Assert.True(
            TokenEstimate.Of(result.Text) <= 200,
            $"{TokenEstimate.Of(result.Text)} estimated tokens against a budget of 200");
    }

    [Fact]
    public void A_log_inside_the_budget_is_not_truncated()
    {
        var result = LogDigest.Render(
            LogDigest.Split([Out(Stamped(0, "short"))]), new LogQuery(BudgetTokens: 200));

        Assert.Equal(0, result.Dropped);
        Assert.DoesNotContain("truncated", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_truncated_payload_still_carries_the_cursor_to_read_on_from()
    {
        var chunks = Enumerable.Range(0, 200)
            .Select(i => Out(Stamped(i, $"line {i} padded out so the budget is actually reached here")))
            .ToList();

        var result = LogDigest.Render(LogDigest.Split(chunks), new LogQuery(BudgetTokens: 150));

        Assert.NotNull(result.Cursor);
        Assert.Contains("cursor  " + result.Cursor, result.Text, StringComparison.Ordinal);
    }

    // ---- the cursor -----------------------------------------------------------------------------

    [Fact]
    public void The_cursor_is_the_last_timestamp_read_and_not_the_last_one_kept()
    {
        // Taken before filtering on purpose: a cursor from after a level filter would resume from the
        // last error and silently skip everything quieter written since.
        var lines = LogDigest.Split(
        [
            Err(Stamped(0, "ERROR failed")),
            Out(Stamped(60, "INFO carried on quietly")),
        ]);

        var result = LogDigest.Render(lines, new LogQuery(MinimumLevel: LogLevel.Error));

        Assert.DoesNotContain("carried on", result.Text, StringComparison.Ordinal);
        Assert.Equal(
            LogDigest.CursorPrefix + T0.AddSeconds(60).ToString("O"),
            result.Cursor);
    }

    [Fact]
    public void Since_returns_only_what_came_after_it()
    {
        var lines = LogDigest.Split(
            [Out(Stamped(0, "before")), Out(Stamped(10, "after"))]);

        var result = LogDigest.Render(lines, new LogQuery(Since: T0.AddSeconds(5)));

        Assert.DoesNotContain("before", result.Text, StringComparison.Ordinal);
        Assert.Contains("after", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Since_the_last_cursor_returns_nothing_rather_than_the_last_line_again()
    {
        // The endpoint's own since is inclusive of the second it names, so filtering on the exact
        // timestamp is what stops a caller paying for the line it already has.
        var lines = LogDigest.Split([Out(Stamped(0, "only line"))]);
        var first = LogDigest.Render(lines, new LogQuery());

        Assert.True(LogDigest.TryParseCursor(first.Cursor, out var since, out _));
        var second = LogDigest.Render(lines, new LogQuery(Since: since));

        Assert.Equal(0, second.Lines);
        Assert.Contains("nothing since that cursor", second.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_state_cursor_is_refused_by_name_rather_than_generically()
    {
        // The two cursors on this surface look alike and mean different things, so the refusal says
        // which one was pasted.
        Assert.False(LogDigest.TryParseCursor("c:231884", out _, out var refusal));
        Assert.Contains("state cursor", refusal!, StringComparison.Ordinal);
        Assert.Contains("read context", refusal!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_is_not_a_cursor(string? text) =>
        Assert.False(LogDigest.TryParseCursor(text, out _, out _));

    [Fact]
    public void A_cursor_round_trips_with_and_without_its_prefix()
    {
        Assert.True(LogDigest.TryParseCursor("t:" + T0.ToString("O"), out var withPrefix, out _));
        Assert.True(LogDigest.TryParseCursor(T0.ToString("O"), out var without, out _));
        Assert.Equal(T0, withPrefix);
        Assert.Equal(T0, without);
    }

    // ---- what a stream marks --------------------------------------------------------------------

    [Fact]
    public void Each_line_says_which_stream_it_came_from()
    {
        // Which stream a failure went to is a fact worth one character, and DD26's doctor reads stderr
        // for exactly this reason.
        var result = LogDigest.Render(
            LogDigest.Split([Out(Stamped(0, "out")), Err(Stamped(1, "err"))]), new LogQuery());

        Assert.Contains("O  out", result.Text, StringComparison.Ordinal);
        Assert.Contains("E  err", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_log_says_so_rather_than_returning_nothing()
    {
        var result = LogDigest.Render([], new LogQuery());

        Assert.Equal(0, result.Lines);
        Assert.Contains("nothing", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void What_the_dedup_is_worth_on_a_restart_loop()
    {
        // The measured claim, in DD23's unit. A container that restarted eight times writing a
        // six-line trace each time is the canonical shape, and the answer is the same either way.
        const string trace = """
            ERROR shop.api.Bootstrap - failed to bind :8080
            java.net.BindException: Address already in use
            	at java.base/sun.nio.ch.Net.bind0(Native Method)
            	at java.base/sun.nio.ch.Net.bind(Net.java:565)
            	at shop.api.http.Server.start(Server.java:88)
            	at shop.api.Bootstrap.main(Bootstrap.java:41)
            """;

        var chunks = new List<LogChunk>();
        var second = 0;
        for (var restart = 0; restart < 8; restart++)
        {
            foreach (var line in trace.Split('\n'))
            {
                chunks.Add(Err(Stamped(second++, line.TrimEnd('\r'))));
            }
        }

        var lines = LogDigest.Split(chunks);
        var whole = TokenEstimate.Of(LogDigest.Render(lines, new LogQuery()).Text);
        var deduped = TokenEstimate.Of(LogDigest.Render(lines, new LogQuery(Dedup: true)).Text);

        Assert.Equal(48, lines.Count);
        Assert.True(
            deduped * 4 < whole,
            $"the dedup saved too little to be worth the argument: {whole} tokens became {deduped}");
    }

    [Fact]
    public void The_default_ceiling_is_the_one_the_budget_records()
    {
        // A registered ceiling that nothing enforces describes nothing. `read logs` applies this when the
        // caller names no budget, so the number in the file is the behaviour.
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null)
        {
            var candidate = System.IO.Path.Combine(here.FullName, "agent-budget.json");
            if (File.Exists(candidate))
            {
                using var budget = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(candidate));
                Assert.Equal(
                    LogDigest.DefaultBudgetTokens,
                    budget.RootElement.GetProperty("surface").GetProperty("shapes")
                        .GetProperty("read logs").GetInt32());
                return;
            }

            here = here.Parent;
        }

        Assert.Fail("agent-budget.json was not found");
    }

    [Fact]
    public void Nothing_renders_from_null()
    {
        Assert.Throws<ArgumentNullException>(() => LogDigest.Render(null!, new LogQuery()));
        Assert.Throws<ArgumentNullException>(() => LogDigest.Render([], null!));
        Assert.Throws<ArgumentNullException>(() => LogDigest.Split(null!));
    }
}
