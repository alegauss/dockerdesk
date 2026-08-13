using FreeWilly.Core.Preflight;

namespace FreeWilly.Preflight.Tests;

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

    /// <summary>
    /// False by default: the healthy machine is a real one. A guest is a state to opt into, and
    /// leaving this null would make every existing test run against a machine whose most important
    /// row abstains.
    /// </summary>
    public bool? IsVirtualMachine { get; init; }

    public WslInstallation Wsl { get; init; } = new()
    {
        CommandPresent = true,
        FeatureInstalled = true,
        Version = "2.6.1.0",
        KernelVersion = "6.6.87.2",
        DefaultVersion = 2,
    };

    public IReadOnlyList<RivalEngine> RivalEngines { get; init; } = [];

    /// <summary>
    /// The healthy machine's CLI reaches this engine. `default` is what a machine that never had a
    /// rival reports, and it is the pipe this engine serves.
    /// </summary>
    public DockerClientTarget DockerClient { get; init; } = new()
    {
        ContextName = "default",
        Host = "npipe:////./pipe/docker_engine",
    };
}
