using DockerDesk.Core.Preflight;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// A machine that can be made to fail. Virtualization switched off in firmware, a missing WSL2
/// kernel and an installed rival are the three states the report exists for, and none of them can
/// be produced on the machine running the tests.
/// </summary>
internal sealed record FakeMachine : IMachineFacts
{
    /// <summary>A machine on which every blocking row is green.</summary>
    internal static FakeMachine Healthy => new();

    public Version OperatingSystem { get; init; } = new(10, 0, 26200, 0);

    public bool? VirtualizationFirmwareEnabled { get; init; } = true;

    public bool? HypervisorPresent { get; init; }

    public WslInstallation Wsl { get; init; } = new()
    {
        CommandPresent = true,
        Version = "2.6.1.0",
        KernelVersion = "6.6.87.2",
        DefaultVersion = 2,
    };

    public IReadOnlyList<RivalEngine> RivalEngines { get; init; } = [];
}
