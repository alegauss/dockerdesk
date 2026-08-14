using FreeWilly.Core.Agent;
using FreeWilly.Core.Engine;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The rule that decides whether the user's <c>DOCKER_CONFIG</c> is this install's to write (DD124).
/// </summary>
/// <remarks>
/// The deciding only. <see cref="DockerConfigEntry.Ensure"/> reads and writes <c>HKCU\Environment</c>,
/// which is the machine running this suite — so what is asserted here is the pure function it asks,
/// the same split <c>RivalEngineProbe</c> makes for the same reason.
/// </remarks>
public class DockerConfigEntryTests
{
    private const string Root = @"C:\Users\x\AppData\Local\FreeWilly";
    private const string Bin = @"C:\Users\x\AppData\Local\FreeWilly\bin";

    [Fact]
    public void An_unset_variable_is_written()
    {
        // The state every machine is in before DD124, and the one this exists for.
        Assert.True(DockerConfigEntry.NeedsWriting(null, Root));
        Assert.True(DockerConfigEntry.NeedsWriting("", Root));
        Assert.True(DockerConfigEntry.NeedsWriting("   ", Root));
    }

    [Fact]
    public void A_variable_already_naming_this_install_is_left_alone()
    {
        // Not an optimisation: writing is a registry write and a WM_SETTINGCHANGE broadcast, and
        // doing it at every logon for no change is the kind of resident behaviour this product is
        // the complaint about.
        Assert.False(DockerConfigEntry.NeedsWriting(Root, Root));
    }

    [Theory]
    [InlineData(@"c:\users\x\appdata\local\freewilly")]
    [InlineData(@"C:\Users\x\AppData\Local\FreeWilly\")]
    [InlineData(@"  C:\Users\x\AppData\Local\FreeWilly  ")]
    public void The_same_directory_spelled_differently_is_still_the_same_directory(string current)
    {
        // Windows paths compare case-insensitively, and a user who typed a trailing backslash has
        // nothing wrong with their environment. Rewriting it would be this tool correcting spelling
        // in somebody else's registry.
        Assert.False(DockerConfigEntry.NeedsWriting(current, Root));
    }

    [Fact]
    public void A_variable_pointing_somewhere_else_is_written_over()
    {
        // The deliberate half of the rule. Where PATH says this install owns the docker command, a
        // DOCKER_CONFIG naming anywhere else means that docker finds no compose — which is the whole
        // symptom. Ownership is decided by PATH, and once decided it is not half-held.
        Assert.True(DockerConfigEntry.NeedsWriting(@"C:\Users\x\.docker", Root));
    }

    [Fact]
    public void An_install_that_is_not_on_PATH_does_not_touch_the_users_environment()
    {
        // The consent check. DOCKER_CONFIG is read by every docker.exe a shell runs and carries
        // config.json, the contexts and the docker login credentials with it — so pointing it here
        // is only honest where the docker a shell resolves is this one. Declining the installer's
        // checkbox says "leave my command line alone", and this is what leaves it alone.
        Assert.False(DockerConfigEntry.OwnsTheDockerCommand(null, Bin));
        Assert.False(DockerConfigEntry.OwnsTheDockerCommand("", Bin));
        Assert.False(DockerConfigEntry.OwnsTheDockerCommand(@"C:\Windows;C:\Windows\System32", Bin));
    }

    [Fact]
    public void An_install_on_PATH_owns_the_docker_command()
    {
        Assert.True(DockerConfigEntry.OwnsTheDockerCommand(Bin, Bin));
        Assert.True(DockerConfigEntry.OwnsTheDockerCommand($@"C:\Windows;{Bin};C:\Other", Bin));
        Assert.True(DockerConfigEntry.OwnsTheDockerCommand($@"C:\Windows;{Bin}", Bin));
        Assert.True(DockerConfigEntry.OwnsTheDockerCommand($@"{Bin};C:\Windows", Bin));
    }

    [Fact]
    public void A_longer_directory_starting_with_this_one_is_not_this_one()
    {
        // The guard PathEntryMissing makes in the installer, made here too — the two halves of one
        // rule, and a prefix match would have this half claim an install the other half never made.
        Assert.False(DockerConfigEntry.OwnsTheDockerCommand($@"C:\Windows;{Bin}2", Bin));
        Assert.False(DockerConfigEntry.OwnsTheDockerCommand($@"{Bin}2;C:\Windows", Bin));
        Assert.False(DockerConfigEntry.OwnsTheDockerCommand($@"C:\Windows;{Bin}\nested", Bin));
    }

    [Fact]
    public void A_trailing_separator_on_PATH_does_not_hide_the_entry()
    {
        // Windows tolerates it and plenty of machines carry one, so a rule that missed it would
        // silently decline to fix exactly the machines most likely to need fixing.
        Assert.True(DockerConfigEntry.OwnsTheDockerCommand($@"C:\Windows;{Bin};", Bin));
        Assert.True(DockerConfigEntry.OwnsTheDockerCommand($@"C:\Windows;{Bin}\;", Bin));
    }

    [Fact]
    public void The_variable_has_one_spelling_across_both_writers()
    {
        // The child-level assignment (DD73) and the user-level one (DD124) are different decisions
        // about the same variable. Two literals is how they would drift onto different names, and
        // the failure would be silent: the bundled compose would keep working while the shell the
        // user is actually in kept getting `unknown flag`.
        Assert.Equal("DOCKER_CONFIG", DockerConfigEntry.Variable);
        Assert.Equal(DockerConfigEntry.Variable, BundledComposeCli.ConfigVariable);
    }

    [Fact]
    public void What_the_variable_wants_is_the_directory_holding_the_plugins()
    {
        // The CLI is given a *config directory* and looks for cli-plugins inside it, so naming the
        // plugins directory itself would place the plugins one level below where anything looks.
        var paths = new EnginePaths(Root);
        var entry = new DockerConfigEntry(paths);

        Assert.Equal(paths.ConfigDirectory, entry.Wanted);
        Assert.Equal(
            paths.PluginsDirectory,
            System.IO.Path.Combine(entry.Wanted, "cli-plugins"));
    }
}
