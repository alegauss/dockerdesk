using FreeWilly.Tray.Cli;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// Which face of the one executable a command line reaches (DD14).
/// </summary>
/// <remarks>
/// This is the seam that replaced three executables with one, so it is the seam where a mistake is
/// silent: a verb routed to the tray shows an icon and prints nothing, and a verb routed to a
/// console surface that the tray also matches would flash a window instead of answering. Neither
/// fails loudly, and both are one line of a switch.
/// </remarks>
public sealed class CommandLineTests
{
    [Fact]
    public void No_arguments_is_the_tray_and_no_window()
    {
        var route = CommandLine.Of([]);

        Assert.Equal(Surface.Tray, route.Surface);
        Assert.False(route.OpenWindow);
    }

    [Theory]
    [InlineData("--window")]
    [InlineData("--WINDOW")]
    [InlineData("--Window")]
    public void The_window_verb_is_the_tray_with_the_window_open(string spelling)
    {
        // Case-insensitive because this is the argument a Windows shortcut carries, and a shortcut
        // is edited by hand in a properties dialog.
        var route = CommandLine.Of([spelling]);

        Assert.Equal(Surface.Tray, route.Surface);
        Assert.True(route.OpenWindow);
    }

    [Fact]
    public void The_preflight_verb_reaches_the_preflight_without_being_passed_on_to_it()
    {
        // The preflight refuses arguments it does not have, and --preflight is one of them: leaving
        // the verb in the list would make `--preflight --json` exit 2 instead of printing a report.
        var route = CommandLine.Of(["--preflight", "--json"]);

        Assert.Equal(Surface.Preflight, route.Surface);
        Assert.Equal(["--json"], route.Arguments);
    }

    [Fact]
    public void The_preflight_verb_on_its_own_passes_nothing_on()
    {
        var route = CommandLine.Of(["--preflight"]);

        Assert.Equal(Surface.Preflight, route.Surface);
        Assert.Empty(route.Arguments);
    }

    [Theory]
    [InlineData("--plan")]
    [InlineData("--acquire")]
    [InlineData("--provision")]
    [InlineData("--run")]
    [InlineData("--stop")]
    [InlineData("--status")]
    [InlineData("--api")]
    [InlineData("--watch")]
    [InlineData("--autostart")]
    public void Every_engine_verb_reaches_the_engine_with_the_verb_still_in_its_hand(string verb)
    {
        // The verb stays: the engine switches on args[0], unlike the preflight.
        var route = CommandLine.Of([verb]);

        Assert.Equal(Surface.Engine, route.Surface);
        Assert.Equal([verb], route.Arguments);
    }

    [Fact]
    public void The_autostart_value_travels_with_it()
    {
        var route = CommandLine.Of(["--autostart", "on"]);

        Assert.Equal(Surface.Engine, route.Surface);
        Assert.Equal(["--autostart", "on"], route.Arguments);
    }

    [Fact]
    public void The_verb_the_tray_launches_for_itself_is_one_of_them() =>
        // The tray starts the engine as this same executable with --run (EngineHolder). A rename on
        // one side of that and the Start engine menu item silently opens a second tray icon.
        Assert.Equal(Surface.Engine, CommandLine.Of(["--run"]).Surface);

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Help_is_a_console_surface_and_not_a_tray(string spelling) =>
        Assert.Equal(Surface.Help, CommandLine.Of([spelling]).Surface);

    [Fact]
    public void The_version_verb_is_its_own_surface() =>
        Assert.Equal(Surface.Version, CommandLine.Of(["--version"]).Surface);

    [Theory]
    [InlineData("--nonsense")]
    [InlineData("-x")]
    [InlineData("provision")]
    [InlineData("--Run")]
    public void An_argument_this_executable_does_not_have_is_refused(string argument) =>
        // --Run among them, deliberately: the engine's own switch is case-sensitive, so routing a
        // differently-cased verb to it would reach the engine's "unknown argument" rather than this
        // one, and the exit code would be right for the wrong reason.
        Assert.Equal(Surface.Unknown, CommandLine.Of([argument]).Surface);

    [Fact]
    public void The_window_verb_mixed_with_anything_else_is_refused() =>
        // A tray cannot also be a console verb, and guessing which half was meant is worse than
        // saying so.
        Assert.Equal(Surface.Unknown, CommandLine.Of(["--window", "--status"]).Surface);

    [Fact]
    public void Every_verb_that_routes_somewhere_is_in_the_help_text()
    {
        // The help used to be two texts, one in the engine and one in the preflight, and a verb
        // documented in neither is a verb nobody can find. There is one text now; this is what
        // keeps it honest when a verb is added to the router.
        var help = CommandLine.HelpText;

        foreach (var verb in CommandLine.EngineVerbs)
        {
            Assert.Contains(verb, help, StringComparison.Ordinal);
        }

        Assert.Contains(CommandLine.PreflightVerb, help, StringComparison.Ordinal);
        Assert.Contains(CommandLine.WindowVerb, help, StringComparison.Ordinal);
        Assert.Contains("--version", help, StringComparison.Ordinal);
        Assert.Contains("--help", help, StringComparison.Ordinal);
        Assert.Contains(CommandLine.ExecutableName, help, StringComparison.Ordinal);
    }

    [Fact]
    public void A_null_command_line_is_a_defect_here_rather_than_a_route() =>
        Assert.Throws<ArgumentNullException>(() => CommandLine.Of(null!));
}
