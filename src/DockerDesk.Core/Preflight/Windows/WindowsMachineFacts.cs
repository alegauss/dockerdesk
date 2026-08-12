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

    private readonly Lazy<WslInstallation> _wsl = new(WslProbe.Read);

    private readonly Lazy<IReadOnlyList<RivalEngine>> _rivals = new(RivalEngineProbe.Read);

    /// <inheritdoc/>
    public Version OperatingSystem => Environment.OSVersion.Version;

    /// <inheritdoc/>
    public bool? HypervisorPresent => _virtualization.Value.Hypervisor;

    /// <inheritdoc/>
    public bool? VirtualizationFirmwareEnabled => _virtualization.Value.Firmware;

    /// <inheritdoc/>
    public WslInstallation Wsl => _wsl.Value;

    /// <inheritdoc/>
    public IReadOnlyList<RivalEngine> RivalEngines => _rivals.Value;

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
