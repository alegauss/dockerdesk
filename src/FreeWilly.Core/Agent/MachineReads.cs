using FreeWilly.Core.Preflight;
using FreeWilly.Core.Preflight.Windows;

namespace FreeWilly.Core.Agent;

/// <summary>Where this user's own <c>docker</c> command points, behind a seam.</summary>
/// <remarks>
/// <see cref="DockerContextProbe"/> reads a per-user config file and an environment variable, so the
/// answer differs between a developer's machine and a runner with no Docker on it. That is the right
/// behaviour and the wrong input to a measurement — hence the interface, shaped after
/// <see cref="IServiceProbe"/>.
/// </remarks>
public interface IContextProbe
{
    /// <summary>Read where the CLI points.</summary>
    /// <returns>The target.</returns>
    DockerClientTarget Read();
}

/// <summary>The real read, which is this machine's own config.</summary>
public sealed class ContextProbe : IContextProbe
{
    /// <inheritdoc/>
    public DockerClientTarget Read() => DockerContextProbe.Read();
}

/// <summary>
/// What a read verb learns from Windows rather than from the engine (DD78).
/// </summary>
/// <remarks>
/// Every other input to a read verb arrives through <c>IEngineReads</c>, which a measurement can
/// serve from fixtures. These two could not: <c>read context</c> constructed a
/// <see cref="WindowsMachineFacts"/> to name the CLI's context and <c>read doctor</c> constructed a
/// <see cref="HostPorts"/> to ask whether anything held the container's published port, both inside
/// the verb where nothing could reach them.
///
/// <para><b>What that cost.</b> DD65's shaped token figure had to be banded at 15% to survive the
/// two, against a measured variance of about 5% — so a response that grew by 100 tokens landed inside
/// the band and the gate said nothing. A gate is only as tight as its least deterministic input, and
/// these were it.</para>
///
/// <para><b><c>read verify</c>'s probe is here too</b>, though it was not one of the two: it connects
/// to a host port, the dispatcher constructed it, and it stays silent on the measured task only
/// because that fixture's container is exited and nothing is probed for one of those. A seam that
/// holds while a fixture does is not a seam.</para>
///
/// <para><b>Lazy on purpose.</b> Every member is a seam rather than a value, so a verb that needs
/// none reads none. Constructing this must not make <c>read ps</c> open a config file.</para>
/// </remarks>
public sealed class MachineReads
{
    /// <summary>This machine, which is what every caller but a measurement wants.</summary>
    public static MachineReads OfThisMachine { get; } = new();

    /// <summary>What Windows is listening on.</summary>
    public IHostPorts Ports { get; init; } = new HostPorts();

    /// <summary>Where the user's own <c>docker</c> command points.</summary>
    public IContextProbe Client { get; init; } = new ContextProbe();

    /// <summary>What reaches a published port from Windows.</summary>
    public IServiceProbe Service { get; init; } = new ServiceProbe();
}
