using FreeWilly.Core.Preflight;
using FreeWilly.Core.Preflight.Windows;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// Telling "I am virtualized" from "I can host a hypervisor" (DD19).
/// </summary>
/// <remarks>
/// The two facts the row now reads were measured on two machines, and the point of the measurement
/// is that the bits it used to read are identical on both:
///
/// <code>
///                                  host (Hyper-V running)   VMware guest (nested virt off)
///   HypervisorPresent              True                     True
///   VirtualizationFirmwareEnabled  False                    False
///   Win32_ComputerSystem.Model     XPS 8960                 VMware20,1
///   Win32_BIOS.Manufacturer        Dell Inc.                VMware, Inc.
/// </code>
///
/// So a test that only drives booleans cannot reach this defect at all, which is why the identity
/// strings are the thing under test here.
/// </remarks>
public sealed class VirtualMachineTests
{
    // ---- the two machines that were actually measured ------------------------------------------

    [Fact]
    public void The_measured_VMware_guest_is_recognised_as_a_guest() =>
        Assert.True(VirtualMachineSignature.Matches("VMware, Inc.", "VMware20,1", "VMware, Inc."));

    [Fact]
    public void The_measured_host_is_not() =>
        Assert.False(VirtualMachineSignature.Matches("Dell Inc.", "XPS 8960", "Dell Inc."));

    // ---- the hypervisors somebody might actually be on -----------------------------------------

    [Theory]
    [InlineData("VMware, Inc.", "VMware20,1", "VMware, Inc.")]
    [InlineData("innotek GmbH", "VirtualBox", "innotek GmbH")]
    [InlineData("Microsoft Corporation", "Virtual Machine", "Microsoft Corporation")]
    [InlineData("QEMU", "Standard PC (Q35 + ICH9, 2009)", "QEMU")]
    [InlineData("Xen", "HVM domU", "Xen")]
    [InlineData("Parallels Software International Inc.", "Parallels Virtual Platform", "Parallels")]
    [InlineData("Amazon EC2", "t3.medium", "Amazon EC2")]
    [InlineData("Google", "Google Compute Engine", "Google")]
    [InlineData("Red Hat", "KVM", "SeaBIOS")]
    public void A_guest_is_recognised_however_its_firmware_spells_it(
        string manufacturer, string model, string bios) =>
        Assert.True(VirtualMachineSignature.Matches(manufacturer, model, bios));

    [Theory]
    [InlineData("Dell Inc.", "XPS 8960", "Dell Inc.")]
    [InlineData("LENOVO", "20XW004UUS", "LENOVO")]
    [InlineData("HP", "HP EliteBook 840 G8", "HP")]
    [InlineData("ASUS", "System Product Name", "American Megatrends Inc.")]
    [InlineData("Framework", "Laptop (13th Gen Intel Core)", "INSYDE Corp.")]
    public void A_real_machine_is_not_mistaken_for_one(
        string manufacturer, string model, string bios) =>
        Assert.False(VirtualMachineSignature.Matches(manufacturer, model, bios));

    [Fact]
    public void A_Surface_is_a_real_machine_even_though_Microsoft_also_makes_Hyper_V() =>
        // "Microsoft Corporation" is the manufacturer of both a Surface and a Hyper-V guest, so it
        // is matched on the model and never on the vendor. Getting this wrong blocks an install on
        // a laptop.
        Assert.False(VirtualMachineSignature.Matches(
            "Microsoft Corporation", "Surface Laptop Studio", "Microsoft Corporation"));

    [Fact]
    public void A_Hyper_V_guest_from_the_same_vendor_still_is_one() =>
        Assert.True(VirtualMachineSignature.Matches(
            "Microsoft Corporation", "Virtual Machine", "Microsoft Corporation"));

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("   ", null, "")]
    public void Nothing_readable_is_Unknown_and_never_a_real_machine(
        string? manufacturer, string? model, string? bios) =>
        // A WMI that did not answer must not read as "not a VM". That would put the false Pass back
        // exactly where this took it out.
        Assert.Null(VirtualMachineSignature.Matches(manufacturer, model, bios));

    [Fact]
    public void One_string_out_of_three_is_still_an_answer()
    {
        Assert.True(VirtualMachineSignature.Matches(null, null, "VMware, Inc."));
        Assert.False(VirtualMachineSignature.Matches(null, null, "Dell Inc."));
    }

    // ---- what the row says --------------------------------------------------------------------

    [Fact]
    public void Inside_a_guest_the_row_abstains_rather_than_passing()
    {
        // The defect, as a report: both of these facts are what the measured guest reported, and
        // the row used to return Pass and clear the install.
        var row = VirtualizationRow(new FakeMachine
        {
            IsVirtualMachine = true,
            HypervisorPresent = true,
            VirtualizationFirmwareEnabled = false,
        });

        Assert.Equal(Verdict.Unknown, row.Verdict);
        Assert.True(row.Blocking);
        Assert.Contains("virtual machine", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void And_the_install_is_blocked_rather_than_failing_halfway()
    {
        // What DD19 is actually about: the sequence where the report says yes and
        // ImportDistribution then says "WSL2 cannot start because virtualization is not enabled".
        var report = PreflightInspection.Run(new FakeMachine
        {
            IsVirtualMachine = true,
            HypervisorPresent = true,
            VirtualizationFirmwareEnabled = false,
        });

        Assert.False(report.CanHostEngine);
        Assert.Contains(report.Blockers, row => row.Title == "Hardware virtualization");
    }

    [Fact]
    public void The_remedy_names_the_setting_on_the_host_and_not_a_BIOS_to_reboot_into()
    {
        var row = VirtualizationRow(new FakeMachine { IsVirtualMachine = true });

        Assert.NotNull(row.Remedy);
        Assert.Contains("nested virtualization", row.Remedy, StringComparison.OrdinalIgnoreCase);
        // A guest has no UEFI setup screen of its own worth sending anybody into.
        Assert.DoesNotContain("UEFI", row.Remedy, StringComparison.Ordinal);
    }

    [Fact]
    public void A_guest_abstains_even_where_its_firmware_bit_reads_true() =>
        // Deliberate: being a guest means the question is unanswerable from in here, and a guest
        // reporting the bit is not evidence the host exposed anything. Unknown blocks, so the worst
        // case is somebody reading a remedy — never a failed install.
        Assert.Equal(
            Verdict.Unknown,
            VirtualizationRow(new FakeMachine
            {
                IsVirtualMachine = true,
                VirtualizationFirmwareEnabled = true,
            }).Verdict);

    // ---- what did not change ------------------------------------------------------------------

    [Fact]
    public void A_real_machine_running_Hyper_V_still_passes_on_the_hypervisor_alone()
    {
        // The ordering the original row was built for, and it was right: Windows reports the
        // firmware bit false once a hypervisor claims it, so reading the bit first would send
        // somebody into a BIOS to switch on something already on. Measured on this host, which
        // reports exactly this pair.
        var row = VirtualizationRow(new FakeMachine
        {
            IsVirtualMachine = false,
            HypervisorPresent = true,
            VirtualizationFirmwareEnabled = false,
        });

        Assert.Equal(Verdict.Pass, row.Verdict);
        Assert.Contains("already running", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_real_machine_with_the_bit_off_still_fails_into_its_firmware()
    {
        var row = VirtualizationRow(new FakeMachine
        {
            IsVirtualMachine = false,
            HypervisorPresent = false,
            VirtualizationFirmwareEnabled = false,
        });

        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.Contains("UEFI", row.Remedy!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_machine_that_could_not_be_asked_anything_is_still_Unknown()
    {
        var row = VirtualizationRow(new FakeMachine
        {
            IsVirtualMachine = null,
            HypervisorPresent = null,
            VirtualizationFirmwareEnabled = null,
        });

        Assert.Equal(Verdict.Unknown, row.Verdict);
        Assert.True(row.Blocking);
    }

    private static PreflightCheck VirtualizationRow(FakeMachine machine) =>
        PreflightInspection.Run(machine)
            .Checks.Single(check => check.Id == PreflightInspection.Rows.Virtualization);
}
