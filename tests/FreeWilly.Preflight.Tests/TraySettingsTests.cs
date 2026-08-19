using FreeWilly.Core.Engine;
using FreeWilly.Core.Settings;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// What the user has decided about how this tool behaves, and the file that remembers it (DD135,
/// DD154).
/// </summary>
public sealed class TraySettingsTests
{
    /// <summary>A path in a directory of this test's own, removed when it is done.</summary>
    private sealed class Scratch : IDisposable
    {
        private readonly string _directory =
            Path.Combine(Path.GetTempPath(), $"freewilly-settings-{Guid.NewGuid():N}");

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

        Assert.True(TraySettings.Read(scratch.File).StartWithTheTray);
        Assert.True(TraySettings.EngineShipsOn);
    }

    [Fact]
    public void An_install_nobody_has_changed_anything_on_makes_no_release_request()
    {
        // The one default in this file that is a promise rather than a convenience (DD154). The site
        // says the only network traffic this project makes is the five pinned artefacts during a
        // provision the user asked for, and a check that shipped on would make that false on every
        // machine before anybody had chosen anything.
        using var scratch = new Scratch();

        Assert.False(TraySettings.Read(scratch.File).CheckForReleases);
        Assert.False(TraySettings.ReleaseCheckShipsOn);
    }

    [Fact]
    public void Turning_it_off_survives_the_next_launch()
    {
        // The whole reason it is a setting and not a hard-coded start. If this does not round-trip,
        // the non-goal it sits beside has been quietly deleted rather than kept.
        using var scratch = new Scratch();
        new TraySettings { StartWithTheTray = false }.Write(scratch.File);

        Assert.False(TraySettings.Read(scratch.File).StartWithTheTray);
    }

    [Fact]
    public void Turning_it_back_on_survives_too()
    {
        using var scratch = new Scratch();
        new TraySettings { StartWithTheTray = false }.Write(scratch.File);
        new TraySettings { StartWithTheTray = true }.Write(scratch.File);

        Assert.True(TraySettings.Read(scratch.File).StartWithTheTray);
    }

    [Fact]
    public void The_release_check_round_trips_on_its_own_terms()
    {
        using var scratch = new Scratch();
        new TraySettings { CheckForReleases = true }.Write(scratch.File);

        Assert.True(TraySettings.Read(scratch.File).CheckForReleases);
    }

    [Fact]
    public void Saving_one_setting_does_not_reset_the_other()
    {
        // This is why the type is named after the file rather than after either setting (DD154).
        // Write serialises the object it is called on, so two records over one path would each
        // round-trip only their own property — and every save would be the other one's reset.
        using var scratch = new Scratch();

        new TraySettings { StartWithTheTray = false, CheckForReleases = true }.Write(scratch.File);
        var read = TraySettings.Read(scratch.File);

        Assert.False(read.StartWithTheTray);
        Assert.True(read.CheckForReleases);

        // And the record's own copy semantics, which is what the menu hands back on a tick.
        (read with { CheckForReleases = false }).Write(scratch.File);
        var again = TraySettings.Read(scratch.File);

        Assert.False(again.StartWithTheTray);
        Assert.False(again.CheckForReleases);
    }

    [Fact]
    public void A_file_that_cannot_be_read_answers_with_the_defaults_rather_than_throwing()
    {
        // A preference file truncated by a power cut is not a reason to refuse to start an engine,
        // and this runs in a constructor where throwing takes the tray icon with it. It is also the
        // one path where the release check's default has to hold: a corrupt file must not be able to
        // turn outbound traffic on.
        using var scratch = new Scratch();
        Directory.CreateDirectory(Path.GetDirectoryName(scratch.File)!);
        File.WriteAllText(scratch.File, "{ this is not json");

        var read = TraySettings.Read(scratch.File);

        Assert.True(read.StartWithTheTray);
        Assert.False(read.CheckForReleases);
    }

    [Fact]
    public void A_file_written_before_DD154_keeps_its_answer_and_gains_the_new_default()
    {
        // What is actually on the machines this ships to: settings.json holding the one property
        // DD135 wrote. Renaming the type must not have renamed the property, or every install that
        // had turned the engine start off would silently get it back.
        using var scratch = new Scratch();
        Directory.CreateDirectory(Path.GetDirectoryName(scratch.File)!);
        File.WriteAllText(scratch.File, "{\n  \"StartWithTheTray\": false\n}");

        var read = TraySettings.Read(scratch.File);

        Assert.False(read.StartWithTheTray);
        Assert.False(read.CheckForReleases);
    }

    [Fact]
    public void Writing_where_nothing_exists_yet_creates_the_directory()
    {
        // The settings file lives beside the install and is not created by EnginePaths.Create, so
        // the first write is also the first time its folder may need to exist.
        using var scratch = new Scratch();

        new TraySettings { StartWithTheTray = false }.Write(scratch.File);

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
