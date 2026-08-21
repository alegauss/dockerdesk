namespace FreeWilly.Core.Engine;

/// <summary>
/// Whether an engine that went away is worth another attempt, and how long to wait first (DD136,
/// DD164).
/// </summary>
/// <remarks>
/// WSL2 does not survive every suspend. A laptop that sleeps with containers running wakes with the
/// virtual machine gone, and before this the only thing that noticed was the user finding a dead
/// <c>docker ps</c> — the engine host was awake and polling the whole time, and all it was allowed
/// to do about it was give up.
///
/// <para><b>The wait grows because the failures that repeat are the slow ones.</b> A distribution
/// that has just been terminated comes back in a second or two; a machine still settling after a
/// resume needs longer, and asking it four times a second while it does is a way of making the thing
/// you are waiting for slower. Doubling from <see cref="FirstWait"/> spends about a minute in total
/// across <see cref="Attempts"/>, which is long enough to cover a resume and short enough that a
/// user watching the tray sees it resolve.</para>
///
/// <para><b>Running out of those attempts used to end the host, and DD164 is why it no longer
/// does.</b> The reasoning was that an engine which cannot come up is a fact the user needs, and
/// that a loop retrying forever converts that fact into a machine quietly doing nothing. The first
/// half is right. The second described the wrong loop: measured on 21 August 2026 the host spent
/// seven minutes on its five attempts, wrote that it had given up, exited — and the machine then
/// sat offline for an hour until somebody clicked Start. The fact reached nobody, because it was
/// written to a file nobody had been told to open by a process that then stopped existing, and the
/// outcome was exactly the silence the bound was meant to prevent.</para>
///
/// <para><b>So giving up and stopping are now two things.</b> The five quick attempts are unchanged
/// and running out of them is still announced; what follows is <see cref="PatientWait"/> rather
/// than an exit, and the host keeps trying at that interval until the engine is back or somebody
/// asks it to stop. A machine whose distribution is genuinely broken is no worse off — it gets the
/// same sentence, in the same place — and a machine that recovers gets its engine back without a
/// click.</para>
///
/// <para>Counted rather than timed, for the reason <see cref="EngineWatch.ToleratedQuietPolls"/> is:
/// a count is what a test can drive without waiting.</para>
/// </remarks>
public sealed class EngineRevival
{
    /// <summary>How many consecutive failures spend the quick attempts.</summary>
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

    /// <summary>
    /// How long the host leaves between attempts once the quick ones are spent (DD164).
    /// </summary>
    /// <remarks>
    /// Five minutes, and the number is chosen against what is actually being waited for. What
    /// repairs a machine in this state is a person — closing whatever took the pipe, freeing a
    /// disk, running <c>wsl --shutdown</c> — or Windows finishing something it was in the middle
    /// of. None of that resolves in seconds, so a shorter interval would spend a
    /// <c>wsl --terminate</c> and a sixty-second start budget over and over on a machine that
    /// cannot yet answer, which is the hammering <see cref="LongestWait"/> exists to prevent
    /// written large.
    ///
    /// <para>And it bounds how long a recovered machine stays down. Five minutes is short enough
    /// that somebody who fixed the problem does not go looking for the menu item, and long enough
    /// that a laptop left broken overnight wakes up having spent a couple of hundred attempts
    /// rather than a hundred thousand.</para>
    /// </remarks>
    public static readonly TimeSpan PatientWait = TimeSpan.FromMinutes(5);

    /// <summary>How many attempts in a row have failed since the last time it came back.</summary>
    public int Failures { get; private set; }

    /// <summary>How many times the engine has been brought back over this host's life.</summary>
    public int Revivals { get; private set; }

    /// <summary>Whether one of the quick attempts is still owed.</summary>
    public bool WorthAnotherTry => Failures < Attempts;

    /// <summary>
    /// Whether the quick attempts are spent and this has settled into waiting (DD164).
    /// </summary>
    public bool Patient => Failures >= Attempts;

    /// <summary>
    /// Whether the failure just recorded is the one that spent the last quick attempt (DD164).
    /// </summary>
    /// <remarks>
    /// True on exactly one call, which is what makes it the right thing to announce on. The host
    /// says once that it is slowing down; saying it again every five minutes would fill the journal
    /// with a machine repeating itself, and that file is worth opening because everything in it is
    /// something that happened.
    /// </remarks>
    public bool JustRanOutOfQuickAttempts => Failures == Attempts;

    /// <summary>How long to wait before the next attempt.</summary>
    public TimeSpan Wait
    {
        get
        {
            if (Patient)
            {
                return PatientWait;
            }

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

    /// <summary>What to say on settling into the long wait (DD164).</summary>
    /// <param name="last">The state the last quick attempt reached.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// The count is in the sentence for the reason it is in
    /// <see cref="EngineWatch.WhyItStopped"/>: a host that has tried five times and one that has
    /// not tried at all are different machines to be sitting in front of, and the detail alone does
    /// not tell them apart.
    ///
    /// <para>The interval is in it too, and that is what DD164 changed. This sentence used to end
    /// the file — the host said it and exited, and a reader who found it an hour later had no way
    /// to know whether anything was still watching. Naming what happens next is the difference
    /// between a log that reports an ending and one that reports a state.</para>
    /// </remarks>
    public string WhyItIsSlowingDown(EngineStatus last)
    {
        ArgumentNullException.ThrowIfNull(last);
        return $"{last.State,-8}  {last.Detail} — {Failures} attempts have failed; still trying, "
            + $"now every {PatientWait.TotalMinutes:0} minutes";
    }
}
