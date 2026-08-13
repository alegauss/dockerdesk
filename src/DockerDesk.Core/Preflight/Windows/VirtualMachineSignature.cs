namespace DockerDesk.Core.Preflight.Windows;

/// <summary>
/// Whether this machine is itself a guest, read off what its firmware calls itself.
/// </summary>
/// <remarks>
/// The fact DD19 needed and the row did not have. <c>HypervisorPresent</c> is true of every machine
/// running <em>under</em> a hypervisor as well as every machine running one, so it cannot tell a
/// laptop with Hyper-V enabled from a virtual machine — and the two come apart exactly where it
/// matters. Measured, two machines, nothing else differing:
///
/// <code>
///                                  host (Hyper-V running)   VMware guest (nested virt off)
///   HypervisorPresent              True                     True
///   VirtualizationFirmwareEnabled  False                    False
///   Win32_ComputerSystem.Model     XPS 8960                 VMware20,1
///   Win32_BIOS.Manufacturer        Dell Inc.                VMware, Inc.
/// </code>
///
/// Both of the bits the row already read are identical on the two machines. The identity strings are
/// not, which is why this is what the new fact is made of.
///
/// A pure function of three strings, so every vendor below is a test rather than a machine somebody
/// has to own.
/// </remarks>
internal static class VirtualMachineSignature
{
    /// <summary>
    /// Substrings that name a hypervisor rather than a manufacturer, matched against the system and
    /// BIOS manufacturer.
    /// </summary>
    /// <remarks>
    /// Deliberately not "Microsoft Corporation": that is also what a Surface reports, and a Surface
    /// is a real machine. A Hyper-V guest is recognised by its model instead — see
    /// <see cref="Models"/>.
    /// </remarks>
    private static readonly string[] Vendors =
    [
        "VMware",
        "innotek",          // VirtualBox's system manufacturer
        "Oracle Corporation",
        "QEMU",
        "Bochs",
        "Xen",
        "Parallels",
        "Amazon EC2",
        "Google",           // Google Compute Engine
        "Nutanix",
        "OpenStack",
        "Red Hat",          // KVM
        "Apple Inc.",       // a Windows guest under Apple Virtualization reports this
    ];

    /// <summary>Substrings that name a virtual machine, matched against the model.</summary>
    private static readonly string[] Models =
    [
        "Virtual Machine",  // Hyper-V
        "VirtualBox",
        "VMware",
        "KVM",
        "Bochs",
        "OpenStack",
        "HVM domU",         // Xen
        "Google Compute Engine",
        "Parallels",
        "Standard PC",      // QEMU without a vendor string
        "Apple Virtual Machine",
    ];

    /// <summary>Whether these strings describe a guest.</summary>
    /// <param name="manufacturer"><c>Win32_ComputerSystem.Manufacturer</c>.</param>
    /// <param name="model"><c>Win32_ComputerSystem.Model</c>.</param>
    /// <param name="biosManufacturer"><c>Win32_BIOS.Manufacturer</c>.</param>
    /// <returns>
    /// <see langword="true"/> where something names a hypervisor, <see langword="false"/> where
    /// nothing does and there was something to read, and <see langword="null"/> where all three
    /// were empty — which is "could not be asked", never "a real machine".
    /// </returns>
    internal static bool? Matches(string? manufacturer, string? model, string? biosManufacturer)
    {
        var anything = new[] { manufacturer, model, biosManufacturer }
            .Any(value => !string.IsNullOrWhiteSpace(value));
        if (!anything)
        {
            // Three empty strings is a WMI that did not answer. Reporting that as a real machine
            // would put the false Pass back exactly where DD19 took it out.
            return null;
        }

        return Named(manufacturer, Vendors)
            || Named(biosManufacturer, Vendors)
            || Named(model, Models)
            || Named(model, Vendors);
    }

    private static bool Named(string? value, string[] needles) =>
        !string.IsNullOrWhiteSpace(value)
        && needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
