namespace DockerDesk.Core.Preflight;

/// <summary>
/// One row of the report: a fact, a verdict, and the single action that changes it.
/// </summary>
/// <remarks>
/// The shape is the screen. A row with a verdict but no <see cref="Detail"/> reads as a progress
/// bar, and a failing row with no <see cref="Remedy"/> sends the user to a search engine — which
/// is what an installer that copies files onto a machine that cannot host an engine already does.
/// </remarks>
public sealed record PreflightCheck
{
    /// <summary>Stable identifier, for a caller that wants one row rather than the report.</summary>
    public required string Id { get; init; }

    /// <summary>The question this row answers, as a person would name it.</summary>
    public required string Title { get; init; }

    /// <summary>The verdict.</summary>
    public required Verdict Verdict { get; init; }

    /// <summary>What was actually read — the number, the version, the path.</summary>
    public required string Detail { get; init; }

    /// <summary>
    /// The one action that changes <see cref="Detail"/>, or <see langword="null"/> where there is
    /// nothing to do.
    /// </summary>
    public string? Remedy { get; init; }

    /// <summary>
    /// Whether this row can stop an install. A row that is merely informative is not blocking,
    /// however red it looks.
    /// </summary>
    public bool Blocking { get; init; }

    /// <summary>
    /// Whether this row stops an install <em>right now</em>: blocking, and not green.
    /// <see cref="Verdict.Unknown"/> counts, because the alternative is copying files on the
    /// strength of a question nobody could answer.
    /// </summary>
    public bool Blocks => Blocking && Verdict is Verdict.Fail or Verdict.Unknown;
}
