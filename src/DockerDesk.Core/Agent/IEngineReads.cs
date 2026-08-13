using System.IO;
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

    /// <summary>One container's whole entity tree.</summary>
    /// <param name="id">The container, by id or name.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The inspect.</returns>
    /// <remarks>
    /// On the read side because it is one, and used sparingly because DD23 measured it at 1603
    /// estimated tokens for four leaves. The context pack inspects only what is not running.
    /// </remarks>
    Task<ContainerInspect> InspectAsync(string id, CancellationToken cancellation = default);

    /// <summary>Every image the daemon holds, dangling ones included.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The images.</returns>
    Task<IReadOnlyList<ImageSummary>> ImagesAsync(CancellationToken cancellation = default);

    /// <summary>Every volume, by name, without sizing any of them.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The volumes.</returns>
    /// <remarks>
    /// Deliberately not the sizing read: <c>/system/df</c> walks the filesystem and is seconds on a
    /// machine with data on it, which a pack built to replace five cheap calls cannot spend.
    /// </remarks>
    Task<IReadOnlyList<VolumeSummary>> VolumesAsync(CancellationToken cancellation = default);

    /// <summary>A container's log, as the daemon frames it.</summary>
    /// <param name="id">The container.</param>
    /// <param name="tail">How many lines of history to open with.</param>
    /// <param name="follow">Whether to keep the stream open for new output.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The response body.</returns>
    /// <remarks>
    /// A read, and the largest one there is: the log is the biggest token sink on this surface, which is
    /// why the doctor takes a bounded tail of one stream rather than the whole thing (DD26) and why
    /// making the general read cheap is its own task (DD27).
    /// </remarks>
    Task<Stream> LogsAsync(
        string id, int tail = 2000, bool follow = true, CancellationToken cancellation = default);
}
