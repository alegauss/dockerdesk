using FreeWilly.Core.Engine;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The root and the distribution name — the two names that are state on a machine rather than
/// text in a build.
/// </summary>
/// <remarks>
/// This file used to be twice this size and was about adoption: a root and a distribution resolved
/// between the current spelling and the one from before the rename, a machine holding both, a
/// directory that looked like an install and was not. DD86 removed all of it, because nothing ever
/// shipped under the old name and so no machine could be in any of those states — and a test for a
/// state that cannot occur is a guard for nothing. What is left is what still has to hold.
/// </remarks>
public sealed class EnginePathsTests
{
    private const string Local = @"C:\Users\someone\AppData\Local";
    private static readonly string Root = Path.Combine(Local, "FreeWilly");

    [Fact]
    public void The_names_are_the_ones_the_installer_and_wsl_will_actually_carry()
    {
        // Literals on both sides on purpose. Asserting the constants against themselves would stay
        // green through the one change that matters — a respelling — which is exactly what DD72
        // learned about the session label.
        Assert.Equal("freewilly", EnginePaths.CurrentDistribution);
        Assert.Equal("FreeWilly", EnginePaths.CurrentRootName);
    }

    [Fact]
    public void Everything_under_the_root_follows_the_root()
    {
        // `bin` is the directory the installer put on PATH, so this is not cosmetic: a path resolved
        // against a different root points the docker CLI at a folder with nothing in it.
        var paths = new EnginePaths(Root);

        Assert.Equal(Path.Combine(Root, "distro"), paths.Distribution);
        Assert.Equal(Path.Combine(Root, "downloads"), paths.Downloads);
        Assert.Equal(Path.Combine(Root, "bin"), paths.CliDirectory);

        // DD141 moved the vendor's CLI one directory across, and the two live apart by a rule of
        // Windows rather than by preference: PATHEXT resolves .EXE before everything else, so a
        // forwarder placed beside docker.exe would be a file nothing ever runs. What sits on PATH
        // now is this project's own docker, which finds the one below relative to itself.
        Assert.Equal(Path.Combine(Root, "cli"), paths.VendorCliDirectory);
        Assert.Equal(Path.Combine(Root, "cli", "docker.exe"), paths.DockerCli);
        Assert.Equal(Path.Combine(Root, "bin", "docker.exe"), paths.DockerShim);

        // DD73: the plugins hang off the config directory the CLI is pointed at, which is the root.
        Assert.Equal(Root, paths.ConfigDirectory);
        Assert.Equal(Path.Combine(Root, "cli-plugins"), paths.PluginsDirectory);
    }

    [Fact]
    public void A_machine_with_nothing_installed_still_resolves_the_current_root()
    {
        // The parameterless constructor reads %LOCALAPPDATA% and appends the one name. It used to
        // search for an install first, which is what made it worth a test with a fake filesystem;
        // now the only thing that could go wrong is the name, which the first test holds.
        var paths = new EnginePaths();

        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                EnginePaths.CurrentRootName),
            paths.Root);
        Assert.Equal(EnginePaths.CurrentDistribution, paths.DistributionName);
    }

    // ---- the uninstall is one command -------------------------------------------------------------

    [Fact]
    public void The_installer_unregisters_the_distribution_this_tool_owns()
    {
        // An owned distribution makes the uninstall exactly one command, and this is the assertion
        // that keeps the script and EnginePaths talking about the same name. It used to check two
        // `--unregister` calls because an adopted install could carry the older name; DD86 removed
        // the second, and a leftover call would now unregister a distribution belonging to nobody.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "FreeWilly.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "the repository root was not found above the test binaries");
        var script = File.ReadAllText(Path.Combine(directory!.FullName, "build", "installer.iss"));

        Assert.Contains("DistroName = 'freewilly';", script, StringComparison.Ordinal);
        Assert.Contains("'--unregister ' + DistroName", script, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyDistroName", script, StringComparison.Ordinal);
        Assert.DoesNotContain("dockerdesk", script, StringComparison.OrdinalIgnoreCase);

        // And {app} is the root EnginePaths resolves, or the two disagree about where `bin` — the
        // directory on PATH — actually is.
        Assert.Contains(
            @"DefaultDirName={localappdata}\FreeWilly", script, StringComparison.Ordinal);
    }
}
