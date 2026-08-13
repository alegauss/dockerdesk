using System.Management;
using System.Runtime.InteropServices;

namespace DockerDesk.Core.Preflight.Windows;

/// <summary>
/// <see cref="IMachineFacts"/> read off the machine this process is running on.
/// </summary>
/// <remarks>
/// Every read is cached on first use, so a caller that renders the report twice does not pay for
/// a second WMI round trip or a second <c>wsl --version</c>. Nothing here judges: a fact that
/// could not be read comes back <see langword="null"/> and the report decides what that means.
/// </remarks>
public sealed class WindowsMachineFacts : IMachineFacts
{
    private readonly Lazy<(bool? Hypervisor, bool? Firmware)> _virtualization =
        new(ReadVirtualization);

    private readonly Lazy<bool?> _guest = new(ReadIsVirtualMachine);

    private readonly Lazy<WslInstallation> _wsl = new(WslProbe.Read);

    private readonly Lazy<IReadOnlyList<RivalEngine>> _rivals = new(RivalEngineProbe.Read);

    private readonly Lazy<DockerClientTarget> _dockerClient = new(DockerContextProbe.Read);

    /// <inheritdoc/>
    public Version OperatingSystem => Environment.OSVersion.Version;

    /// <inheritdoc/>
    public bool? HypervisorPresent => _virtualization.Value.Hypervisor;

    /// <inheritdoc/>
    public bool? VirtualizationFirmwareEnabled => _virtualization.Value.Firmware;

    /// <inheritdoc/>
    public bool? IsVirtualMachine => _guest.Value;

    /// <inheritdoc/>
    public WslInstallation Wsl => _wsl.Value;

    /// <inheritdoc/>
    public IReadOnlyList<RivalEngine> RivalEngines => _rivals.Value;

    /// <inheritdoc/>
    public DockerClientTarget DockerClient => _dockerClient.Value;

    /// <summary>
    /// Read what the firmware calls this machine, and ask whether that names a hypervisor.
    /// </summary>
    /// <remarks>
    /// Three strings out of two classes, because no one of them is present everywhere: a Hyper-V
    /// guest is identified by its model, a VMware guest by all three, and a QEMU guest sometimes by
    /// the BIOS alone. WMI unreadable comes back null and the report decides what that means.
    /// </remarks>
    private static bool? ReadIsVirtualMachine()
    {
        var system = QueryStrings(
            "SELECT Manufacturer, Model FROM Win32_ComputerSystem", "Manufacturer", "Model");
        var bios = QueryStrings("SELECT Manufacturer FROM Win32_BIOS", "Manufacturer");

        return VirtualMachineSignature.Matches(system[0], system[1], bios[0]);
    }

    /// <summary>One row of string properties out of WMI, each null where it could not be read.</summary>
    private static string?[] QueryStrings(string wql, params string[] properties)
    {
        var answer = new string?[properties.Length];
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            foreach (ManagementBaseObject row in searcher.Get())
            {
                using (row)
                {
                    for (var i = 0; i < properties.Length; i++)
                    {
                        answer[i] ??= row[properties[i]] as string;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is ManagementException
            or COMException or UnauthorizedAccessException or InvalidOperationException
            or NotSupportedException)
        {
            // Same reasoning as the boolean read below: a broken WMI is Unknown, never a green row.
        }

        return answer;
    }

    private static (bool? Hypervisor, bool? Firmware) ReadVirtualization() =>
        (Query("SELECT HypervisorPresent FROM Win32_ComputerSystem", "HypervisorPresent"),
         Query("SELECT VirtualizationFirmwareEnabled FROM Win32_Processor",
             "VirtualizationFirmwareEnabled"));

    /// <summary>
    /// One boolean out of WMI, or <see langword="null"/> where the question could not be asked.
    /// </summary>
    /// <remarks>
    /// True from any row wins. A machine with more than one processor package answers per package,
    /// and virtualization enabled on one of them is virtualization enabled.
    /// </remarks>
    private static bool? Query(string wql, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            bool? answer = null;
            foreach (ManagementBaseObject row in searcher.Get())
            {
                using (row)
                {
                    if (row[property] is bool value)
                    {
                        if (value)
                        {
                            return true;
                        }

                        answer = false;
                    }
                }
            }

            return answer;
        }
        catch (Exception exception) when (exception is ManagementException
            or COMException or UnauthorizedAccessException or InvalidOperationException
            or NotSupportedException)
        {
            // WMI is a service, and a machine whose repository is broken or whose service is
            // disabled cannot answer. That is Unknown to the report, never a green row.
            return null;
        }
    }
}
