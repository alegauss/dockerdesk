using DockerDesk.Core.Api;
using DockerDesk.Tray.Ui;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// The buffer behind a log window: where chunks become lines, and where the cap that keeps a window
/// left open overnight from being a leak is enforced.
/// </summary>
public sealed class LogBufferTests
{
    private static LogChunk Out(string text) => new(LogStream.StdOut, text);

    private static LogChunk Err(string text) => new(LogStream.StdErr, text);

    // ---- chunks are not lines --------------------------------------------------------------------

    [Fact]
    public void A_line_split_across_two_chunks_is_one_line()
    {
        // A frame can end mid-sentence; the daemon chunks by write, not by line.
        var buffer = new LogBuffer();

        buffer.Append(Out("listening on "));
        buffer.Append(Out(":8080\n"));

        Assert.Equal(["listening on :8080"], buffer.Lines.Select(l => l.Text));
    }

    [Fact]
    public void A_chunk_carrying_several_lines_becomes_several()
    {
        var buffer = new LogBuffer();

        buffer.Append(Out("one\ntwo\nthree\n"));

        Assert.Equal(["one", "two", "three"], buffer.Lines.Select(l => l.Text));
    }

    [Fact]
    public void A_line_still_being_written_is_not_a_line_yet_but_is_not_lost()
    {
        var buffer = new LogBuffer();

        buffer.Append(Out("no newline yet"));

        Assert.Empty(buffer.Lines);
        Assert.False(buffer.IsEmpty);
        Assert.Contains("no newline yet", buffer.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_stream_that_closed_without_a_final_newline_still_leaves_a_line()
    {
        var buffer = new LogBuffer();
        buffer.Append(Err("panic: last words"));

        buffer.Close();

        Assert.Equal(["panic: last words"], buffer.Lines.Select(l => l.Text));
    }

    [Fact]
    public void Carriage_returns_do_not_reach_the_view()
    {
        // \r\n from a container built on Windows, and the lone \r a progress bar redraws with.
        var buffer = new LogBuffer();

        buffer.Append(Out("windows line\r\nprogress\rdone\n"));

        Assert.Equal(["windows line", "progressdone"], buffer.Lines.Select(l => l.Text));
    }

    // ---- the two streams stay apart ---------------------------------------------------------------

    [Fact]
    public void Stderr_is_marked_so_the_window_can_find_it()
    {
        var buffer = new LogBuffer();

        buffer.Append(Out("starting\n"));
        buffer.Append(Err("panic: nil map\n"));

        Assert.False(buffer.Lines[0].IsError);
        Assert.True(buffer.Lines[1].IsError);
    }

    [Fact]
    public void A_half_written_stdout_line_is_not_spliced_onto_the_stderr_that_interrupted_it()
    {
        // Two writers into one stream. Holding the partial open across the switch would join two
        // unrelated lines into one that never existed.
        var buffer = new LogBuffer();

        buffer.Append(Out("waiting for db"));
        buffer.Append(Err("connection refused\n"));

        Assert.Equal(["waiting for db", "connection refused"], buffer.Lines.Select(l => l.Text));
        Assert.False(buffer.Lines[0].IsError);
        Assert.True(buffer.Lines[1].IsError);
    }

    // ---- the cap, which is the point ---------------------------------------------------------------

    [Fact]
    public void The_buffer_stops_at_the_cap_and_drops_from_the_front()
    {
        // A window left open overnight against an unbounded collection is the leak that gets a tool
        // called heavy. The end is the part being read, so the front is what goes.
        var buffer = new LogBuffer();
        for (var i = 0; i < LogBuffer.MaximumLines + 250; i++)
        {
            buffer.Append(Out($"line {i}\n"));
        }

        Assert.Equal(LogBuffer.MaximumLines, buffer.Lines.Count);
        Assert.Equal(250, buffer.Dropped);
        Assert.Equal("line 250", buffer.Lines[0].Text);
        Assert.Equal($"line {LogBuffer.MaximumLines + 249}", buffer.Lines[^1].Text);
    }

    [Fact]
    public void One_enormous_line_is_capped_too_because_a_line_count_is_not_a_memory_bound()
    {
        // A process printing a megabyte with no newline in it sits under any line count.
        var buffer = new LogBuffer();

        buffer.Append(Out(new string('x', LogBuffer.MaximumLineLength * 3)));
        buffer.Close();

        Assert.Equal(LogBuffer.MaximumLineLength, buffer.Lines[0].Text.Length);
    }

    // ---- what the window says --------------------------------------------------------------------

    [Fact]
    public void The_status_says_how_much_was_dropped_rather_than_hiding_it()
    {
        var buffer = new LogBuffer();
        for (var i = 0; i < LogBuffer.MaximumLines + 3; i++)
        {
            buffer.Append(Out($"{i}\n"));
        }

        var status = buffer.Status(following: true, ended: false);

        Assert.Contains("3 dropped", status, StringComparison.Ordinal);
        Assert.Contains("following", status, StringComparison.Ordinal);
    }

    [Fact]
    public void The_status_tells_following_from_paused_from_over()
    {
        var buffer = new LogBuffer();
        buffer.Append(Out("one\n"));

        Assert.Contains("following", buffer.Status(following: true, ended: false), StringComparison.Ordinal);
        Assert.Contains("not following", buffer.Status(following: false, ended: false), StringComparison.Ordinal);
        Assert.Contains("stream ended", buffer.Status(following: true, ended: true), StringComparison.Ordinal);
    }

    [Fact]
    public void Copy_all_takes_the_line_still_being_written_too()
    {
        // The most recent line is the one somebody is most likely copying for, and it has no
        // newline yet by definition.
        var buffer = new LogBuffer();
        buffer.Append(Out("done\n"));
        buffer.Append(Err("panic: no newline"));

        Assert.Equal("done\npanic: no newline\n", buffer.ToText());
    }

    // ---- the three silences ------------------------------------------------------------------------

    [Fact]
    public void A_log_with_lines_in_it_has_no_empty_state()
    {
        Assert.Null(LogEmptyState.For(empty: false, ended: false, failure: null));
    }

    [Fact]
    public void A_quiet_open_log_and_a_container_that_wrote_nothing_are_different_answers()
    {
        var waiting = LogEmptyState.For(empty: true, ended: false, failure: null);
        var nothing = LogEmptyState.For(empty: true, ended: true, failure: null);

        Assert.NotNull(waiting);
        Assert.NotNull(nothing);
        Assert.NotEqual(waiting!.Headline, nothing!.Headline);
    }

    [Fact]
    public void A_stream_that_could_not_be_read_says_so_in_the_engine_s_words()
    {
        // The worst blank box: the user is looking at a failure and being shown nothing.
        var failed = LogEmptyState.For(
            empty: true, ended: true, failure: "No such container: a1b2c3d4e5f6");

        Assert.NotNull(failed);
        Assert.Contains("No such container", failed!.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failure_outranks_having_lines_because_the_log_stopped_being_true()
    {
        var failed = LogEmptyState.For(empty: false, ended: true, failure: "the engine went away");

        Assert.NotNull(failed);
    }
}
