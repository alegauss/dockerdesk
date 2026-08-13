using DockerDesk.Core.Api;

namespace DockerDesk.Core.Agent;

/// <summary>
/// The half of the engine a <c>read</c> verb is allowed to see.
/// </summary>
/// <remarks>
/// DD24. <c>read</c> is a promise, not a naming convention: a verb under it that writes is a defect.
/// Two things enforce that, and both are needed because they fail differently.
///
/// This interface is the first: a read verb is handed one of these and not a
/// <see cref="DockerApi"/>, so <c>StartContainerAsync</c> and <c>RemoveVolumeAsync</c> are not
/// reachable from where a read verb is written. That catches the mistake at compile time, which is
/// the cheapest place.
///
/// It is not sufficient on its own — <see cref="DockerApi.StreamAsync(string, System.Threading.CancellationToken)"/>
/// is a read of any path and a determined verb could ask for one that mutates — so the second guard
/// is behavioural: every registered read verb is driven against a fake daemon and every request it
/// made has to be a GET. That one catches what a type cannot.
/// </remarks>
public interface IEngineReads
{
    /// <summary>Whether the engine is answering at all.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns><see langword="true"/> when it answered.</returns>
    Task<bool> PingAsync(CancellationToken cancellation = default);

    /// <summary>What the engine says it is.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The version.</returns>
    Task<EngineVersion> VersionAsync(CancellationToken cancellation = default);

    /// <summary>Every container, stopped ones included.</summary>
    /// <param name="all">Whether to include the ones that are not running.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The containers, in the order the daemon returned them.</returns>
    Task<IReadOnlyList<ContainerSummary>> ContainersAsync(
        bool all = true, CancellationToken cancellation = default);
}
