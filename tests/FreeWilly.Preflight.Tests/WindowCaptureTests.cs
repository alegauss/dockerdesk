using FreeWilly.Tray.Cli;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The capture verb's command line (DD22).
/// </summary>
/// <remarks>
/// The render itself needs a WPF <c>Application</c> and a window, which is a thing to run rather than
/// a thing to assert — it was verified by running it and looking at the PNG, which is the only check
/// that catches what a capture is for. Two defects were found that way and neither would have failed a
/// test: the content's margin clipped "About" off the right edge, and the header row came back blank
/// because nothing had asked the window to read the engine.
///
/// What is asserted here is the part around it that is pure: which surface the verb reaches, and that
/// a path is not confused with a flag.
/// </remarks>
public sealed class WindowCaptureTests
{
    [Fact]
    public void The_capture_verb_reaches_its_own_surface_and_keeps_its_arguments()
    {
        var route = CommandLine.Of(["--capture-window", "out.png"]);

        Assert.Equal(Surface.CaptureWindow, route.Surface);
        Assert.Equal(["out.png"], route.Arguments);
    }

    [Fact]
    public void A_tab_travels_with_it()
    {
        var route = CommandLine.Of(["--capture-window", "out.png", "Volumes"]);

        Assert.Equal(Surface.CaptureWindow, route.Surface);
        Assert.Equal(["out.png", "Volumes"], route.Arguments);
    }

    [Fact]
    public void It_is_not_the_tray_and_it_is_not_the_window_verb()
    {
        // --window opens a visible window; --capture-window opens nothing anybody can see. Routing one
        // to the other would either show a window to a script or write nothing for a person.
        Assert.Equal(Surface.Tray, CommandLine.Of(["--window"]).Surface);
        Assert.NotEqual(Surface.Tray, CommandLine.Of(["--capture-window", "out.png"]).Surface);
    }

    [Fact]
    public void The_verb_on_its_own_is_refused_rather_than_writing_somewhere_chosen_for_the_caller()
    {
        // Reaches the surface, which then refuses: there is no default path, because a capture verb
        // that invents one writes a file nobody asked for.
        var route = CommandLine.Of(["--capture-window"]);

        Assert.Equal(Surface.CaptureWindow, route.Surface);
        Assert.Empty(route.Arguments);
        Assert.Equal(2, WindowCapture.Run(route.Arguments));
    }

    [Fact]
    public void A_flag_where_the_path_should_be_is_refused()
    {
        // Borrowed from claude-tray, where taking a positional by index wrote a file named after the
        // flag next door. Refused before any window exists, so nothing is created either.
        Assert.Equal(2, WindowCapture.Run(["--json"]));
        Assert.Equal(2, WindowCapture.Run(["-x", "out.png"]));
    }

    [Fact]
    public void More_than_a_path_and_a_tab_is_refused() =>
        Assert.Equal(2, WindowCapture.Run(["out.png", "Volumes", "extra"]));

    [Fact]
    public void The_verb_is_in_the_help_text()
    {
        // The one help text there is, so a verb added to the router is a verb somebody can find.
        Assert.Contains(CommandLine.CaptureWindowVerb, CommandLine.HelpText, StringComparison.Ordinal);
    }

    [Fact]
    public void The_capture_verb_is_not_one_of_the_engine_verbs() =>
        // The engine verbs are matched as a set; a capture landing in it would start an engine.
        Assert.DoesNotContain(CommandLine.CaptureWindowVerb, CommandLine.EngineVerbs);
}
