using FreeWilly.Core.Engine;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The two names the rename could not overwrite, and the adoption that keeps them working (DD55).
/// </summary>
/// <remarks>
/// Every decision here is a pure function of what was found, so what this asserts holds on a machine
/// that has an install made before the rename and on one that has never seen this product. The
/// alternative — asking the real registry and the real disk — would be a suite whose answer depends
/// on the developer's laptop, which is the failure DD64 had just finished removing.
/// </remarks>
public sealed class EnginePathsTests
{
    private const string Local = @"C:\Users\someone\AppData\Local";
    private static readonly string Current = Path.Combine(Local, "FreeWilly");
    private static readonly string Old = Path.Combine(Local, "DockerDesk");

    /// <summary>A machine on which exactly these roots hold an install.</summary>
    private static Func<string, bool> Installed(params string[] roots) =>
        path => roots.Any(root =>
            path.Equals(Path.Combine(root, "distro"), StringComparison.OrdinalIgnoreCase)
            || path.Equals(Path.Combine(root, "downloads"), StringComparison.OrdinalIgnoreCase));

    // ---- the root ---------------------------------------------------------------------------------

    [Fact]
    public void A_machine_that_has_never_seen_this_product_gets_the_current_root()
    {
        Assert.Equal(Current, EnginePaths.RootFor(Local, Installed()));
    }

    [Fact]
    public void An_install_made_before_the_rename_is_adopted_where_it_stands()
    {
        // The whole of DD55 in one assertion. `distro` under this root is the BasePath WSL registered
        // the distribution at, so moving the directory orphans the distribution exactly as surely as
        // renaming it would — and the directory holds every image and volume the user created.
        Assert.Equal(Old, EnginePaths.RootFor(Local, Installed(Old)));
    }

    [Fact]
    public void The_current_root_wins_where_a_machine_somehow_has_both()
    {
        // The only ordering that converges. Preferring the legacy one here would make the adoption
        // permanent by accident on a machine where the new install is the real one.
        Assert.Equal(Current, EnginePaths.RootFor(Local, Installed(Current, Old)));
    }

    [Fact]
    public void A_legacy_directory_holding_no_install_does_not_capture_a_fresh_one()
    {
        // Found by running --plan on the development machine, which reported the old root on a
        // machine that has never installed this engine: DD39 writes window.json into the root the
        // first time a window closes, so opening the window once was enough to leave the directory
        // behind. Adopting that would point a fresh install's bin — the folder on PATH — at nothing.
        var onlyTheDirectory = (string path) =>
            path.Equals(Old, StringComparison.OrdinalIgnoreCase)
            || path.Equals(Path.Combine(Old, "window.json"), StringComparison.OrdinalIgnoreCase);

        Assert.Equal(Current, EnginePaths.RootFor(Local, onlyTheDirectory));
    }

    [Fact]
    public void Downloads_alone_is_enough_evidence_of_an_install()
    {
        // The same two directories build\installer.iss asks about before it offers to delete
        // anything. An acquire that stopped before importing leaves downloads and no distro, and
        // that root is still this product's.
        var downloadsOnly = (string path) =>
            path.Equals(Path.Combine(Old, "downloads"), StringComparison.OrdinalIgnoreCase);

        Assert.Equal(Old, EnginePaths.RootFor(Local, downloadsOnly));
    }

    // ---- the distribution -------------------------------------------------------------------------

    [Fact]
    public void The_distribution_is_whichever_of_the_two_names_WSL_has_registered()
    {
        Assert.Equal("freewilly", EnginePaths.DistributionFor([]));
        Assert.Equal("freewilly", EnginePaths.DistributionFor(["Ubuntu", "docker-desktop"]));
        Assert.Equal("dockerdesk", EnginePaths.DistributionFor(["Ubuntu", "dockerdesk"]));
        Assert.Equal("freewilly", EnginePaths.DistributionFor(["dockerdesk", "freewilly"]));
    }

    [Fact]
    public void A_registered_name_is_matched_however_it_is_cased_or_padded()
    {
        // WSL reports what the import was given, and `wsl --list` pads its output. A name that failed
        // to match here would import a second distribution beside a full one.
        Assert.Equal("dockerdesk", EnginePaths.DistributionFor(["  DockerDesk  "]));
    }

    [Fact]
    public void An_unreadable_registry_reads_as_a_fresh_machine_rather_than_as_an_error()
    {
        // Wsl.RegisteredDistributions answers empty where it cannot read, and empty resolves to the
        // current name — which is what a fresh machine gives anyway. The cost of being wrong here is
        // an import that the provisioner's own "already registered" check would catch.
        Assert.Equal("freewilly", EnginePaths.DistributionFor([]));
    }

    // ---- what the two together mean ---------------------------------------------------------------

    [Fact]
    public void Adopted_is_true_when_either_name_is_the_old_one()
    {
        // Either, not both: an uninstall that kept the user's data leaves a root with no distribution
        // under it, and a distribution outlives a root somebody deleted by hand. A report that called
        // one of those a fresh install would send its reader looking in the wrong folder.
        Assert.True(new EnginePaths(Old, "freewilly").IsAdopted);
        Assert.True(new EnginePaths(Current, "dockerdesk").IsAdopted);
        Assert.True(new EnginePaths(Old, "dockerdesk").IsAdopted);
        Assert.False(new EnginePaths(Current, "freewilly").IsAdopted);
    }

    [Fact]
    public void Which_of_the_two_is_old_is_asked_separately_because_they_can_differ()
    {
        // A single "adopted" flag reported the development machine as `distribution freewilly
        // (adopted)`, which is a sentence about the root wearing the distribution's label. The two
        // are separate questions and the plan marks whichever line is actually the old one.
        var oldRootOnly = new EnginePaths(Old, "freewilly");
        Assert.True(oldRootOnly.RootIsLegacy);
        Assert.False(oldRootOnly.DistributionIsLegacy);

        var oldDistributionOnly = new EnginePaths(Current, "dockerdesk");
        Assert.False(oldDistributionOnly.RootIsLegacy);
        Assert.True(oldDistributionOnly.DistributionIsLegacy);
    }

    [Fact]
    public void A_trailing_separator_does_not_make_an_adopted_root_read_as_fresh()
    {
        Assert.True(new EnginePaths(Old + @"\", "freewilly").IsAdopted);
    }

    [Fact]
    public void Everything_under_the_root_follows_the_root_that_was_adopted()
    {
        // `bin` is the directory the installer put on PATH, so this is not cosmetic: resolving it
        // under the new name on an adopted machine points the docker CLI at a folder with nothing
        // in it.
        var adopted = new EnginePaths(Old, "dockerdesk");

        Assert.Equal(Path.Combine(Old, "distro"), adopted.Distribution);
        Assert.Equal(Path.Combine(Old, "downloads"), adopted.Downloads);
        Assert.Equal(Path.Combine(Old, "bin"), adopted.CliDirectory);
        Assert.Equal(Path.Combine(Old, "bin", "docker.exe"), adopted.DockerCli);
    }

    [Fact]
    public void The_old_spellings_are_named_once_and_are_marked_as_history()
    {
        // The one place a reader who meets `dockerdesk` in a WSL listing can find out in one search
        // that it is an adopted install rather than a spelling somebody forgot to change.
        Assert.Equal("dockerdesk", EnginePaths.Legacy.Distribution);
        Assert.Equal("DockerDesk", EnginePaths.Legacy.RootName);
        Assert.Equal("freewilly", EnginePaths.CurrentDistribution);
        Assert.Equal("FreeWilly", EnginePaths.CurrentRootName);
    }

    // ---- the uninstall stays one command ----------------------------------------------------------

    [Fact]
    public void The_installer_unregisters_both_names()
    {
        // The sentence the old comment made and this task had to keep true: an owned distribution
        // makes the uninstall exactly one command. It is now two `--unregister` calls rather than a
        // derivation, because a derivation that got it wrong would leave a distribution no
        // uninstaller knows about — the exact failure this set exists to avoid.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "FreeWilly.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "the repository root was not found above the test binaries");
        var script = File.ReadAllText(Path.Combine(directory!.FullName, "build", "installer.iss"));

        Assert.Contains("DistroName = 'freewilly';", script, StringComparison.Ordinal);
        Assert.Contains("LegacyDistroName = 'dockerdesk';", script, StringComparison.Ordinal);
        Assert.Contains("'--unregister ' + DistroName", script, StringComparison.Ordinal);
        Assert.Contains("'--unregister ' + LegacyDistroName", script, StringComparison.Ordinal);

        // And {app} is the root EnginePaths resolves for a fresh machine, or the two disagree about
        // where `bin` — the directory on PATH — actually is.
        Assert.Contains(
            @"DefaultDirName={localappdata}\FreeWilly", script, StringComparison.Ordinal);
    }
}
