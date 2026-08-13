using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;

namespace DockerDesk.Tray.Ui;

/// <summary>One published port, and where clicking it goes.</summary>
/// <param name="Text">What the cell reads, e.g. <c>8080-&gt;80/tcp</c>.</param>
/// <param name="Url">
/// Where to open, or <see langword="null"/> when there is nowhere: a port the container exposes and
/// nothing published has no address on this machine, so it is text and not a link.
/// </param>
public sealed record PortLink(string Text, string? Url)
{
    /// <summary>Whether this one can be clicked.</summary>
    public bool IsLink => Url is not null;

    /// <summary>Read a binding.</summary>
    /// <param name="port">The binding.</param>
    /// <returns>The cell.</returns>
    public static PortLink From(PortBinding port)
    {
        ArgumentNullException.ThrowIfNull(port);

        // Only TCP gets a link. A published UDP port is real and http://localhost:x is not where it
        // is, and a link that opens a browser onto nothing is worse than plain text.
        var url = port.PublicPort is { } published
            && port.Type.Equals("tcp", StringComparison.OrdinalIgnoreCase)
                ? $"http://localhost:{published}"
                : null;

        return new PortLink(port.ToString(), url);
    }
}

/// <summary>A container, as the window shows it.</summary>
/// <param name="Name">The name, or the short id when it has none.</param>
/// <param name="Image">The image.</param>
/// <param name="State">One word.</param>
/// <param name="Status">The daemon's own sentence: <c>Up 3 minutes</c>, <c>Exited (0) …</c>.</param>
/// <param name="Ports">The ports, deduplicated, each with its link or without one.</param>
/// <param name="Id">The full id, which is what an action is addressed to.</param>
public sealed record ContainerRow(
    string Name,
    string Image,
    string State,
    string Status,
    IReadOnlyList<PortLink> Ports,
    string Id)
{
    /// <summary>
    /// What this row is waiting for — <c>Stopping…</c> — or <see langword="null"/> when idle.
    /// </summary>
    /// <remarks>
    /// Set the moment the button is pressed and cleared by the event that confirms it, because a
    /// stop can take the daemon's full ten-second grace period and a row that sits unchanged for
    /// ten seconds reads as a click that did nothing.
    /// </remarks>
    public string? Pending { get; init; }

    /// <summary>The engine's own sentence about why the last action failed, or nothing.</summary>
    public string? Failure { get; init; }

    /// <summary>Whether this container is running now.</summary>
    public bool IsRunning => State.Equals("running", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the container is up in any sense the four verbs care about.
    /// </summary>
    /// <remarks>
    /// Paused and restarting are not <c>running</c> and are not stopped either. Offering "Start" for
    /// them would be a button whose only outcome is a 409 from the daemon.
    /// </remarks>
    public bool IsLive => State.Equals("running", StringComparison.OrdinalIgnoreCase)
        || State.Equals("paused", StringComparison.OrdinalIgnoreCase)
        || State.Equals("restarting", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether an action is in flight on this row.</summary>
    public bool IsPending => Pending is not null;

    /// <summary>Whether the last action on this row failed.</summary>
    public bool HasFailure => Failure is not null;

    /// <summary>Whether the row offers Start.</summary>
    public bool CanStart => !IsPending && !IsLive;

    /// <summary>Whether the row offers Stop.</summary>
    public bool CanStop => !IsPending && IsLive;

    /// <summary>Whether the row offers Restart.</summary>
    public bool CanRestart => !IsPending && IsLive;

    /// <summary>Whether the row offers Remove. Anything not already busy can be removed.</summary>
    public bool CanRemove => !IsPending;

    /// <summary>
    /// Whether a shell can be opened: running, and nothing else in flight.
    /// </summary>
    /// <remarks>
    /// Running and not merely live. <c>docker exec</c> against a paused container is accepted and
    /// then hangs until somebody unpauses it, with no window and no error — which is worse than a
    /// button that is visibly off.
    /// </remarks>
    public bool CanShell => !IsPending && IsRunning;

    /// <summary>
    /// Why the Shell button is off, or what it does when it is on.
    /// </summary>
    /// <remarks>
    /// Unlike the other four, this one is shown disabled rather than hidden, so it owes an
    /// explanation: a disabled control that says why is a limitation, and one that does not is a
    /// bug the user is left to guess at.
    /// </remarks>
    public string ShellReason => this switch
    {
        { IsPending: true } => "Wait for the action already running on this container.",
        { IsRunning: true } => "Open a shell in this container.",
        { State: "paused" } => "This container is paused. Unpause it before opening a shell.",
        { State: "restarting" } => "This container is restarting. Wait for it to come up.",
        _ => "This container is not running. Start it before opening a shell.",
    };


    /// <summary>
    /// What the state chip asserts, in the three tones a glance tells apart (DD36).
    /// </summary>
    /// <remarks>
    /// A clean exit is muted rather than red: it stopped because it was finished, and a migration
    /// container that did its job is not a problem to draw attention to. Only a non-zero exit is bad,
    /// which is the distinction the tertiary grey it used to be drawn in could not make.
    /// </remarks>
    public RowTone Tone => this switch
    {
        { IsRunning: true } => RowTone.Good,
        { State: "paused" } or { State: "restarting" } or { State: "created" } => RowTone.Warn,
        _ when ExitCode is 0 => RowTone.Muted,
        { IsLive: false } => RowTone.Bad,
        _ => RowTone.Muted,
    };

    /// <summary>
    /// The exit code the daemon put in the status line, where it put one there.
    /// </summary>
    /// <remarks>
    /// Parsed out of <c>Exited (137) 12 seconds ago</c> rather than asked for: it is already in the
    /// list response, and an inspect per row to read one integer is the call this window does not make.
    /// </remarks>
    public int? ExitCode
    {
        get
        {
            var open = Status.IndexOf('(', StringComparison.Ordinal);
            var close = Status.IndexOf(')', StringComparison.Ordinal);
            return open >= 0 && close > open + 1
                   && int.TryParse(
                       Status.AsSpan(open + 1, close - open - 1),
                       System.Globalization.NumberStyles.Integer,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out var code)
                ? code
                : null;
        }
    }

    /// <summary>
    /// Why the chip says what it says.
    /// </summary>
    /// <remarks>
    /// A chip is an assertion, so it owes evidence — and the evidence has to be more than the status
    /// column already shows, or the tooltip is the same sentence twice. 137 is the one worth spelling
    /// out: it is SIGKILL, it is what the kernel's memory limit looks like from here, and it is the
    /// exit code the diagnostic half of this product exists for.
    /// </remarks>
    public string StateEvidence => this switch
    {
        { IsRunning: true } => Status.Length > 0 ? Status : "The daemon reports this container running.",
        { State: "paused" } => "Paused: its processes are frozen, not stopped.",
        { State: "restarting" } => "Restarting: the daemon is bringing it back up.",
        { State: "created" } => "Created and never started.",
        { ExitCode: 0 } => "Exited 0 — it finished and meant to.",
        { ExitCode: 137 } => "Exited 137 — SIGKILL. Usually the memory limit, sometimes a stop that "
            + "ran out of its grace period.",
        { ExitCode: { } code } => $"Exited {code.ToString(System.Globalization.CultureInfo.InvariantCulture)} "
            + "— it stopped and did not mean to. Its log is the next thing to read.",
        _ => Status,
    };

    /// <summary>The fill the chip is drawn with, set once per render from <see cref="RowStyle"/>.</summary>
    public System.Windows.Media.Brush? ChipFill { get; init; }

    /// <summary>What the chip's word is written in.</summary>
    public System.Windows.Media.Brush? ChipText { get; init; }

    /// <summary>
    /// The one verb this row is opened for, beside Logs.
    /// </summary>
    /// <remarks>
    /// The other three moved behind the overflow (DD36): six word captions per row is two hundred of
    /// them on a list of forty, and the eye has nothing to skip past. Which one this is depends on the
    /// state, because a running container is opened to be stopped and a stopped one to be started.
    /// </remarks>
    public string PrimaryVerb => CanStop ? "Stop" : "Start";

    /// <summary>Whether the primary verb is offered at all.</summary>
    public bool HasPrimary => CanStop || CanStart;

    /// <summary>Dress this row in the theme's brushes.</summary>
    /// <param name="style">The brushes, resolved once for the whole render.</param>
    /// <returns>The row, with its chip filled in.</returns>
    public ContainerRow WithChip(RowStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return this with { ChipFill = style.Fill(Tone), ChipText = style.Text(Tone) };
    }

    /// <summary>Project one summary.</summary>
    /// <param name="container">What the API returned.</param>
    /// <returns>The row.</returns>
    public static ContainerRow From(ContainerSummary container)
    {
        ArgumentNullException.ThrowIfNull(container);

        // Deduplicated the same way the list renders them: a port published on both address
        // families comes back twice and would otherwise be two identical cells.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ports = new List<PortLink>();
        foreach (var port in container.Ports)
        {
            var link = PortLink.From(port);
            if (seen.Add(link.Text))
            {
                ports.Add(link);
            }
        }

        return new ContainerRow(
            container.DisplayName,
            container.Image,
            container.State,
            container.Status,
            ports,
            container.Id);
    }
}

/// <summary>What the window says when the list is empty.</summary>
/// <param name="Headline">One line, large.</param>
/// <param name="Detail">One sentence under it.</param>
/// <param name="OffersStart">Whether the empty state should offer to start the engine.</param>
public sealed record EmptyState(string Headline, string Detail, bool OffersStart)
{
    /// <summary>
    /// The empty state for an engine in <paramref name="engine"/>.
    /// </summary>
    /// <remarks>
    /// Empty is a designed state and not a blank grid. The first screen a new user sees is usually
    /// this one, and a table with headers and nothing under them is where a free alternative loses
    /// them — so the two reasons a list is empty say different things, and only one of them is
    /// something the user can act on.
    /// </remarks>
    /// <param name="engine">What the engine is doing.</param>
    /// <returns>What to show.</returns>
    public static EmptyState For(EngineState engine) => engine switch
    {
        EngineState.Running => new EmptyState(
            "No containers",
            "The engine is running and has nothing to show. Start one and it appears here.",
            OffersStart: false),

        EngineState.Starting => new EmptyState(
            "Starting the engine",
            "This takes a few seconds while WSL2 boots the distribution.",
            OffersStart: false),

        _ => new EmptyState(
            "The engine is not running",
            "Start it to see your containers.",
            OffersStart: true),
    };
}
