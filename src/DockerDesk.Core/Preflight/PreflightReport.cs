using System.Text.Json.Serialization;

namespace DockerDesk.Core.Preflight;

/// <summary>
/// Every row, and the one question an installer asks of them.
/// </summary>
/// <param name="Checks">The rows, in the order they are meant to be read.</param>
public sealed record PreflightReport(IReadOnlyList<PreflightCheck> Checks)
{
    /// <summary>
    /// Whether this machine can host a container engine — true only when no blocking row is
    /// anything other than green. Nothing is copied to disk while this is false.
    /// </summary>
    public bool CanHostEngine => Blockers.Count == 0;

    /// <summary>The blocking rows that are not green, in report order.</summary>
    /// <remarks>
    /// Left out of the JSON: every row here is already in <see cref="Checks"/> carrying
    /// <see cref="PreflightCheck.Blocks"/>, and a consumer that reads both can be told two things.
    /// </remarks>
    [JsonIgnore]
    public IReadOnlyList<PreflightCheck> Blockers { get; } =
        [.. Checks.Where(check => check.Blocks)];

    /// <summary>The one row with this id, or <see langword="null"/>.</summary>
    public PreflightCheck? this[string id] =>
        Checks.FirstOrDefault(check => check.Id == id);
}
