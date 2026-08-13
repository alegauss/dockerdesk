using FreeWilly.Tray.Ui;

// WinForms contributes a RowStyle too; this file means the one the rows are drawn with.
using RowStyle = FreeWilly.Tray.Ui.RowStyle;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// A row carries its state rather than a wall of captions (DD36).
/// </summary>
/// <remarks>
/// Two things are pinned here. The chip has to make a distinction the tertiary grey could not — a
/// clean exit and a kill are not the same event — and the three answers the redesign was not allowed
/// to lose have to still be there: the pending word, the engine's own refusal under the row, and Shell
/// disabled with a reason rather than hidden.
/// </remarks>
public sealed class ContainerChipTests
{
    private static ContainerRow Row(string state, string status) =>
        new("sample-api-1", "sample/api:1.4.2", state, status, [], "c1aaaaaaaaaa0000");

    private static string Markup()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FreeWilly.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "the repository root was not found above the test binaries");
        return File.ReadAllText(
            Path.Combine(directory!.FullName, "src/FreeWilly.Tray/Ui/Pages/ContainersPage.xaml"));
    }

    // ---- the distinction the grey could not make -------------------------------------------------

    [Theory]
    [InlineData("running", "Up 6 minutes", RowTone.Good)]
    [InlineData("paused", "Paused", RowTone.Warn)]
    [InlineData("restarting", "Restarting (1)", RowTone.Warn)]
    [InlineData("created", "Created", RowTone.Warn)]
    [InlineData("exited", "Exited (0) 3 minutes ago", RowTone.Muted)]
    [InlineData("exited", "Exited (137) 12 seconds ago", RowTone.Bad)]
    [InlineData("exited", "Exited (1) 2 minutes ago", RowTone.Bad)]
    public void The_chip_says_which_kind_of_stopped_this_is(string state, string status, RowTone tone)
    {
        // A migration container that finished is not a problem to draw attention to, and a container
        // the kernel killed is. Both were `exited` in the same tertiary grey.
        Assert.Equal(tone, Row(state, status).Tone);
    }

    [Fact]
    public void The_exit_code_is_read_off_the_list_and_not_asked_for()
    {
        // An inspect per row to read one integer is the call this window does not make.
        Assert.Equal(137, Row("exited", "Exited (137) 12 seconds ago").ExitCode);
        Assert.Equal(0, Row("exited", "Exited (0) 3 minutes ago").ExitCode);
        Assert.Null(Row("running", "Up 6 minutes").ExitCode);

        // A status the daemon words differently is not a crash here: no code, and the tone falls back.
        Assert.Null(Row("exited", "Exited a while back").ExitCode);
    }

    [Fact]
    public void A_chip_carries_evidence_rather_than_the_column_beside_it()
    {
        var killed = Row("exited", "Exited (137) 12 seconds ago");

        // A tooltip that repeated the status column would be the same sentence twice. 137 is the one
        // worth spelling out, and it is the exit code the diagnostic half of this product is for.
        Assert.NotEqual(killed.Status, killed.StateEvidence);
        Assert.Contains("SIGKILL", killed.StateEvidence, StringComparison.Ordinal);
        Assert.Contains("memory limit", killed.StateEvidence, StringComparison.Ordinal);

        Assert.Contains("meant to", Row("exited", "Exited (0) 1 minute ago").StateEvidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Dressing_a_row_fills_both_halves_of_its_chip()
    {
        // A fill with no text brush is a chip whose word is unreadable on it.
        var bare = Row("running", "Up 6 minutes");
        Assert.Null(bare.ChipFill);

        var style = new RowStyle(
            System.Windows.Media.Brushes.Green, System.Windows.Media.Brushes.Orange,
            System.Windows.Media.Brushes.Red, System.Windows.Media.Brushes.Gray,
            System.Windows.Media.Brushes.White, System.Windows.Media.Brushes.LightGray);

        var dressed = bare.WithChip(style);
        Assert.Equal(System.Windows.Media.Brushes.Green, dressed.ChipFill);
        Assert.Equal(System.Windows.Media.Brushes.White, dressed.ChipText);
    }

    // ---- the primary verb, and the three that moved ----------------------------------------------

    [Theory]
    [InlineData("running", "Stop")]
    [InlineData("paused", "Stop")]
    [InlineData("exited", "Start")]
    [InlineData("created", "Start")]
    public void The_row_offers_the_one_verb_it_was_opened_for(string state, string verb)
    {
        var row = Row(state, "whatever");
        Assert.True(row.HasPrimary);
        Assert.Equal(verb, row.PrimaryVerb);
    }

    [Fact]
    public void A_row_with_something_in_flight_offers_no_verb_at_all()
    {
        // The pending word is what the row says instead, and pressing a second verb while the first
        // is in flight is how a stop and a remove race.
        var busy = Row("running", "Up 6 minutes") with { Pending = "Stopping…" };
        Assert.False(busy.HasPrimary);
    }

    [Fact]
    public void The_row_draws_three_controls_and_not_six()
    {
        var markup = Markup();

        // Six word captions per row is two hundred of them on a list of forty, and the eye has
        // nothing to skip past.
        Assert.Contains("Content=\"Logs\"", markup, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding PrimaryVerb}\"", markup, StringComparison.Ordinal);
        Assert.Contains("<ContextMenu>", markup, StringComparison.Ordinal);

        foreach (var moved in new[] { "Content=\"Shell\"", "Content=\"Restart\"", "Content=\"Remove\"" })
        {
            Assert.DoesNotContain(moved, markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_overflow_is_drawn_rather_than_revealed()
    {
        // The section is explicit that hiding a verb costs a discovery problem. One click deeper is
        // not the same as hidden, and a control that only appears on hover is unreachable by keyboard.
        var markup = Markup();
        var start = markup.IndexOf("Grid.Column=\"5\"", StringComparison.Ordinal);
        Assert.True(start > 0, "the actions column is gone");

        var actions = markup[start..markup.IndexOf("</StackPanel>", start, StringComparison.Ordinal)];

        Assert.Contains("OpenOverflow", actions, StringComparison.Ordinal);

        // Nothing in the action set may turn on the pointer being over the row. The hover surface
        // itself is a ListViewItem trigger and lives well above this block.
        Assert.DoesNotContain("IsMouseOver", actions, StringComparison.Ordinal);
    }

    // ---- what the redesign was not allowed to lose -----------------------------------------------

    [Fact]
    public void The_three_answers_a_better_looking_row_could_have_dropped_are_still_there()
    {
        var markup = Markup();

        // The pending word: the half-second between the click and the engine's first word.
        Assert.Contains("{Binding Pending}", markup, StringComparison.Ordinal);

        // The engine's own refusal, under the row that caused it.
        Assert.Contains("{Binding Failure}", markup, StringComparison.Ordinal);

        // Shell disabled with a reason, not hidden: "you cannot do this yet" and "this does not
        // exist" are different answers and only the tooltip tells them apart.
        Assert.Contains("IsEnabled=\"{Binding CanShell}\"", markup, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding ShellReason}\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_highlights_so_that_it_reads_as_a_row()
    {
        // Nothing highlighted before, so a list of forty read as a wall.
        Assert.Contains("IsMouseOver", Markup(), StringComparison.Ordinal);
    }
}
