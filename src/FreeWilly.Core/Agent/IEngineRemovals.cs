namespace FreeWilly.Core.Agent;

/// <summary>
/// The half of the engine that takes things away, which only a <c>do</c> verb ever sees.
/// </summary>
/// <remarks>
/// DD29. The mirror of <see cref="IEngineReads"/> and for the same reason: a reclaim needs to remove,
/// so it is handed a handle that can, and everything else on this surface is handed one that cannot.
/// Deliberately two methods rather than the whole <c>DockerApi</c> — a reclaim has no business
/// starting a container, and the narrow handle is also what lets a test count exactly what was removed
/// without a daemon that can be tricked into agreeing.
/// </remarks>
public interface IEngineRemovals : IEngineReads
{
    /// <summary>Remove one container.</summary>
    /// <param name="id">Its id or name.</param>
    /// <param name="force">Whether to kill it first.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    Task RemoveContainerAsync(
        string id, bool force = false, CancellationToken cancellation = default);

    /// <summary>Remove one volume.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    Task RemoveVolumeAsync(string name, CancellationToken cancellation = default);
}
