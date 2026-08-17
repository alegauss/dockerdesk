using FreeWilly.Core.Engine;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// Whether the engine comes up with the tray, and the file that remembers it (DD135).
/// </summary>
public sealed class EngineOnLaunchTests
{
    /// <summary>A path in a directory of this test's own, removed when it is done.</summary>
    private sealed class Scratch : IDisposable
    {
        private readonly string _directory =
            Path.Combine(Path.GetTempPath(), $"freewilly-onlaunch-{Guid.NewGuid():N}");

        internal string File => Path.Combine(_directory, "settings.json");

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }
            }
            catch (IOException)
            {
                // A temp directory that outlives the run costs nothing worth failing a suite over.
            }
        }
    }

    [Fact]
    public void An_install_nobody_has_changed_anything_on_starts_the_engine()
    {
        // The decision the user made, asserted rather than left to a literal somewhere. Shipping
        // this off would make the setting invisible: nobody goes looking in a menu for a box that
        // matches what already happens.
        using var scratch = new Scratch();

        Assert.True(EngineOnLaunch.Read(scratch.File).StartWithTheTray);
        Assert.True(EngineOnLaunch.ShipsOn);
    }

    [Fact]
    public void Turning_it_off_survives_the_next_launch()
    {
        // The whole reason it is a setting and not a hard-coded start. If this does not round-trip,
        // the non-goal it sits beside has been quietly deleted rather than kept.
        using var scratch = new Scratch();
        new EngineOnLaunch { StartWithTheTray = false }.Write(scratch.File);

        Assert.False(EngineOnLaunch.Read(scratch.File).StartWithTheTray);
    }

    [Fact]
    public void Turning_it_back_on_survives_too()
    {
        using var scratch = new Scratch();
        new EngineOnLaunch { StartWithTheTray = false }.Write(scratch.File);
        new EngineOnLaunch { StartWithTheTray = true }.Write(scratch.File);

        Assert.True(EngineOnLaunch.Read(scratch.File).StartWithTheTray);
    }

    [Fact]
    public void A_file_that_cannot_be_read_answers_with_the_default_rather_than_throwing()
    {
        // A preference file truncated by a power cut is not a reason to refuse to start an engine,
        // and this runs in a constructor where throwing takes the tray icon with it.
        using var scratch = new Scratch();
        Directory.CreateDirectory(Path.GetDirectoryName(scratch.File)!);
        File.WriteAllText(scratch.File, "{ this is not json");

        Assert.True(EngineOnLaunch.Read(scratch.File).StartWithTheTray);
    }

    [Fact]
    public void Writing_where_nothing_exists_yet_creates_the_directory()
    {
        // The settings file lives beside the install and is not created by EnginePaths.Create, so
        // the first write is also the first time its folder may need to exist.
        using var scratch = new Scratch();

        new EngineOnLaunch { StartWithTheTray = false }.Write(scratch.File);

        Assert.True(File.Exists(scratch.File));
    }

    [Fact]
    public void It_is_kept_apart_from_the_window_and_from_the_run_key()
    {
        // Three different questions that all sound like "does it start": where the window was, what
        // logon runs, and what opening the tray does. DD97 already paid for conflating two of them.
        var paths = new EnginePaths();

        Assert.NotEqual(paths.WindowState, paths.Settings);
        Assert.EndsWith("settings.json", paths.Settings, StringComparison.Ordinal);
    }
}
