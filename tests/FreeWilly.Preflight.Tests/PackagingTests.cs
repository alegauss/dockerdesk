using System.Reflection;
using FreeWilly.Core.Licensing;
using FreeWilly.Tray.Cli;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The chain a release travels along (DD14): one version, stated once, and one file name.
/// </summary>
/// <remarks>
/// Every link here is a string that has to agree with a string somewhere else, and none of them
/// fails at compile time. The version is set in Directory.Build.props, compiled into the assembly,
/// read back off the published .exe by build\installer.iss, and shown by Windows in Add/Remove
/// Programs; the file name is set by AssemblyName, typed by a person, and written into the
/// installer's shortcuts. A break anywhere along either is found by running an installer, which is
/// the most expensive place to find anything.
/// </remarks>
public sealed class PackagingTests
{
    private static readonly Assembly Shipped = typeof(CommandLine).Assembly;

    [Fact]
    public void The_name_the_help_prints_is_the_name_the_build_produces()
    {
        // AssemblyName in FreeWilly.Tray.csproj, against the name every message and the installer
        // spell out. The assembly is a .dll here and an .exe when published, so compare the stem.
        var built = System.IO.Path.GetFileNameWithoutExtension(Shipped.Location);

        Assert.Equal(
            built,
            System.IO.Path.GetFileNameWithoutExtension(CommandLine.ExecutableName));
        Assert.Equal(".exe", System.IO.Path.GetExtension(CommandLine.ExecutableName));
    }

    [Fact]
    public void The_product_version_carries_no_commit_suffix()
    {
        // IncludeSourceRevisionInInformationalVersion=false in Directory.Build.props. Without it the
        // SDK appends "+<commit>", installer.iss reads that whole string out of the .exe with
        // GetStringFileInfo, and the version Windows shows in Add/Remove Programs has a git hash in
        // it. BuildVersion trims the suffix for its own display, so it cannot catch this.
        var informational = Shipped
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.NotNull(informational);
        Assert.DoesNotContain('+', informational);
    }

    [Fact]
    public void The_version_is_one_a_person_can_read_out_of_a_bug_report()
    {
        var current = BuildVersion.Current;

        Assert.NotEqual("0.0.0", current);
        Assert.True(
            Version.TryParse(current, out var parsed),
            $"{current} should parse as a version");
        Assert.Equal(current, parsed!.ToString());
    }

    /// <summary>The installer script, read as text — the only way to assert on Inno's decisions.</summary>
    private static string InstallerScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "FreeWilly.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "the repository root was not found above the test binaries");
        return File.ReadAllText(Path.Combine(directory!.FullName, "build", "installer.iss"));
    }

    [Fact]
    public void The_AppId_is_pinned_here_so_a_future_tidy_cannot_move_it()
    {
        // Inno identifies a product by AppId and by nothing else, so from the first release onward
        // this is an identity rather than a spelling. Change it and a machine carrying the published
        // build ends up with two entries in Add/Remove Programs, two Run values and two roots — and
        // the old uninstaller then offers to delete the engine root the new install is using.
        //
        // DD86 changed it once, which was only free because nothing has been released. The literal
        // is repeated here rather than read from the script so that the next change has to be
        // deliberate in two files: this test is the record that it is not a name to sweep.
        Assert.Contains(
            "AppId={{6B0E4D2A-9C77-4A31-8F5E-FREEWILLY0001}",
            InstallerScript(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_tray_and_the_engine_start_at_logon_under_names_that_differ()
    {
        // DD97, and the assertion is inverted from what stood here. It used to require the two to
        // be spelled the SAME, reasoning that one value keeps the window and the uninstaller from
        // touching two entries. That holds for one feature and is exactly backwards for two: the
        // installer's checkbox writes `--tray` and `--autostart on` wrote `--run` over it, so
        // whichever ran last won. Ticking the box then turning the engine on stopped the tray
        // appearing, and turning the engine off deleted the box's entry outright.
        var script = InstallerScript();

        Assert.DoesNotContain("LegacyRunValue", script, StringComparison.Ordinal);
        Assert.NotEqual(
            Core.Engine.Autostart.TrayEntryName,
            Core.Engine.Autostart.EngineEntryName);

        // Each name is spelled in both files, which is the drift nothing else would notice.
        Assert.Contains(
            $"ValueName: \"{Core.Engine.Autostart.EngineEntryName}\"",
            script,
            StringComparison.Ordinal);
        Assert.Equal("FreeWilly", Core.Engine.Autostart.TrayEntryName);
    }

    [Fact]
    public void The_uninstaller_takes_the_engine_entry_without_the_installer_ever_writing_it()
    {
        // Two settings mean two values, and only one of them is the installer's to create. Leaving
        // the other behind is a Run entry pointing at an executable that has been deleted — the
        // exact state DD57 had to clean up once already.
        //
        // `ValueType: none` is what makes it delete-on-uninstall and touch nothing on install.
        // Writing it properly would turn the engine autostart on for everyone, and off-by-default
        // is not a preference here: it is the complaint about Docker Desktop that this answers.
        var engineValue = Assert.Single(
            InstallerScript().Split('\n'),
            line => line.Contains(
                $"ValueName: \"{Core.Engine.Autostart.EngineEntryName}\"", StringComparison.Ordinal));

        Assert.Contains("ValueType: none", engineValue, StringComparison.Ordinal);
        Assert.DoesNotContain("ValueData:", engineValue, StringComparison.Ordinal);
    }

    [Fact]
    public void Turning_the_engine_autostart_off_leaves_the_trays_entry_alone()
    {
        // The failure itself, driven rather than restated. `Disable` removes rather than blanks,
        // deliberately, so before DD97 turning the engine off silently undid a box somebody had
        // ticked in the installer — and turning it ON overwrote it with `--run`, so the tray
        // stopped appearing at logon.
        //
        // A scratch key, never the real Run: this writes and deletes, and the machine running the
        // suite must not be what it experiments on.
        var scratch = $@"Software\FreeWilly\Tests\{Guid.NewGuid():N}";
        try
        {
            var tray = new Core.Engine.Autostart(
                @"""C:\x\FreeWilly.exe"" --tray", Core.Engine.Autostart.TrayEntryName, scratch);
            var engine = new Core.Engine.Autostart(
                @"""C:\x\FreeWilly.exe"" --run", Core.Engine.Autostart.EngineEntryName, scratch);

            tray.Enable();
            engine.Enable();

            // Both, at once, which is the state that was unreachable before: one value could only
            // hold one of the two commands.
            Assert.True(tray.Enabled);
            Assert.True(engine.Enabled);
            Assert.EndsWith("--tray", tray.Registered!, StringComparison.Ordinal);
            Assert.EndsWith("--run", engine.Registered!, StringComparison.Ordinal);

            engine.Disable();

            Assert.False(engine.Enabled);
            Assert.True(tray.Enabled, "turning the engine off took the tray's entry with it");
            Assert.EndsWith("--tray", tray.Registered!, StringComparison.Ordinal);
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(scratch, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void Start_with_windows_asks_for_the_tray_alone()
    {
        // DD80 inverted the default: a bare launch opens the window, so the Run value has to say
        // it wants the tray on its own or every logon puts a window in somebody's face. The two
        // spellings are in two files and nothing else would notice them drifting apart.
        //
        // The line rather than the whole literal: Inno doubles its own quotes, so an exact match
        // asserts that escaping as much as it asserts the argument, and the argument is the claim.
        var runValue = Assert.Single(
            InstallerScript().Split('\n'),
            line => line.Contains("ValueData:", StringComparison.Ordinal)
                && line.Contains("MyAppExeName", StringComparison.Ordinal));

        Assert.EndsWith(
            $"{Tray.Cli.CommandLine.TrayOnlyVerb}\"; \\",
            runValue.TrimEnd('\r'),
            StringComparison.Ordinal);

        // And it really is silence: the flag the script writes has to be the one that means it.
        var route = Tray.Cli.CommandLine.Of([Tray.Cli.CommandLine.TrayOnlyVerb]);
        Assert.Equal(Tray.Cli.Surface.Tray, route.Surface);
        Assert.False(route.OpenWindow);
    }

    [Fact]
    public void The_version_the_assembly_states_is_the_one_the_installer_would_read() =>
        // GetStringFileInfo(PRODUCT_VERSION) reads the informational version, which is what
        // BuildVersion prints. Two ways to ask, and they have to agree or the installed version and
        // the About box disagree about the same build.
        Assert.Equal(
            Shipped.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
            BuildVersion.Current);
}
