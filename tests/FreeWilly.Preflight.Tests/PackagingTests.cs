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
    public void The_AppId_is_the_one_already_on_machines_and_never_changes()
    {
        // DD57's decision, and the one thing in that file that must survive every future tidy: Inno
        // identifies a product by AppId and by nothing else. The old name is inside this GUID and
        // stays there, because it is an identity rather than a spelling. Change it and a machine
        // carrying the published build ends up with two entries in Add/Remove Programs, two Run
        // values and two roots — and the old uninstaller then offers to delete the engine root the
        // new install is using.
        Assert.Contains(
            "AppId={{6B0E4D2A-9C77-4A31-8F5E-DOCKERDESK001}",
            InstallerScript(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_old_autostart_value_is_removed_rather_than_left_running()
    {
        // The other half of DD57, and a regression DD54's rename introduced: the Run value's name
        // moved with the sweep, so an upgraded machine kept a `DockerDesk` value pointing at an
        // executable this build no longer produces. Logon would fail silently while the window
        // reported autostart as off.
        var script = InstallerScript();

        Assert.Contains("#define LegacyRunValue \"DockerDesk\"", script, StringComparison.Ordinal);
        Assert.Contains(
            "ValueName: \"{#LegacyRunValue}\"; Flags: deletevalue uninsdeletevalue",
            script,
            StringComparison.Ordinal);

        // And the runtime owns the same pair, so turning it on or off from the window converges too.
        Assert.Equal("FreeWilly", Core.Engine.Autostart.EntryName);
        Assert.Equal("DockerDesk", Core.Engine.Autostart.LegacyEntryName);
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
