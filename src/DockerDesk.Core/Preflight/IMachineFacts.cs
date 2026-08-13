namespace DockerDesk.Core.Preflight;

/// <summary>
/// What this machine says about itself. The seam between reading Windows and judging it.
/// </summary>
/// <remarks>
/// Every property is a fact and none is a verdict, so <see cref="PreflightInspection"/> stays a pure
/// function of this interface. That is the only way the report's own failure modes are testable:
/// a machine with virtualization switched off in firmware cannot be produced on demand, and a
/// check that has only ever been observed passing has demonstrated nothing.
/// </remarks>
public interface IMachineFacts
{
    /// <summary>The Windows version, whose <c>Build</c> is the field that decides.</summary>
    Version OperatingSystem { get; }

    /// <summary>
    /// Whether firmware exposes hardware virtualization, or <see langword="null"/> where the
    /// question could not be asked. Reported <see langword="false"/> by Windows while a
    /// hypervisor is running, which is why <see cref="HypervisorPresent"/> is read first.
    /// </summary>
    bool? VirtualizationFirmwareEnabled { get; }

    /// <summary>
    /// Whether a hypervisor is already running, or <see langword="null"/> where the question
    /// could not be asked.
    /// </summary>
    /// <remarks>
    /// True is proof that a hypervisor is running, and not proof that this machine can host one:
    /// it is equally true of every guest. <see cref="IsVirtualMachine"/> is the fact that separates
    /// the two, and reading this one alone is the defect DD19 fixed.
    /// </remarks>
    bool? HypervisorPresent { get; }

    /// <summary>
    /// Whether this machine is itself a guest, or <see langword="null"/> where the question could
    /// not be asked.
    /// </summary>
    /// <remarks>
    /// Inside a guest, nothing readable from in here says whether the host exposed nested
    /// virtualization, so a row that knows only this has to answer Unknown rather than guess.
    /// </remarks>
    bool? IsVirtualMachine { get; }

    /// <summary>What is installed of WSL.</summary>
    WslInstallation Wsl { get; }

    /// <summary>
    /// Container engines already on this machine. Empty is the state an install wants.
    /// </summary>
    IReadOnlyList<RivalEngine> RivalEngines { get; }

    /// <summary>Where this user's own <c>docker</c> command will send a request.</summary>
    DockerClientTarget DockerClient { get; }
}

/// <summary>Where the <c>docker</c> CLI points, as facts.</summary>
/// <remarks>
/// Read rather than judged: whether this is a problem depends on the pipe this engine serves, and
/// that comparison belongs to the report.
/// </remarks>
public sealed record DockerClientTarget
{
    /// <summary>
    /// The active context's name, or <see langword="null"/> where <c>DOCKER_HOST</c> decided and no
    /// context was consulted.
    /// </summary>
    public string? ContextName { get; init; }

    /// <summary>
    /// The endpoint the CLI will use, or <see langword="null"/> where it could not be worked out.
    /// </summary>
    public string? Host { get; init; }

    /// <summary>
    /// Whether <c>DOCKER_HOST</c> is what decided. It outranks the active context, so a remedy that
    /// says "switch context" would change nothing while this is true.
    /// </summary>
    public bool FromEnvironment { get; init; }

    /// <summary>Why the endpoint is absent, when something was wrong rather than unset.</summary>
    public string? Unreadable { get; init; }
}

/// <summary>What was found of WSL, as facts.</summary>
public sealed record WslInstallation
{
    /// <summary>Whether <c>wsl.exe</c> is on disk at all.</summary>
    public bool CommandPresent { get; init; }

    /// <summary>
    /// What <c>wsl --status</c> said about the feature itself being installed:
    /// <see langword="false"/> where it reported it is not, <see langword="true"/> where it
    /// answered normally, and <see langword="null"/> where it could not be asked.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="CommandPresent"/>, and the distinction is the whole of DD18:
    /// <c>wsl.exe</c> ships in System32 on a Windows 11 that never had WSL, so its presence was
    /// never the answer. This is the cheap question that separates "the launcher is here and the
    /// feature is not" from "half installed", and asking it first is what stops the row costing
    /// fifteen seconds to say nothing.
    /// </remarks>
    public bool? FeatureInstalled { get; init; }

    /// <summary>
    /// The WSL version reported by <c>wsl --version</c>, or <see langword="null"/> on an
    /// installation too old to answer it — which is itself the answer.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>The Linux kernel version WSL2 will boot, when one is installed.</summary>
    public string? KernelVersion { get; init; }

    /// <summary>
    /// The WSL version a new distro is created at: 1 or 2, or <see langword="null"/> where
    /// nothing has recorded a default yet.
    /// </summary>
    public int? DefaultVersion { get; init; }

    /// <summary>
    /// Why the fields above are absent, when something went wrong reading them rather than
    /// there being nothing to read. Distinguishes "WSL2 is missing" from "we could not look".
    /// </summary>
    public string? Unreadable { get; init; }
}

/// <summary>An engine that already owns what this one would take.</summary>
/// <param name="Name">The product, as its own installer names it.</param>
/// <param name="Evidence">The path, pipe or key it was found by — so the report can be argued with.</param>
public sealed record RivalEngine(string Name, string Evidence);
