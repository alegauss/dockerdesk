namespace FreeWilly.Core.Engine;

/// <summary>
/// Whether an engine that went away is worth another attempt, and how long to wait first (DD136).
/// </summary>
/// <remarks>
/// WSL2 does not survive every suspend. A laptop that sleeps with containers running wakes with the
/// virtual machine gone, and before this the only thing that noticed was the user finding a dead
/// <c>docker ps</c> — the engine host was awake and polling the whole time, and all it was allowed
/// to do about it was give up.
///
/// <para><b>Bounded, because the alternative hides a real failure.</b> An engine that cannot come up
/// — a corrupted distribution, a disk with nothing left on it, a pipe another daemon has taken — is
/// a fact the user needs, and a loop that retries it forever converts that fact into a machine
/// quietly doing nothing. So the attempts are counted, the wait between them grows, and running out
/// is an ending with a sentence attached rather than a silence.</para>
///
/// <para><b>The wait grows because the failures that repeat are the slow ones.</b> A distribution
/// that has just been terminated comes back in a second or two; a machine still settling after a
/// resume needs longer, and asking it four times a second while it does is a way of making the thing
/// you are waiting for slower. Doubling from <see cref="FirstWait"/> spends about a minute in total
/// across <see cref="Attempts"/>, which is long enough to cover a resume and short enough that a
/// user watching the tray sees it resolve.</para>
///
/// <para>Counted rather than timed, for the reason <see cref="EngineWatch.ToleratedQuietPolls"/> is:
/// a count is what a test can drive without waiting.</para>
/// </remarks>
public sealed class EngineRevival
{
    /// <summary>How many consecutive failures end the attempt to bring it back.</summary>
    public const int Attempts = 5;

    /// <summary>How long to wait before the first retry.</summary>
    public static readonly TimeSpan FirstWait = TimeSpan.FromSeconds(2);

    /// <summary>The longest the wait grows to, however many times it has failed.</summary>
    /// <remarks>
    /// A ceiling and not a target. Without it the doubling reaches minutes by the last attempt, and
    /// a user who fixed whatever was wrong would sit in front of a working machine watching nothing
    /// happen — the back-off exists to stop hammering a busy machine, not to punish a slow one.
    /// </remarks>
    public static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(30);

    /// <summary>How many attempts in a row have failed since the last time it came back.</summary>
    public int Failures { get; private set; }

    /// <summary>How many times the engine has been brought back over this host's life.</summary>
    public int Revivals { get; private set; }

    /// <summary>Whether another attempt is owed.</summary>
    public bool WorthAnotherTry => Failures < Attempts;

    /// <summary>How long to wait before the next attempt.</summary>
    public TimeSpan Wait
    {
        get
        {
            // Shifted rather than Math.Pow, and clamped before the multiply: at a dozen failures the
            // doubling would overflow the TimeSpan long before the cap could catch it. Failures is
            // bounded by Attempts in practice, but a number that is only safe because of a check
            // somewhere else is the kind that stops being safe when that check moves.
            var doublings = Math.Min(Failures, 16);
            var grown = FirstWait * (1L << doublings);
            return grown > LongestWait ? LongestWait : grown;
        }
    }

    /// <summary>Record that the engine came back.</summary>
    public void Revived()
    {
        Failures = 0;
        Revivals++;
    }

    /// <summary>Record that an attempt did not bring it back.</summary>
    public void Failed() => Failures++;

    /// <summary>What to say about giving up.</summary>
    /// <param name="last">The state the final attempt reached.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// The count is in the sentence for the reason it is in
    /// <see cref="EngineWatch.WhyItStopped"/>: a host that came down after trying five times and one
    /// that came down without trying at all are different machines to be sitting in front of, and
    /// the detail alone does not tell them apart.
    /// </remarks>
    public string WhyItGaveUp(EngineStatus last)
    {
        ArgumentNullException.ThrowIfNull(last);
        return $"{last.State,-8}  {last.Detail} — gave up after {Failures} attempts to restart it";
    }
}
