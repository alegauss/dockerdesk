namespace FreeWilly.Core.Engine;

/// <summary>
/// Turns a run of polls into the decision to stop serving, so one slow answer is not mistaken for a
/// dead engine (DD133).
/// </summary>
/// <remarks>
/// <c>--run</c> polls <see cref="EngineLifecycle.StatusAsync"/> every couple of seconds and comes
/// down when the engine is no longer there, which is right: a <c>--stop</c> from another process, or
/// a distribution terminated by hand, has to bring the pipe down with it or clients keep connecting
/// to a relay with nothing behind it.
///
/// <para><b>What was wrong is that one poll decided it.</b> The poll is a real Engine API request
/// over the pipe, and every connection through the relay spawns a <c>wsl.exe</c> running
/// <c>socat</c> — see <see cref="WslSocatBackend.Open"/>. Under a long <c>compose up --build</c>
/// the machine is already spawning those as fast as the build asks for them, and the ping's own
/// three seconds is then a budget for process creation on a saturated machine rather than for the
/// daemon. It runs out while the daemon is perfectly healthy, and the answer that came back said
/// Starting.</para>
///
/// <para>The cost of believing it was the whole of DD133: <see cref="EngineLifecycle.StopAsync"/>
/// disposes the relay and terminates the distribution, so a build lost its engine mid-flight and
/// every command after it failed with the pipe <em>absent</em> rather than refusing — which is
/// exactly what a caller sees when nothing is serving the name any more.</para>
///
/// <para><b>So a run of quiet polls is what counts, not one.</b> Any answer of Running clears the
/// run, because the engine proved itself by replying. The tolerance below is deliberately longer
/// than the worst poll: nothing is waiting on <c>--run</c> noticing, so being slow to come down
/// costs a caller nothing, while being quick to come down costs them the build.</para>
/// </remarks>
public sealed class EngineWatch
{
    /// <summary>How long the engine may stay quiet before this stops believing in it.</summary>
    /// <remarks>
    /// Expressed as a count of polls rather than a clock, because the poll interval is a constant
    /// beside it and a count is what a test can drive without waiting. At the two seconds between
    /// polls and the three the ping is given, six of them is between twelve and thirty seconds of
    /// unbroken silence — the far end of which is the window DD133 asked the answer to survive.
    /// </remarks>
    public const int ToleratedQuietPolls = 6;

    /// <summary>How many polls in a row have failed to find the engine.</summary>
    public int QuietPolls { get; private set; }

    /// <summary>
    /// Fold one poll in, and say whether to carry on serving.
    /// </summary>
    /// <param name="now">What the poll observed.</param>
    /// <returns><see langword="true"/> to keep serving the pipe.</returns>
    /// <remarks>
    /// Two answers rather than one, since DD134. Before it, every state that was not Running was
    /// tolerated identically — correct given what a status could say at the time, because Stopped
    /// was reachable through <c>wsl --list</c> timing out and so carried no more information than
    /// silence did. The tolerance was the only defence, and it was a clock: hold out long enough
    /// and a slow machine still talks this into killing a working daemon, which is what it did.
    ///
    /// <para>What changed is that a status now says whether it is
    /// <see cref="EngineStatus.Conclusive"/>. Evidence ends the watch at once, because the only
    /// readings that qualify came from a local process handle or a probe that answered, and
    /// counting six of those would just be slow. Everything load can manufacture is inconclusive by
    /// construction and never reaches that branch — so the run of quiet polls below now guards
    /// against a stall it can actually be caused by, and nothing else.</para>
    /// </remarks>
    public bool KeepServing(EngineStatus now)
    {
        ArgumentNullException.ThrowIfNull(now);

        if (now.State is EngineState.Running)
        {
            QuietPolls = 0;
            return true;
        }

        QuietPolls++;
        return !now.Conclusive && QuietPolls < ToleratedQuietPolls;
    }

    /// <summary>What to say about the run of silence that ended the watch.</summary>
    /// <param name="last">The last thing observed.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// The count is in the sentence because without it the line is the one <c>--run</c> printed
    /// before DD133, and that line was indistinguishable from the false alarm it usually was. A
    /// reader who sees six needs to know the engine was asked six times.
    ///
    /// <para>And it is only in the sentence when it means something (DD134). A conclusive reading
    /// ended the watch on its own merits, so appending "1 polls in a row" to it would suggest a run
    /// of silence that never happened — and send the reader looking for a load problem instead of
    /// reading the detail, which in that case already names the cause.</para>
    /// </remarks>
    public string WhyItStopped(EngineStatus last)
    {
        ArgumentNullException.ThrowIfNull(last);
        return last.Conclusive
            ? $"{last.State,-8}  {last.Detail}"
            : $"{last.State,-8}  {last.Detail} — {QuietPolls} polls in a row";
    }
}
