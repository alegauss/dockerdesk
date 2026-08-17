namespace FreeWilly.Core.Engine;

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

    /// <summary>
    /// Whether this reading is evidence about the engine, rather than the absence of evidence
    /// (DD134).
    /// </summary>
    /// <remarks>
    /// Not every non-Running answer means the same thing, and treating them as though they did is
    /// what let a loaded machine talk the engine host into killing a working daemon. A reading is
    /// conclusive when it came from something load cannot forge — the held process handle, or a
    /// probe that actually answered — and inconclusive when all that happened is that nothing
    /// replied in time.
    ///
    /// <para>The default is the cautious one. A status assembled anywhere that has not thought
    /// about this says "I do not know", which costs a caller a wait; saying the opposite by default
    /// would cost them the engine.</para>
    /// </remarks>
    public bool Conclusive { get; init; }
}
