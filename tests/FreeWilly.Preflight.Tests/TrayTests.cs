using FreeWilly.Core.Api;
using FreeWilly.Core.Engine;
using FreeWilly.Tray;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>Records what it was asked to launch instead of launching it.</summary>
internal sealed class FakeLauncher(string? failure = null) : IProcessLauncher
{
    internal List<(string File, string Arguments)> Launched { get; } = [];

    public string? Launch(string fileName, string arguments)
    {
        Launched.Add((fileName, arguments));
        return failure;
    }
}

/// <summary>
/// The icon and the lifetime. The icon tests assert on ink rather than colour, because a state a
/// reader can only get from hue is a state some readers cannot get at all.
/// </summary>
public sealed class TrayTests
{
    // ---- the icon ---------------------------------------------------------------------------

    [Fact]
    public void The_three_states_are_told_apart_with_no_colour_at_all()
    {
        using var running = StateIcon.Draw(EngineState.Running, 32);
        using var starting = StateIcon.Draw(EngineState.Starting, 32);
        using var stopped = StateIcon.Draw(EngineState.Stopped, 32);

        var filled = StateIcon.InkedPixels(running);
        var gapped = StateIcon.InkedPixels(starting);
        var ring = StateIcon.InkedPixels(stopped);

        // The centre is the discriminator that is about shape rather than about how much ink there
        // happens to be: a disc is painted through the middle and a ring is hollow, whatever the
        // stroke width. Measured at 32px the areas are 602 and 341, which a threshold could also
        // catch — but only until somebody changes the pen.
        Assert.True(Inked(running, 16, 16), "a filled disc should be painted at its centre");
        Assert.False(Inked(stopped, 16, 16), "a ring should be hollow at its centre");
        Assert.False(Inked(starting, 16, 16), "a gapped ring should be hollow at its centre");

        // And the two hollow ones differ by the gap — measured as the pixels the whole ring has and
        // the gapped one does not. `gapped < ring` alone is not this assertion: closing the arc to
        // 360 degrees left it one pixel smaller and the suite stayed green, which is a test that
        // would have let Starting and Stopped become the same picture.
        var gap = MissingFrom(stopped, starting);
        Assert.True(gap > ring / 5, $"the gap ({gap}) should be a visible slice of the ring ({ring})");
        Assert.True(gapped > ring / 2, $"a gapped ring ({gapped}) should still read as a ring ({ring})");
        Assert.True(filled > ring, $"a disc ({filled}) should carry more ink than a ring ({ring})");
    }

    [Theory]
    [InlineData(EngineState.Running)]
    [InlineData(EngineState.Starting)]
    [InlineData(EngineState.Stopped)]
    public void Every_state_draws_something_at_the_size_a_taskbar_uses(EngineState state)
    {
        using var drawn = StateIcon.Draw(state);

        Assert.Equal(16, drawn.Width);
        Assert.True(StateIcon.InkedPixels(drawn) > 10, "an icon nobody can see is not an icon");
    }

    [Fact]
    public void The_three_states_are_also_three_colours()
    {
        var colours = new[] { EngineState.Running, EngineState.Starting, EngineState.Stopped }
            .Select(StateIcon.ColourFor)
            .ToList();

        Assert.Equal(3, colours.Distinct().Count());
    }

    [Fact]
    public void The_tooltip_names_the_state_in_words()
    {
        // The section's own reason for a tooltip: sixteen pixels is not always enough.
        Assert.Contains("running", StateIcon.TooltipFor(EngineState.Running), StringComparison.Ordinal);
        Assert.Contains("starting", StateIcon.TooltipFor(EngineState.Starting), StringComparison.Ordinal);
        Assert.Contains("stopped", StateIcon.TooltipFor(EngineState.Stopped), StringComparison.Ordinal);
    }

    [Fact]
    public void An_icon_smaller_than_a_state_can_be_drawn_in_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => StateIcon.Draw(EngineState.Running, 4));

    private static bool Inked(System.Drawing.Bitmap bitmap, int x, int y) =>
        bitmap.GetPixel(x, y).A > 128;

    /// <summary>Pixels painted in <paramref name="whole"/> and not in <paramref name="cut"/>.</summary>
    private static int MissingFrom(System.Drawing.Bitmap whole, System.Drawing.Bitmap cut)
    {
        var missing = 0;
        for (var x = 0; x < whole.Width; x++)
        {
            for (var y = 0; y < whole.Height; y++)
            {
                if (Inked(whole, x, y) && !Inked(cut, x, y))
                {
                    missing++;
                }
            }
        }

        return missing;
    }

    // ---- what the indicator says --------------------------------------------------------------

    [Fact]
    public void The_engine_is_Running_exactly_when_the_event_stream_is_connected() =>
        Assert.Equal(EngineState.Running, TrayState.For(EventStreamState.Watching, false));

    [Theory]
    [InlineData(EventStreamState.Connecting)]
    [InlineData(EventStreamState.Reconnecting)]
    [InlineData(EventStreamState.Stopped)]
    public void A_stream_that_is_not_connected_reads_as_Stopped_when_nobody_asked_for_a_start(
        EventStreamState stream) =>
        Assert.Equal(EngineState.Stopped, TrayState.For(stream, startRequested: false));

    [Theory]
    [InlineData(EventStreamState.Connecting)]
    [InlineData(EventStreamState.Reconnecting)]
    public void The_same_stream_reads_as_Starting_once_somebody_asked(EventStreamState stream) =>
        Assert.Equal(EngineState.Starting, TrayState.For(stream, startRequested: true));

    [Fact]
    public void A_start_that_landed_is_Running_and_not_still_Starting() =>
        Assert.Equal(EngineState.Running, TrayState.For(EventStreamState.Watching, startRequested: true));

    // ---- the lifetime -------------------------------------------------------------------------

    [Fact]
    public void Starting_launches_the_engine_in_a_process_of_its_own()
    {
        var launcher = new FakeLauncher();
        var holder = new EngineHolder(@"C:\x\dockerdesk-engine.exe", launcher);

        holder.Start();

        Assert.Equal((@"C:\x\dockerdesk-engine.exe", "--run"), launcher.Launched[0]);
    }

    [Fact]
    public void Stopping_goes_through_the_engine_rather_than_killing_a_process()
    {
        // --stop terminates the distribution, so it reaches an engine this tray never started —
        // one left running from a terminal, or by a previous tray.
        var launcher = new FakeLauncher();
        var holder = new EngineHolder(@"C:\x\dockerdesk-engine.exe", launcher);

        holder.Stop();

        Assert.Equal((@"C:\x\dockerdesk-engine.exe", "--stop"), launcher.Launched[0]);
    }

    [Fact]
    public void An_engine_that_cannot_be_started_is_reported_rather_than_thrown()
    {
        // This crashed the tray from a click handler: the engine was simply not beside it in a dev
        // build, Process.Start threw, and the icon vanished. An icon that disappears when somebody
        // presses its own menu item is worse than any message it could have shown instead.
        var holder = new EngineHolder(@"C:\x\dockerdesk-engine.exe", new FakeLauncher("not there"));

        Assert.Equal("not there", holder.Start());
        Assert.Equal("not there", holder.Stop());
    }

    [Fact]
    public void A_start_that_worked_reports_nothing() =>
        Assert.Null(new EngineHolder(@"C:\x\dockerdesk-engine.exe", new FakeLauncher()).Start());

    [Fact]
    public void The_real_launcher_names_the_missing_file_instead_of_throwing()
    {
        var missing = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"dockerdesk-absent-{Guid.NewGuid():N}.exe");

        var failure = new DetachedLauncher().Launch(missing, "--run");

        Assert.NotNull(failure);
        Assert.Contains("is not in", failure, StringComparison.Ordinal);
        Assert.Contains(System.IO.Path.GetTempPath().TrimEnd('\\'), failure, StringComparison.Ordinal);
    }

    [Fact]
    public void The_engine_is_this_executable_and_not_a_file_beside_it()
    {
        // DD14: one .exe. The engine used to be a second file expected in the same folder, and a
        // copy that arrived without it had a Start engine menu item that could only apologise. What
        // this asserts is that the holder drives something that exists — itself.
        var path = EngineHolder.ThisProcess();

        Assert.True(System.IO.Path.IsPathRooted(path));
        Assert.True(System.IO.File.Exists(path), $"{path} should be the running executable");
    }

    [Fact]
    public void Both_verbs_go_to_the_same_executable()
    {
        var launcher = new FakeLauncher();
        var holder = new EngineHolder(EngineHolder.ThisProcess(), launcher);

        holder.Start();
        holder.Stop();

        Assert.Equal(holder.EnginePath, launcher.Launched[0].File);
        Assert.Equal(holder.EnginePath, launcher.Launched[1].File);
    }

    // ---- what the shell is told at add time (DD82) --------------------------------------------

    [Fact]
    public void The_icon_and_its_tooltip_are_set_before_the_entry_becomes_visible()
    {
        // Asserted on the source because that is where the fact lives: setting Visible is what emits
        // the shell's notify-add, and Windows persists whatever that one call carried. Nothing on
        // NotifyIcon reports what was sent, and the consequence — an overflow flyout entry with no
        // name — is only visible in the registry of a machine that has run it.
        var source = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "src/FreeWilly.Tray/Program.cs"));

        var shown = source.IndexOf("Show(EngineState.Stopped);", StringComparison.Ordinal);
        var visible = source.IndexOf("_icon.Visible = true;", StringComparison.Ordinal);

        // Both have to be there, or this passes by finding neither.
        Assert.True(shown >= 0, "the tray no longer draws an initial state");
        Assert.True(visible >= 0, "the tray no longer makes its icon visible");
        Assert.True(
            shown < visible,
            "the icon becomes visible before it has an image and a tooltip, so the shell persists "
            + "an empty one and the overflow flyout names nothing (DD82)");
    }

    /// <summary>The repository root, found by walking up from the test binary.</summary>
    private static string RepositoryRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "FreeWilly.slnx")))
        {
            here = here.Parent;
        }

        Assert.True(here is not null, "the repository root was not found above the test binaries");
        return here!.FullName;
    }
}
