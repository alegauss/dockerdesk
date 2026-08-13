using System.Drawing;
using DockerDesk.Core.Engine;
using DockerDesk.Tray.Ui;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// The window remembers where it was and what was being read (DD39).
/// </summary>
public sealed class WindowMemoryTests
{
    private static readonly Rectangle Laptop = new(0, 0, 1920, 1080);
    private static readonly Rectangle SecondScreen = new(1920, 0, 2560, 1440);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DockerDesk.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "the repository root was not found above the test binaries");
        return directory!.FullName;
    }

    private static WindowMemory On(Rectangle where) =>
        new() { Left = where.X, Top = where.Y, Width = where.Width, Height = where.Height };

    // ---- the rectangle is only used if it still exists ---------------------------------------------

    [Fact]
    public void A_window_remembered_onto_a_screen_that_is_gone_is_not_restored()
    {
        // The whole reason this check exists: the second screen was on the desk last night and the
        // laptop is on a train this morning. Restoring the saved rectangle would open the window at
        // x=2100 on a machine whose only monitor ends at 1920, and the recovery is a keyboard
        // shortcut nobody knows.
        var docked = On(new Rectangle(2100, 300, 1040, 560));

        Assert.True(docked.LandsOn([Laptop, SecondScreen]));
        Assert.False(docked.LandsOn([Laptop]));
    }

    [Fact]
    public void Overlapping_a_screen_is_not_the_same_as_being_reachable_on_it()
    {
        // A window whose bottom edge clips the top of a screen overlaps it and is still ungrabbable,
        // so the test is against the strip the window is dragged by rather than the whole rectangle.
        var underTheTopEdge = On(new Rectangle(400, -540, 1040, 560));
        Assert.False(underTheTopEdge.LandsOn([Laptop]));

        // And a sliver at the right edge is not enough to take hold of either.
        var almostOff = On(new Rectangle(1880, 200, 1040, 560));
        Assert.False(almostOff.LandsOn([Laptop]));

        var reachable = On(new Rectangle(1600, 200, 1040, 560));
        Assert.True(reachable.LandsOn([Laptop]));
    }

    [Fact]
    public void Nothing_remembered_is_not_a_rectangle_at_the_origin()
    {
        // A default record has a zero size, and 0,0,0,0 intersects the primary screen. Without the
        // size check every first run would "restore" to a window with no width.
        Assert.False(new WindowMemory().LandsOn([Laptop]));
        Assert.False(On(new Rectangle(100, 100, 0, 560)).LandsOn([Laptop]));
    }

    // ---- maximised is a state, not a rectangle the size of the screen -------------------------------

    [Fact]
    public void A_maximised_window_keeps_the_rectangle_it_restores_to()
    {
        // The usual defect is saving the maximised bounds as the size. Un-maximising then gives back a
        // window welded to whichever monitor it was last maximised on, and the size the user actually
        // chose is gone. The restore rectangle is what is checked and what is written.
        var maximised = On(new Rectangle(2100, 300, 1040, 560)) with { Maximised = true };

        Assert.True(maximised.Maximised);
        Assert.Equal(1040, maximised.Width);
        Assert.False(maximised.LandsOn([Laptop]));
    }

    // ---- the tab ------------------------------------------------------------------------------------

    [Fact]
    public void The_destination_survives_a_round_trip_and_defaults_to_containers()
    {
        var file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "window.json");
        try
        {
            Assert.Equal("Containers", WindowMemory.FirstDestination);
            Assert.Equal(WindowMemory.FirstDestination, new WindowMemory().Destination);

            var saved = On(new Rectangle(120, 80, 1200, 700)) with
            {
                Destination = "Images",
                Maximised = true,
                LogWidth = 1400,
                LogHeight = 900,
            };
            saved.Write(file);

            var read = WindowMemory.Read(file);

            Assert.Equal(saved, read);
        }
        finally
        {
            var directory = Path.GetDirectoryName(file)!;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void A_file_that_is_missing_or_damaged_reads_as_nothing_remembered()
    {
        // A preference file truncated by a power cut is not a reason to refuse to show a window, so
        // every failure is the same answer: open where a window with no history opens.
        var missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        Assert.Null(WindowMemory.Read(missing));

        var damaged = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.WriteAllText(damaged, "{\"Left\": 12, \"Top\":");
        try
        {
            Assert.Null(WindowMemory.Read(damaged));
        }
        finally
        {
            File.Delete(damaged);
        }
    }

    // ---- the save is guarded by the same rule as the restore ----------------------------------------

    [Fact]
    public void A_window_nobody_could_reach_is_not_written_down()
    {
        // --capture-window shows the window at -32000 with no desktop under it, and a screenshot run
        // must not overwrite the rectangle and the tab a person chose with the render harness's own.
        // Found by running the capture verb: it left Left=-32768 and Destination=Images behind.
        var offTheDesktop = On(new Rectangle(-32000, -32000, 1040, 560));
        Assert.False(offTheDesktop.LandsOn([Laptop, SecondScreen]));

        var recall = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src/DockerDesk.Tray/Ui/WindowRecall.cs"));
        var remember = recall[recall.IndexOf("private void Remember()", StringComparison.Ordinal)..];

        // The check comes before the write, not after it.
        var guard = remember.IndexOf("LandsOn", StringComparison.Ordinal);
        var write = remember.IndexOf(".Write(", StringComparison.Ordinal);
        Assert.True(guard > 0 && write > guard, "the save writes without checking where the window is");
    }

    // ---- where it is kept ---------------------------------------------------------------------------

    [Fact]
    public void It_lives_beside_everything_else_this_tool_owns()
    {
        // No settings system of its own: a handful of values in the root this application already
        // has, and not one of the directories Create() makes — its absence is the first run.
        var paths = new EnginePaths(@"C:\somewhere\DockerDesk");

        Assert.Equal(@"C:\somewhere\DockerDesk\window.json", paths.WindowState);
    }
}
