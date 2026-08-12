namespace DockerDesk.Core.Preflight;

/// <summary>The verdict on one row of the preflight report.</summary>
public enum Verdict
{
    /// <summary>The fact was read, and it is what this machine needs.</summary>
    Pass,

    /// <summary>
    /// The fact was read, and it is not. <see cref="PreflightCheck.Remedy"/> carries the one
    /// action that changes it.
    /// </summary>
    Fail,

    /// <summary>
    /// Read, and neither: worth knowing, and not a reason to stop.
    /// </summary>
    Warn,

    /// <summary>
    /// Not readable here. Never folded into <see cref="Pass"/>: an unread fact is not a green
    /// row, and a report that says "fine" about a question it could not ask is the failure this
    /// check exists to prevent.
    /// </summary>
    Unknown,
}
