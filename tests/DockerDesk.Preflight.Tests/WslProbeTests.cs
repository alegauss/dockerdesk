using DockerDesk.Core.Preflight;
using DockerDesk.Core.Preflight.Windows;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// Which question the WSL2 row asks, and in which order (DD18).
/// </summary>
/// <remarks>
/// Measured on a fresh Windows 11 guest, build 26200, that never had WSL: <c>wsl --version</c> does
/// not answer and does not fail there — it hangs, and the probe's own fifteen-second timeout ended
/// it, so the row cost fifteen seconds of silence to say <c>[?] could not be read</c> and then
/// offered <c>wsl --update</c>, which updates a WSL that is not installed.
///
/// The cost is the half a verdict assertion cannot see, so it is asserted directly: the version
/// command is a delegate here, and the bare-machine tests fail if anything calls it.
/// </remarks>
public sealed class WslProbeTests
{
    /// <summary>What `wsl --status` answers on a machine that never had WSL.</summary>
    /// <remarks>
    /// Exit 50, immediately. The sentence is the guest's own, in the guest's own language, and is
    /// carried here to make the point that nothing reads it: a localized string is not a fact.
    /// </remarks>
    private static ProcessOutput NoFeature => new(
        50,
        "O Subsistema do Windows para Linux não está instalado. "
        + "Você pode instalar executando 'wsl.exe --install'.",
        null);

    private static ProcessOutput Healthy => new(
        0, "Versão padrão: 2", null);

    private static ProcessOutput Hung => new(
        null, "", "C:\\WINDOWS\\system32\\wsl.exe did not finish within 15 seconds");

    /// <summary>A version command that fails the test if it is ever run.</summary>
    private static Func<ProcessOutput> NeverAsked => () =>
        throw new InvalidOperationException(
            "`wsl --version` was run on a machine that already said WSL is not installed — "
            + "that call is what costs fifteen seconds, and not making it is the fix");

    // ---- the cost -----------------------------------------------------------------------------

    [Fact]
    public void A_machine_that_never_had_WSL_is_never_asked_for_a_version()
    {
        var wsl = WslProbe.Interpret(NoFeature, NeverAsked, () => false, defaultVersion: null);

        Assert.True(wsl.CommandPresent);
        Assert.False(wsl.FeatureInstalled);
    }

    [Fact]
    public void A_status_that_hangs_is_not_followed_by_a_version_that_would_hang_too()
    {
        // Two timeouts to learn the same nothing is thirty seconds. One is enough to say so.
        var wsl = WslProbe.Interpret(Hung, NeverAsked, () => false, defaultVersion: null);

        Assert.True(wsl.CommandPresent);
        Assert.Null(wsl.FeatureInstalled);
        Assert.Equal("C:\\WINDOWS\\system32\\wsl.exe did not finish within 15 seconds", wsl.Unreadable);
    }

    [Fact]
    public void A_healthy_machine_is_still_asked_for_its_version()
    {
        var asked = 0;

        var wsl = WslProbe.Interpret(
            Healthy,
            () => { asked++; return new ProcessOutput(0, "WSL 2.5.10.0\nkernel 6.6.87.2", null); },
            () => false,
            defaultVersion: 2);

        Assert.Equal(1, asked);
        Assert.True(wsl.FeatureInstalled);
        Assert.Equal("2.5.10.0", wsl.Version);
        Assert.Equal("6.6.87.2", wsl.KernelVersion);
    }

    // ---- the verdict and the remedy -----------------------------------------------------------

    [Fact]
    public void The_row_says_not_installed_and_offers_install_rather_than_update()
    {
        var row = Wsl2Row(WslProbe.Interpret(NoFeature, NeverAsked, () => false, null));

        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.Contains("not installed", row.Detail, StringComparison.Ordinal);
        Assert.NotNull(row.Remedy);
        // The defect this replaces, named: `wsl --update` updates nothing on this machine.
        Assert.DoesNotContain("--update", row.Remedy, StringComparison.Ordinal);
        Assert.Contains("--install", row.Remedy, StringComparison.Ordinal);
        Assert.True(row.Blocking);
    }

    [Fact]
    public void A_bare_machine_blocks_the_install()
    {
        var report = PreflightInspection.Run(new FakeMachine
        {
            Wsl = WslProbe.Interpret(NoFeature, NeverAsked, () => false, null),
        });

        Assert.False(report.CanHostEngine);
        Assert.Contains(report.Blockers, row => row.Title == "WSL2");
    }

    [Fact]
    public void A_row_that_could_not_be_read_is_Unknown_and_still_blocks()
    {
        // The part of the original design that held: honest, and it stops an install. What changed
        // is that this is no longer the normal path for a bare machine.
        var row = Wsl2Row(WslProbe.Interpret(Hung, NeverAsked, () => false, null));

        Assert.Equal(Verdict.Unknown, row.Verdict);
        Assert.True(row.Blocking);
        Assert.Contains("15 seconds", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_half_installed_machine_is_told_to_update_because_that_is_what_it_needs()
    {
        // Feature installed, `--version` answered, and no kernel behind it. Here `wsl --update` is
        // the right remedy, which is why the not-installed case needed its own row rather than a
        // reworded one.
        var row = Wsl2Row(WslProbe.Interpret(
            Healthy, () => new ProcessOutput(0, "no versions here", null), () => false, null));

        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.Contains("--update", row.Remedy!, StringComparison.Ordinal);
    }

    // ---- what did not change ------------------------------------------------------------------

    [Fact]
    public void An_installation_too_old_for_the_version_command_is_still_recognised()
    {
        // `--status` is the older command, so such a machine answers it and reaches the fallback.
        var wsl = WslProbe.Interpret(
            Healthy,
            () => new ProcessOutput(1, "unknown option", null),
            inboxKernelPresent: () => true,
            defaultVersion: 2);

        Assert.True(wsl.FeatureInstalled);
        Assert.Equal("bundled with Windows, version not reported", wsl.KernelVersion);
        Assert.Null(wsl.Version);
    }

    [Fact]
    public void A_version_command_that_hangs_is_still_Unreadable()
    {
        var wsl = WslProbe.Interpret(Healthy, () => Hung, () => false, null);

        Assert.True(wsl.FeatureInstalled);
        Assert.NotNull(wsl.Unreadable);
    }

    [Fact]
    public void The_default_version_travels_from_the_registry_on_every_path()
    {
        Assert.Equal(1, WslProbe.Interpret(NoFeature, NeverAsked, () => false, 1).DefaultVersion);
        Assert.Equal(1, WslProbe.Interpret(Hung, NeverAsked, () => false, 1).DefaultVersion);
        Assert.Equal(1, WslProbe.Interpret(Healthy, () => Hung, () => false, 1).DefaultVersion);
    }

    [Fact]
    public void Only_the_measured_exit_code_means_not_installed()
    {
        // A wrong "not installed" would be worse than the slow row this replaced, so any other
        // non-zero answer falls through to the behaviour the row always had.
        var asked = 0;
        var wsl = WslProbe.Interpret(
            new ProcessOutput(1, "something else went wrong", null),
            () => { asked++; return new ProcessOutput(0, "WSL 2.5.10.0\nkernel 6.6.87.2", null); },
            () => false,
            null);

        Assert.Equal(1, asked);
        Assert.True(wsl.FeatureInstalled);
        Assert.Equal("6.6.87.2", wsl.KernelVersion);
    }

    [Fact]
    public void Nothing_here_accepts_a_null_where_it_needs_a_fact()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WslProbe.Interpret(null!, NeverAsked, () => false, null));
        Assert.Throws<ArgumentNullException>(() =>
            WslProbe.Interpret(Healthy, null!, () => false, null));
        Assert.Throws<ArgumentNullException>(() =>
            WslProbe.Interpret(Healthy, NeverAsked, null!, null));
    }

    private static PreflightCheck Wsl2Row(WslInstallation wsl) =>
        PreflightInspection.Run(new FakeMachine { Wsl = wsl })
            .Checks.Single(check => check.Title == "WSL2");
}
