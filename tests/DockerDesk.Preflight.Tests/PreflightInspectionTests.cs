using DockerDesk.Core.Preflight;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// The report's own failure modes, each one injected. A check observed only passing has proved
/// nothing, so every row here is asserted red at least once.
/// </summary>
public sealed class PreflightInspectionTests
{
    [Fact]
    public void A_healthy_machine_can_host_an_engine()
    {
        var report = PreflightInspection.Run(FakeMachine.Healthy);

        Assert.True(report.CanHostEngine);
        Assert.Empty(report.Blockers);
        Assert.All(report.Checks, check => Assert.Equal(Verdict.Pass, check.Verdict));
    }

    [Fact]
    public void Every_row_is_reported_once()
    {
        var report = PreflightInspection.Run(FakeMachine.Healthy);

        Assert.Equal(
            [
                PreflightInspection.Rows.WindowsBuild,
                PreflightInspection.Rows.Virtualization,
                PreflightInspection.Rows.Wsl2,
                PreflightInspection.Rows.RivalEngine,
                PreflightInspection.Rows.DockerContext,
            ],
            report.Checks.Select(check => check.Id));
    }

    [Theory]
    [InlineData(18362)]
    [InlineData(19040)]
    [InlineData(1)]
    public void A_build_below_the_floor_blocks(int build)
    {
        var report = Run(new FakeMachine
        {
            OperatingSystem = new Version(10, 0, build, 0),
        });

        var row = Row(report, PreflightInspection.Rows.WindowsBuild);
        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.True(row.Blocks);
        Assert.False(report.CanHostEngine);
        Assert.Contains(build.ToString(), row.Detail, StringComparison.Ordinal);
        Assert.NotNull(row.Remedy);
    }

    [Fact]
    public void The_floor_itself_passes()
    {
        var report = Run(new FakeMachine
        {
            OperatingSystem = new Version(10, 0, PreflightInspection.MinimumWindowsBuild, 0),
        });

        Assert.Equal(Verdict.Pass, Row(report, PreflightInspection.Rows.WindowsBuild).Verdict);
        Assert.True(report.CanHostEngine);
    }

    [Fact]
    public void Virtualization_disabled_in_firmware_blocks()
    {
        var report = Run(new FakeMachine { VirtualizationFirmwareEnabled = false });

        var row = Row(report, PreflightInspection.Rows.Virtualization);
        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.True(row.Blocks);
        Assert.False(report.CanHostEngine);
        Assert.Contains("BIOS", row.Remedy!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Virtualization_that_could_not_be_read_is_unknown_and_still_blocks()
    {
        // The row that would be tempting to call green. An unread fact is not a fact.
        var report = Run(new FakeMachine { VirtualizationFirmwareEnabled = null });

        var row = Row(report, PreflightInspection.Rows.Virtualization);
        Assert.Equal(Verdict.Unknown, row.Verdict);
        Assert.True(row.Blocks);
        Assert.False(report.CanHostEngine);
    }

    [Fact]
    public void A_running_hypervisor_outranks_a_firmware_bit_it_has_already_claimed()
    {
        // Windows reports the firmware bit as false once a hypervisor holds it. Reading the bit
        // first sends a user into a BIOS to enable something that is plainly already on.
        var report = Run(new FakeMachine
        {
            HypervisorPresent = true,
            VirtualizationFirmwareEnabled = false,
        });

        var row = Row(report, PreflightInspection.Rows.Virtualization);
        Assert.Equal(Verdict.Pass, row.Verdict);
        Assert.True(report.CanHostEngine);
    }

    [Fact]
    public void No_wsl_at_all_blocks_and_names_the_install_command()
    {
        var report = Run(new FakeMachine
        {
            Wsl = new WslInstallation { CommandPresent = false },
        });

        var row = Row(report, PreflightInspection.Rows.Wsl2);
        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.True(row.Blocks);
        Assert.Contains("wsl --install", row.Remedy!, StringComparison.Ordinal);
    }

    [Fact]
    public void Wsl_with_no_kernel_behind_it_blocks_and_names_the_update_command()
    {
        var report = Run(new FakeMachine
        {
            Wsl = new WslInstallation { CommandPresent = true, DefaultVersion = 2 },
        });

        var row = Row(report, PreflightInspection.Rows.Wsl2);
        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.True(row.Blocks);
        Assert.Contains("wsl --update", row.Remedy!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wsl_read_that_failed_is_unknown_rather_than_missing()
    {
        var report = Run(new FakeMachine
        {
            Wsl = new WslInstallation
            {
                CommandPresent = true,
                Unreadable = "wsl.exe did not finish within 15 seconds",
            },
        });

        var row = Row(report, PreflightInspection.Rows.Wsl2);
        Assert.Equal(Verdict.Unknown, row.Verdict);
        Assert.True(row.Blocks);
        Assert.Contains("did not finish", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wsl1_default_warns_and_does_not_block()
    {
        var report = Run(new FakeMachine
        {
            Wsl = new WslInstallation
            {
                CommandPresent = true,
                KernelVersion = "6.6.87.2",
                DefaultVersion = 1,
            },
        });

        var row = Row(report, PreflightInspection.Rows.Wsl2);
        Assert.Equal(Verdict.Warn, row.Verdict);
        Assert.False(row.Blocks);
        Assert.True(report.CanHostEngine);
        Assert.Contains("--set-default-version 2", row.Remedy!, StringComparison.Ordinal);
    }

    [Fact]
    public void An_installed_rival_blocks_and_is_named_with_its_evidence()
    {
        var report = Run(new FakeMachine
        {
            RivalEngines = [new RivalEngine("Docker Desktop", @"C:\Program Files\Docker\x.exe")],
        });

        var row = Row(report, PreflightInspection.Rows.RivalEngine);
        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.True(row.Blocks);
        Assert.False(report.CanHostEngine);
        Assert.Contains("Docker Desktop", row.Detail, StringComparison.Ordinal);
        Assert.Contains(@"C:\Program Files\Docker\x.exe", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Several_broken_facts_are_all_reported_rather_than_the_first_one()
    {
        var report = Run(new FakeMachine
        {
            OperatingSystem = new Version(10, 0, 17763, 0),
            VirtualizationFirmwareEnabled = false,
            Wsl = new WslInstallation { CommandPresent = false },
            RivalEngines = [new RivalEngine("Rancher Desktop", "somewhere")],
            DockerClient = new DockerClientTarget
            {
                ContextName = "desktop-linux",
                Host = "npipe:////./pipe/dockerDesktopLinuxEngine",
            },
        });

        // Five rows are wrong and every one of them names an action. Four blockers, not five: the
        // docker-context row warns, because a leftover context does not stop this engine from
        // working — it stops the user's CLI from finding it (DD20).
        Assert.Equal(5, report.Checks.Count);
        Assert.Equal(4, report.Blockers.Count);
        Assert.All(report.Checks, check => Assert.NotNull(check.Remedy));
    }

    [Fact]
    public void A_passing_row_offers_no_remedy()
    {
        var report = PreflightInspection.Run(FakeMachine.Healthy);

        Assert.All(report.Checks, check => Assert.Null(check.Remedy));
    }

    [Fact]
    public void Run_refuses_a_null_machine() =>
        Assert.Throws<ArgumentNullException>(() => PreflightInspection.Run(null!));

    private static PreflightReport Run(IMachineFacts facts) => PreflightInspection.Run(facts);

    private static PreflightCheck Row(PreflightReport report, string id) =>
        report[id] ?? throw new InvalidOperationException($"no row {id}");
}
