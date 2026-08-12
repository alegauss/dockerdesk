namespace DockerDesk.Core.Engine;

/// <summary>The three states a user cares about.</summary>
/// <remarks>
/// <see cref="Starting"/> exists because WSL2 needs seconds to boot the distribution before
/// <c>dockerd</c> opens its socket. A UI that shows Running the moment the start command returns is
/// lying for the length of that gap, and the user's first <c>docker ps</c> fails.
/// </remarks>
public enum EngineState
{
    /// <summary>Nothing is listening and nothing is on its way up.</summary>
    Stopped,

    /// <summary>Asked to start, and the pipe has not answered yet.</summary>
    Starting,

    /// <summary>The pipe answered. This is the only state that means the engine is usable.</summary>
    Running,
}

/// <summary>What the engine is doing, and what was read to decide that.</summary>
/// <param name="State">The state.</param>
/// <param name="Detail">What was observed — never a guess, and never a restatement of the state.</param>
/// <param name="ApiVersion">
/// The Engine API version the daemon reported, when it answered. Proof rather than inference: it
/// can only be known by having talked to the thing.
/// </param>
public sealed record EngineStatus(EngineState State, string Detail, string? ApiVersion = null)
{
    /// <summary>Whether a client can make an API call right now.</summary>
    public bool Usable => State is EngineState.Running;
}
