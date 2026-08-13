using System.Text.RegularExpressions;
using DockerDesk.Core.Engine;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// One meaning, one declaration (DD34).
/// </summary>
/// <remarks>
/// The colour was the sharp case: <c>#E5484D</c> means <em>the engine refused, or this is stderr</em>,
/// it was written four times across two files, and none of the four was pinned. These are the pins —
/// not on the value, which nobody argues about, but on there being one of it.
/// </remarks>
public sealed partial class PaletteTests
{
    private static string RepositoryFile(string name)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null)
        {
            var candidate = System.IO.Path.Combine(here.FullName, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            here = here.Parent;
        }

        throw new InvalidOperationException(name + " was not found");
    }

    private static IEnumerable<string> Markup() =>
        new DirectoryInfo(System.IO.Path.GetDirectoryName(RepositoryFile("agent-budget.json"))!)
            .GetDirectories("src").Single()
            .GetFiles("*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains(@"\obj\", StringComparison.Ordinal))
            .Select(f => f.FullName);

    [GeneratedRegex(@"#[0-9A-Fa-f]{6,8}\b")]
    private static partial Regex HexColour();

    [Fact]
    public void No_markup_declares_a_colour_of_its_own()
    {
        // The whole of L1, as a check a fifth `#E5484D` cannot get past. Markup reaches a value with
        // {x:Static ui:Palette...}, so there is nothing here for a second spelling to disagree with.
        foreach (var file in Markup())
        {
            var offenders = HexColour().Matches(File.ReadAllText(file));
            Assert.True(
                offenders.Count == 0,
                $"{System.IO.Path.GetFileName(file)} declares {string.Join(", ", offenders.Select(m => m.Value))}. "
                + "Colours live in Ui/Palette.cs and markup reaches them with {x:Static}.");
        }
    }

    [Fact]
    public void The_tray_icon_and_the_window_dot_are_the_same_colour_for_every_state()
    {
        // Two edges of one value, which is the thing that was actually broken: GDI+ held the bytes and
        // WPF got them by a hand conversion at one call site, so the two could drift and only one of
        // them was ever looked at.
        foreach (var state in Enum.GetValues<EngineState>())
        {
            var gdi = Tray.StateIcon.ColourFor(state);
            var brush = (System.Windows.Media.SolidColorBrush)Tray.Ui.Palette.EngineBrush(state);

            Assert.Equal(gdi.R, brush.Color.R);
            Assert.Equal(gdi.G, brush.Color.G);
            Assert.Equal(gdi.B, brush.Color.B);
        }
    }

    [Fact]
    public void Every_brush_the_palette_hands_out_is_frozen()
    {
        // They are shared across windows and never mutated, and an unfrozen one pays a lock on every
        // draw — claude-tray's rule, and the reason these are fields rather than properties.
        Assert.True(Tray.Ui.Palette.DangerBrush.IsFrozen);
        foreach (var state in Enum.GetValues<EngineState>())
        {
            Assert.True(Tray.Ui.Palette.EngineBrush(state).IsFrozen);
        }
    }

    [Fact]
    public void Only_one_place_makes_the_application()
    {
        // Two did, and both spelled out the shutdown mode: the tray opening its window and
        // --capture-window rendering one off-screen. A capture that resolved different chrome from the
        // window a user opens would be a picture of something nobody runs.
        var source = new DirectoryInfo(
            System.IO.Path.GetDirectoryName(RepositoryFile("agent-budget.json"))!)
            .GetDirectories("src").Single();

        foreach (var file in source.GetFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains(@"\obj\", StringComparison.Ordinal)
                     && !f.FullName.EndsWith("Theme.cs", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain(
                "new System.Windows.Application",
                File.ReadAllText(file.FullName),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_hex_the_palette_prints_is_the_bytes_it_holds()
    {
        Assert.Equal("#E5484D", Tray.Ui.Palette.DangerHex);
        Assert.Equal(Tray.Ui.Palette.DangerR, Tray.Ui.Palette.Danger.R);
        Assert.Equal(Tray.Ui.Palette.DangerG, Tray.Ui.Palette.Danger.G);
        Assert.Equal(Tray.Ui.Palette.DangerB, Tray.Ui.Palette.Danger.B);
    }
}
