// System.IO is not in this project's implicit usings: enabling WinForms replaces the SDK's default
// list rather than adding to it.
using System.IO;
using System.Text;
using DockerDesk.Core.Agent;
using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;

namespace DockerDesk.Tray.Cli;

/// <summary>Which half of the surface a verb is in.</summary>
public enum AgentNamespace
{
    /// <summary>Reads. Mutates nothing, and that is a promise a test keeps.</summary>
    Read,

    /// <summary>Writes. Every one of these is worth an approval.</summary>
    Do,
}

/// <summary>One agent verb, and what it costs.</summary>
/// <param name="Namespace">Read or do.</param>
/// <param name="Name">The word after it.</param>
/// <param name="Shape">
/// The response shape's name, which has to have a ceiling in <c>agent-budget.json</c>.
/// </param>
/// <param name="Summary">One line, for the help.</param>
public sealed record AgentVerb(
    AgentNamespace Namespace, string Name, string Shape, string Summary)
{
    /// <summary>How this is typed.</summary>
    /// <returns>The two words.</returns>
    public override string ToString() =>
        $"{Namespace.ToString().ToLowerInvariant()} {Name}";
}

/// <summary>
/// The read/do split, which is the highest-leverage decision in the constitution.
/// </summary>
/// <remarks>
/// DD24. <c>docker ps</c> and <c>docker rm -f -v</c> are the same string to an allowlist, so a user
/// either grants the whole verb namespace — which permits deleting a volume — or approves every call by
/// hand. Splitting them in argv makes the rule one line of settings, and what that buys is not
/// keystrokes: most of the calls in a diagnosis mutate nothing, and each of them currently costs the
/// most expensive unit there is, which is stopping to ask.
///
/// <para><b>The table is the registry.</b> <see cref="All"/> is what the router dispatches on, what the
/// help prints, what the budget test demands a ceiling for, and what the read-only guard enumerates. A
/// verb added here is guarded and budgeted without a second edit; a verb added anywhere else is not
/// reachable at all.</para>
///
/// <para><b>The flags stay.</b> <c>--preflight</c>, <c>--status</c> and the rest are the human and
/// installer head and nothing that depends on them changes. These verbs call the same methods
/// underneath rather than a copy of them, so there are two spellings and one behaviour.</para>
/// </remarks>
public static class AgentSurface
{
    /// <summary>The word that opens the read half.</summary>
    public const string ReadVerb = "read";

    /// <summary>The word that opens the do half.</summary>
    public const string DoVerb = "do";

    /// <summary>
    /// Every verb there is.
    /// </summary>
    /// <remarks>
    /// Deliberately short. The constitution's full list — context, doctor, logs, ports, verify and the
    /// rest — is DD25 to DD31, and each arrives with its own argument about what it answers. What DD24
    /// owns is the split itself, so it ships one verb on each side over capability that already exists:
    /// a container list, and starting or stopping the engine.
    /// </remarks>
    public static readonly IReadOnlyList<AgentVerb> All =
    [
        new(AgentNamespace.Read, "changes", "read changes",
            "[--since t:..] what moved on this machine; [--session id] only what I made"),
        new(AgentNamespace.Read, "context", "read context",
            "the whole machine in one budgeted payload; --as brief [--out path] for a project file"),
        new(AgentNamespace.Read, "doctor", "read doctor",
            "<name> — why one container is not answering, as a verdict and a remedy"),
        new(AgentNamespace.Read, "logs", "read logs",
            "<name> [--since t:..] [--level x] [--dedup] [--budget n] [--out path]"),
        new(AgentNamespace.Read, "ports", "read ports",
            "[port] — every published port, and what holds it on Windows"),
        new(AgentNamespace.Read, "ps", "read ps",
            "every container as one line each: name, state, image, ports"),
        new(AgentNamespace.Read, "verify", "read verify",
            "<name> [--request [:port]/path] [--wait] [--timeout 30s] — proof that it answers"),
        new(AgentNamespace.Do, "engine", "do engine",
            "start | stop — bring the engine up or take it down"),
        new(AgentNamespace.Do, "reclaim", "do reclaim",
            "--session [id] [--volumes] [--confirm k:..] — remove exactly what this session made"),
    ];

    /// <summary>The verb these arguments name, or null.</summary>
    /// <param name="arguments">The whole command line, starting at <c>read</c> or <c>do</c>.</param>
    /// <returns>The verb, when the first two words name one.</returns>
    public static AgentVerb? Find(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Length < 2)
        {
            return null;
        }

        var half = arguments[0] switch
        {
            ReadVerb => AgentNamespace.Read,
            DoVerb => AgentNamespace.Do,
            _ => (AgentNamespace?)null,
        };

        return half is null
            ? null
            : All.FirstOrDefault(verb =>
                verb.Namespace == half
                && string.Equals(verb.Name, arguments[1], StringComparison.Ordinal));
    }

    /// <summary>Run whatever these arguments name.</summary>
    /// <param name="arguments">The whole command line, starting at <c>read</c> or <c>do</c>.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Length < 2)
        {
            return Refuse(
                arguments.Length == 1
                    ? $"{arguments[0]} needs a verb after it"
                    : "read or do, and a verb after it");
        }

        if (Find(arguments) is not { } verb)
        {
            // Refused and named, never defaulted: a verb this surface does not have, accepted in
            // silence, is the expensive case DD23 measures — a wrong outcome nobody notices.
            return Refuse($"no such verb: {arguments[0]} {arguments[1]}");
        }

        var rest = arguments[2..];
        return verb.Namespace switch
        {
            AgentNamespace.Read => RunRead(verb, rest),
            _ => RunDo(verb, rest),
        };
    }

    /// <summary>
    /// Run a read verb, against a handle that cannot mutate.
    /// </summary>
    /// <remarks>
    /// The <see cref="IEngineReads"/> is the point: a read verb is written against the half of the
    /// engine that has no start, no remove and no prune on it, so the mistake is a compile error rather
    /// than a review comment. The behavioural half of the same guard lives in the tests, where every
    /// verb in <see cref="All"/> is driven and every request it made has to be a GET.
    /// </remarks>
    private static int RunRead(AgentVerb verb, string[] rest)
    {
        using var api = new DockerApi();
        return Read(verb, api, rest, Console.Out);
    }

    /// <summary>Run a read verb against a given engine, which is what makes it testable.</summary>
    /// <param name="verb">The verb.</param>
    /// <param name="engine">The read-only half of the engine.</param>
    /// <param name="rest">Everything after the two words.</param>
    /// <param name="output">Where the payload goes.</param>
    /// <returns>The process exit code.</returns>
    internal static int Read(AgentVerb verb, IEngineReads engine, string[] rest, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(verb);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(output);

        return verb.Name switch
        {
            "changes" => ReadChanges(engine, rest, output),
            "context" => ReadContext(engine, rest, output),
            "doctor" => ReadDoctor(engine, rest, output),
            "logs" => ReadLogs(engine, rest, output),
            "ports" => ReadPorts(engine, rest, output, new PortOwners()),
            "ps" => ReadPs(engine, rest, output),
            "verify" => ReadVerify(engine, rest, output, new ServiceProbe()),
            _ => Refuse($"{verb} is registered and not implemented"),
        };
    }

    /// <summary>
    /// What moved (DD31), and with <c>--session</c> what this session made (DD29).
    /// </summary>
    /// <remarks>
    /// One verb, because they are one idea seen from two sides: what changed, optionally narrowed to
    /// what changed <i>that is mine</i>. <c>--since</c> makes it a delta and is the only thing on this
    /// surface that makes the <b>next</b> session cheaper — a follow-up syncs on
    /// <c>worker restarted ×3, exited 137</c> rather than re-deriving the machine from DD25's pack.
    ///
    /// <para>The history is the daemon's own. It needs no ring here and no channel to the tray, it
    /// answers whether the tray is running or not, and it satisfies the constraint the section put on
    /// the feed for free: a container the <i>user</i> stopped from the tray is in it, because the daemon
    /// does not know or care which of them asked.</para>
    ///
    /// <para><c>--session</c> without <c>--since</c> is DD29's listing unchanged, and it is state rather
    /// than history on purpose: what this session created is answered by the label the objects carry, so
    /// it is still true after the daemon's event ring has rolled past it.</para>
    /// </remarks>
    /// <param name="engine">The read-only half of the engine.</param>
    /// <param name="rest">Everything after the two words.</param>
    /// <param name="output">Where the payload goes.</param>
    /// <param name="now">When this ran, so a test's window is a fixed one.</param>
    /// <returns>The process exit code.</returns>
    internal static int ReadChanges(
        IEngineReads engine, string[] rest, TextWriter output, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(output);

        var json = false;
        var mine = false;
        string? session = null;
        DateTimeOffset? since = null;

        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--json":
                    json = true;
                    continue;
                case "--session":
                    mine = true;
                    if (i + 1 < rest.Length && !rest[i + 1].StartsWith('-'))
                    {
                        session = rest[++i];
                    }

                    continue;
                case "--since":
                    if (i + 1 >= rest.Length)
                    {
                        return Refuse("--since needs a cursor after it");
                    }

                    if (!ChangeFeed.TryParseCursor(rest[++i], out var at, out var why))
                    {
                        return Refuse(why!);
                    }

                    since = at;
                    continue;
                default:
                    return Refuse($"unexpected argument {rest[i]}: read changes takes --since, "
                        + "--session and --json");
            }
        }

        session ??= SessionLabel.Resolve();
        var until = now ?? DateTimeOffset.UtcNow;

        try
        {
            if (!engine.PingAsync().GetAwaiter().GetResult())
            {
                return RefuseWith(CannotConnect(), json, output);
            }

            // Asked only about this session, with no window: the label answers it from state, which
            // outlives the daemon's ring and is the form DD29's undo reads.
            if (mine && since is null)
            {
                var plan = Reclaim.Plan(
                    session,
                    engine.ContainersAsync().GetAwaiter().GetResult(),
                    engine.VolumesAsync().GetAwaiter().GetResult(),
                    includeVolumes: true);

                output.Write(json ? Reclaim.RenderJson(plan) : Reclaim.RenderChanges(plan));
                return Ok;
            }

            var events = engine
                .EventsAsync(since ?? until - ChangeFeed.DefaultWindow, until)
                .GetAwaiter().GetResult();

            if (mine)
            {
                // The daemon attaches an object's labels to its events, so narrowing costs no extra
                // call. An event carrying no labels at all is somebody else's by definition.
                events = [.. events.Where(e =>
                    e.Actor.Attributes.TryGetValue(SessionLabel.Key, out var stamped)
                    && string.Equals(stamped, session, StringComparison.Ordinal))];
            }

            var delta = ChangeFeed.Collapse(events, until);
            output.Write(json ? ChangeFeed.RenderJson(delta) : ChangeFeed.Render(delta));

            // The delta is complete or it says it is not, and the exit code carries that so a script
            // does not have to read the text for the one thing it must not miss.
            return delta.TooOld ? Failed : Ok;
        }
        catch (DockerApiException)
        {
            return RefuseWith(CannotConnect(), json, output);
        }
    }

    /// <summary>
    /// The whole machine, once, under a ceiling (DD25).
    /// </summary>
    /// <remarks>
    /// One round trip for the caller. Several to the daemon underneath, and that asymmetry is the whole
    /// design: a local pipe call costs no tokens and no approval, while every call the agent makes
    /// costs both. Inspects are still rationed to the containers that are not running, because those
    /// are the only ones an inspect tells you anything the list did not.
    /// </remarks>
    private static int ReadContext(IEngineReads engine, string[] rest, TextWriter output)
    {
        var json = false;
        var brief = false;
        var force = false;
        string? outPath = null;

        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--json":
                    json = true;
                    continue;
                case "--force":
                    force = true;
                    continue;
                case "--as" or "--out":
                    if (i + 1 >= rest.Length)
                    {
                        return Refuse($"{rest[i]} needs a value after it");
                    }

                    if (string.Equals(rest[i], "--out", StringComparison.Ordinal))
                    {
                        outPath = rest[++i];
                        continue;
                    }

                    var shape = rest[++i];
                    if (!string.Equals(shape, "brief", StringComparison.Ordinal))
                    {
                        return Refuse($"--as takes brief, not {shape}");
                    }

                    brief = true;
                    continue;
                default:
                    return Refuse($"unexpected argument {rest[i]}: read context takes --json, "
                        + "--as brief, --out and --force");
            }
        }

        if (outPath is not null && !brief)
        {
            // Named rather than assumed: --out on its own would be a redirect somebody could write
            // with `>`, and quietly accepting it would teach an argument that does nothing.
            return Refuse("--out writes the brief: pass --as brief with it, or redirect with >");
        }

        if (json && brief)
        {
            return Refuse("--as brief and --json are two formats: pick one");
        }

        try
        {
            if (!engine.PingAsync().GetAwaiter().GetResult())
            {
                // State stated rather than probed: the engine line says it is down, so the caller does
                // not spend a call finding out.
                //
                // A brief is still written. Most of what it carries is how this surface is reached,
                // which is true whether the engine is up or not, and somebody setting a project up
                // before starting the engine is the ordinary case rather than the odd one. The exit
                // code still says the machine section came from a stopped engine.
                if (brief)
                {
                    // A refusal about the arguments keeps its own exit code. Reporting "engine not
                    // ready" for a file that already exists would send a script down the wrong branch
                    // over a problem that has nothing to do with the engine.
                    var wrote = WriteBrief(Down("stopped"), outPath, force, output);
                    return wrote == Ok ? NotReady : wrote;
                }

                output.Write(Show(Down("stopped"), json));
                return NotReady;
            }

            var version = engine.VersionAsync().GetAwaiter().GetResult();
            var containers = engine.ContainersAsync().GetAwaiter().GetResult();

            var diagnoses = new Dictionary<string, ContainerInspect>(StringComparer.Ordinal);
            foreach (var container in containers.Where(c =>
                !string.Equals(c.State, "running", StringComparison.Ordinal)))
            {
                try
                {
                    diagnoses[container.Id] = engine
                        .InspectAsync(container.Id).GetAwaiter().GetResult();
                }
                catch (DockerApiException)
                {
                    // A container that went away between the list and the inspect is not a failure of
                    // the pack: the row still states what the list knew.
                }
            }

            var client = new Core.Preflight.Windows.WindowsMachineFacts().DockerClient;
            var facts = new ContextFacts(
                EngineState: "running",
                Distribution: EnginePaths.DistributionName,
                ApiVersion: version.ApiVersion,
                ContextName: client.FromEnvironment ? "DOCKER_HOST" : client.ContextName,
                ContextReachesEngine:
                    Core.Preflight.Windows.DockerContextProbe.ReachesThisEngine(client.Host),
                Containers: containers,
                Diagnoses: diagnoses,
                Images: engine.ImagesAsync().GetAwaiter().GetResult(),
                VolumeCount: engine.VolumesAsync().GetAwaiter().GetResult().Count);

            if (!brief)
            {
                output.Write(Show(facts, json));
                return Ok;
            }

            return WriteBrief(facts, outPath, force, output);
        }
        catch (DockerApiException exception)
        {
            var down = Down($"unreachable: {exception.Message}");
            if (brief)
            {
                var wrote = WriteBrief(down, outPath, force, output);
                return wrote == Ok ? NotReady : wrote;
            }

            output.Write(Show(down, json));
            return NotReady;
        }
    }

    /// <summary>
    /// The brief, to stdout or to exactly the path it was given (DD32).
    /// </summary>
    /// <remarks>
    /// The same rule <c>read logs --out</c> established: a read does not mutate <b>the engine</b>, and a
    /// file at a path the caller named in the same breath is not a mutation of anything it did not ask
    /// for. What is added here is the refusal to clobber — a brief is generated and the file it would
    /// land on might not be, and a tool that ate something somebody wrote to save them a flag has made
    /// the wrong trade. <c>--force</c> is the flag.
    /// </remarks>
    private static int WriteBrief(ContextFacts facts, string? outPath, bool force, TextWriter output)
    {
        var brief = AgentBrief.Render(facts, [.. All.Select(verb => verb.ToString())]);

        if (outPath is null)
        {
            output.Write(brief);
            return Ok;
        }

        try
        {
            var full = Path.GetFullPath(outPath);
            if (File.Exists(full) && !force)
            {
                return Refuse(
                    $"{full} already exists. Pass --force to replace it, or --out somewhere else.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, brief);
            output.WriteLine(
                $"wrote {full}  {brief.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n').Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} line(s)");
            output.WriteLine("Re-run it when the machine changes: it is generated, not maintained.");
            return Ok;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            return Refuse($"could not write {outPath}: {exception.Message}");
        }
    }

    /// <summary>One format or the other, and the line format is the default because it is cheaper.</summary>
    private static string Show(ContextFacts facts, bool json) =>
        json ? ContextPack.RenderJson(facts) : ContextPack.Render(facts);

    /// <summary>The pack for a machine whose engine is not answering.</summary>
    private static ContextFacts Down(string state) => new(
        EngineState: state,
        Distribution: EnginePaths.DistributionName,
        ApiVersion: DockerApi.ApiVersion,
        ContextName: null,
        ContextReachesEngine: false,
        Containers: [],
        Diagnoses: new Dictionary<string, ContainerInspect>(StringComparer.Ordinal),
        Images: [],
        VolumeCount: 0);

    /// <summary>
    /// Why one container is not answering (DD26).
    /// </summary>
    /// <remarks>
    /// The join a caller used to do in its own head, closed rather than moved: one call, and what comes
    /// back is a verdict and a remedy per row rather than the forty fields the five commands would have
    /// cost. The rows are <see cref="Core.Preflight.PreflightCheck"/>, so the vocabulary is the one the
    /// preflight already established and the renderer is the one it already has.
    /// </remarks>
    private static int ReadDoctor(IEngineReads engine, string[] rest, TextWriter output)
    {
        var json = false;
        string? target = null;
        foreach (var argument in rest)
        {
            if (string.Equals(argument, "--json", StringComparison.Ordinal))
            {
                json = true;
            }
            else if (argument.StartsWith('-'))
            {
                return Refuse($"unexpected argument {argument}: read doctor takes a name and --json");
            }
            else if (target is null)
            {
                target = argument;
            }
            else
            {
                return Refuse($"unexpected argument {argument}: read doctor takes one name");
            }
        }

        if (!Core.Agent.Address.TryParse(target, out var address, out var refusal))
        {
            return Refuse(refusal);
        }

        try
        {
            if (!engine.PingAsync().GetAwaiter().GetResult())
            {
                output.WriteLine("engine  stopped  nothing is answering the pipe");
                return NotReady;
            }

            var containers = engine.ContainersAsync().GetAwaiter().GetResult();
            var summary = Match(containers, address);

            ContainerInspect? inspect = null;
            if (summary is not null)
            {
                try
                {
                    inspect = engine.InspectAsync(summary.Id).GetAwaiter().GetResult();
                }
                catch (DockerApiException)
                {
                    // It went away between the list and the inspect. The rows still say what the list
                    // knew, which is more useful than a failure.
                }
            }

            var report = ContainerDoctor.Diagnose(new DoctorFacts(
                Address: address,
                Summary: summary,
                Inspect: inspect,
                ListeningHostPorts: new HostPorts().Listening(),
                StandardError: summary is null ? [] : StandardErrorTail(engine, summary.Id),
                Now: DateTimeOffset.UtcNow));

            output.Write(json
                ? System.Text.Json.JsonSerializer.Serialize(
                    report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
                    + Environment.NewLine
                : Core.Preflight.ReportText.Render(
                    report,
                    heading: $"dockerdesk read doctor {address}",
                    summary: report.CanHostEngine
                        ? "Nothing here is wrong with this container."
                        : $"{report.Blockers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} finding(s). The remedy on each row is the action."));

            // The exit code carries the conclusion, so a script does not have to read the text.
            return report.CanHostEngine ? Ok : Failed;
        }
        catch (DockerApiException exception)
        {
            output.WriteLine($"engine  unreachable  {exception.Message}");
            return NotReady;
        }
    }

    /// <summary>
    /// A container's log, with a cursor, a level, a dedup and a ceiling (DD27).
    /// </summary>
    /// <remarks>
    /// <c>--out</c> is the argument that matters most and is the least obvious. Writing the log to a
    /// file turns an unbounded read into a Grep: against a stream the caller pays for every line, and
    /// against a file it pays for the lines that match. A ten-megabyte log becomes affordable rather
    /// than merely truncated.
    ///
    /// <para>It writes, and it is still a read. <c>read</c> promises not to mutate <b>the engine</b>,
    /// and a file at a path the caller named in the same breath is not a mutation of anything they did
    /// not ask for. The two guards say so: every request to the daemon is a GET, and a read verb touches
    /// no path except the one it was given.</para>
    /// </remarks>
    private static int ReadLogs(IEngineReads engine, string[] rest, TextWriter output)
    {
        string? target = null;
        string? outPath = null;
        var query = new LogQuery(BudgetTokens: LogDigest.DefaultBudgetTokens);

        for (var i = 0; i < rest.Length; i++)
        {
            var argument = rest[i];
            switch (argument)
            {
                case "--dedup":
                    query = query with { Dedup = true };
                    continue;
                case "--since" or "--level" or "--budget" or "--out":
                    if (i + 1 >= rest.Length)
                    {
                        return Refuse($"{argument} needs a value after it");
                    }

                    var value = rest[++i];
                    switch (argument)
                    {
                        case "--since":
                            if (!LogDigest.TryParseCursor(value, out var since, out var why))
                            {
                                return Refuse(why!);
                            }

                            query = query with { Since = since };
                            continue;
                        case "--level":
                            if (!LogDigest.TryParseLevel(value, out var level))
                            {
                                return Refuse(
                                    $"{value} is not a level: trace, debug, info, warn, error or fatal");
                            }

                            query = query with { MinimumLevel = level };
                            continue;
                        case "--budget":
                            if (!int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var budget)
                                || budget <= 0)
                            {
                                return Refuse($"{value} is not a token budget");
                            }

                            query = query with { BudgetTokens = budget };
                            continue;
                        default:
                            outPath = value;
                            continue;
                    }

                default:
                    if (argument.StartsWith('-'))
                    {
                        return Refuse($"unexpected argument {argument}");
                    }

                    if (target is not null)
                    {
                        return Refuse($"unexpected argument {argument}: read logs takes one name");
                    }

                    target = argument;
                    continue;
            }
        }

        if (!Core.Agent.Address.TryParse(target, out var address, out var refusal))
        {
            return Refuse(refusal);
        }

        try
        {
            if (!engine.PingAsync().GetAwaiter().GetResult())
            {
                output.WriteLine("engine  stopped  nothing is answering the pipe");
                return NotReady;
            }

            var containers = engine.ContainersAsync().GetAwaiter().GetResult();
            if (Match(containers, address) is not { } container)
            {
                return Refuse($"no container named {address} on this engine");
            }

            List<LogChunk> chunks = [];
            using (var stream = engine.LogsAsync(
                container.Id, tail: 2000, follow: false, timestamps: true, since: query.Since)
                .GetAwaiter().GetResult())
            {
                var frames = new LogFrames(stream, framed: true);
                while (frames.ReadAsync().GetAwaiter().GetResult() is { } chunk)
                {
                    chunks.Add(chunk);
                }
            }

            var lines = LogDigest.Split(chunks);

            if (outPath is null)
            {
                output.Write(LogDigest.Render(lines, query).Text);
                return Ok;
            }

            // To the file goes everything the filters kept, with no ceiling: the ceiling exists because
            // a payload is read by something paying per token, and a file is not.
            var whole = LogDigest.Render(lines, query with { BudgetTokens = null });
            var full = Path.GetFullPath(outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, whole.Text);

            output.WriteLine(
                $"wrote {full}  {whole.Lines.ToString(System.Globalization.CultureInfo.InvariantCulture)} line(s)"
                + $"  {new FileInfo(full).Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} bytes");
            output.WriteLine("Grep it: the matching lines cost tokens, the rest does not.");
            if (whole.Cursor is not null)
            {
                output.WriteLine("cursor  " + whole.Cursor);
            }

            return Ok;
        }
        catch (DockerApiException exception)
        {
            output.WriteLine($"engine  unreachable  {exception.Message}");
            return NotReady;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            return Refuse($"could not write {outPath}: {exception.Message}");
        }
    }

    /// <summary>
    /// Every published port beside what holds it on Windows (DD28).
    /// </summary>
    /// <remarks>
    /// The join the Engine API cannot make. The daemon knows what was published and only Windows knows
    /// which process owns the socket, so <c>port is already allocated</c> — the one refusal an agent
    /// cannot act on — becomes one it can. Given a port, this answers about that port whether Docker
    /// published it or not, which is exactly the case the daemon has nothing to say about.
    /// </remarks>
    internal static int ReadPorts(
        IEngineReads engine, string[] rest, TextWriter output, IPortOwners owners)
    {
        ArgumentNullException.ThrowIfNull(owners);

        var json = false;
        int? single = null;
        foreach (var argument in rest)
        {
            if (string.Equals(argument, "--json", StringComparison.Ordinal))
            {
                json = true;
            }
            else if (int.TryParse(argument, System.Globalization.CultureInfo.InvariantCulture, out var port)
                && port is > 0 and <= 65535)
            {
                single = port;
            }
            else
            {
                return Refuse($"unexpected argument {argument}: read ports takes a port and --json");
            }
        }

        // Asked about one port, the answer does not need the engine at all: whatever holds it holds it,
        // and the interesting case is precisely the one where Docker is not what holds it.
        if (single is { } only)
        {
            var holder = owners.Holding(only);
            if (holder is null)
            {
                output.Write(json
                    ? "{\"port\":" + only.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ",\"heldBy\":null}" + Environment.NewLine
                    : $"port {only} is free{Environment.NewLine}");
                return Ok;
            }

            var problem = AgentProblem.PortAllocated(only, holder);
            output.Write(json ? problem.ToJson() : problem.ToText());
            return Ok;
        }

        try
        {
            if (!engine.PingAsync().GetAwaiter().GetResult())
            {
                return RefuseWith(CannotConnect(), json, output);
            }

            var containers = engine.ContainersAsync().GetAwaiter().GetResult();
            var rows = new List<string>();
            foreach (var container in containers.OrderBy(c => c.DisplayName, StringComparer.Ordinal))
            {
                foreach (var published in container.PublishedPorts)
                {
                    // "8080->80/tcp" — the host port is what Windows knows about.
                    var arrow = published.IndexOf("->", StringComparison.Ordinal);
                    if (arrow <= 0
                        || !int.TryParse(published[..arrow], System.Globalization.CultureInfo.InvariantCulture, out var host))
                    {
                        continue;
                    }

                    var holder = owners.Holding(host);
                    rows.Add(
                        $"{container.DisplayName}  {published}  "
                        + (holder is null
                            ? "nothing listening"
                            : $"pid {holder.Pid.ToString(System.Globalization.CultureInfo.InvariantCulture)} {holder.Image}"));
                }
            }

            output.Write(rows.Count == 0
                ? "(no published ports)" + Environment.NewLine
                : string.Join(Environment.NewLine, rows) + Environment.NewLine);
            return Ok;
        }
        catch (DockerApiException)
        {
            return RefuseWith(CannotConnect(), json, output);
        }
    }

    /// <summary>
    /// Which of the three unrelated causes of "cannot connect" this machine has.
    /// </summary>
    /// <remarks>
    /// DD16 already reads what owns the docker command and DD20 already reads where the CLI points, and
    /// both facts were being thrown away at the moment somebody needed them.
    /// </remarks>
    private static AgentProblem CannotConnect()
    {
        var facts = new Core.Preflight.Windows.WindowsMachineFacts();
        return AgentProblem.CannotConnect(
            facts.RivalEngines, facts.DockerClient, DockerApi.DefaultPipeName);
    }

    /// <summary>Print a refusal in whichever form was asked for, and return its exit code.</summary>
    private static int RefuseWith(AgentProblem problem, bool json, TextWriter output)
    {
        output.Write(json ? problem.ToJson() : problem.ToText());
        return NotReady;
    }

    /// <summary>The container this address names, by name and never by id.</summary>
    private static ContainerSummary? Match(
        IReadOnlyList<ContainerSummary> containers, Core.Agent.Address address)
    {
        if (address.Kind == Core.Agent.AddressKind.Service)
        {
            return containers.FirstOrDefault(c =>
                c.Labels is not null
                && c.Labels.TryGetValue(ContextPack.ProjectLabel, out var project)
                && c.Labels.TryGetValue(ContextPack.ServiceLabel, out var service)
                && string.Equals(project, address.Project, StringComparison.Ordinal)
                && string.Equals(service, address.Name, StringComparison.Ordinal));
        }

        return containers.FirstOrDefault(c =>
                   string.Equals(c.DisplayName, address.Name, StringComparison.Ordinal))
               ?? containers.FirstOrDefault(c =>
                   c.Id.StartsWith(address.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>How many stderr lines a diagnosis carries.</summary>
    /// <remarks>
    /// Five. A restart loop writes the same trace every time, so the tenth copy costs tokens and says
    /// nothing; making a log read cheap in general — dedup, a cursor, a level — is DD27.
    /// </remarks>
    private const int StandardErrorLines = 5;

    /// <summary>The last few lines the container wrote to stderr, newest last.</summary>
    private static IReadOnlyList<string> StandardErrorTail(IEngineReads engine, string id)
    {
        try
        {
            using var stream = engine
                .LogsAsync(id, tail: 200, follow: false).GetAwaiter().GetResult();
            var frames = new LogFrames(stream, framed: true);
            var lines = new List<string>();
            while (frames.ReadAsync().GetAwaiter().GetResult() is { } chunk)
            {
                if (chunk.Stream != LogStream.StdErr)
                {
                    continue;
                }

                foreach (var line in chunk.Text.Split('\n'))
                {
                    var trimmed = line.TrimEnd('\r');
                    if (trimmed.Length > 0)
                    {
                        lines.Add(trimmed);
                    }
                }
            }

            return lines.Count <= StandardErrorLines
                ? lines
                : lines[^StandardErrorLines..];
        }
        catch (Exception exception) when (exception is DockerApiException or IOException
            or InvalidOperationException)
        {
            // A log this tool could not read is a row that is absent, not a diagnosis that failed.
            return [];
        }
    }

    /// <summary>
    /// Every container, one line each.
    /// </summary>
    /// <remarks>
    /// A terse line format rather than JSON, because entity JSON spends most of its bytes on
    /// punctuation, repeated keys and authoring metadata nothing reads — measured in DD23, where one
    /// container list came to 1906 estimated tokens for six containers. Deterministic order, so it
    /// caches and diffs.
    /// </remarks>
    private static int ReadPs(IEngineReads engine, string[] rest, TextWriter output)
    {
        if (rest.Length > 0)
        {
            return Refuse($"unexpected argument {rest[0]}: read ps takes none");
        }

        IReadOnlyList<ContainerSummary> containers;
        try
        {
            if (!engine.PingAsync().GetAwaiter().GetResult())
            {
                // Self-describing state, so the agent never probes for a capability: the answer says
                // the engine is down rather than returning an empty list that reads as "no containers".
                output.WriteLine("engine  stopped  nothing is answering the pipe");
                return NotReady;
            }

            containers = engine.ContainersAsync().GetAwaiter().GetResult();
        }
        catch (DockerApiException exception)
        {
            output.WriteLine($"engine  unreachable  {exception.Message}");
            return NotReady;
        }

        if (containers.Count == 0)
        {
            output.WriteLine("(no containers)");
            return Ok;
        }

        // Sorted by name, because deterministic order is what makes a payload cacheable and diffable,
        // and the daemon's own order is creation order, which moves.
        var text = new StringBuilder();
        foreach (var container in containers.OrderBy(c => c.DisplayName, StringComparer.Ordinal))
        {
            var ports = container.PublishedPorts.Count == 0
                ? "-"
                : string.Join(",", container.PublishedPorts);
            text.Append(container.DisplayName).Append("  ")
                .Append(container.State).Append("  ")
                .Append(container.Image).Append("  ")
                .Append(ports).AppendLine();
        }

        output.Write(text.ToString());
        return Ok;
    }

    /// <summary>How long the wait sleeps between attempts.</summary>
    /// <remarks>
    /// Half a second. A readiness wait is the thing a caller currently writes as a sleep loop, and the
    /// gap only has to be short enough that the answer is not stale and long enough that a starting
    /// service is not asked forty times a second.
    /// </remarks>
    private static readonly TimeSpan PollGap = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Proof that a service answers, and the readiness primitive that replaces a sleep loop (DD30).
    /// </summary>
    /// <remarks>
    /// <c>--wait</c> is the second half and costs one call rather than a polling loop the caller writes:
    /// it returns the moment the condition holds, and on a timeout it prints the same report saying
    /// which rows did not pass. A sleep loop written by the caller has neither property — it pays for
    /// every poll and it ends knowing only that time ran out.
    /// </remarks>
    /// <param name="engine">The read-only half of the engine.</param>
    /// <param name="rest">Everything after the two words.</param>
    /// <param name="output">Where the report goes.</param>
    /// <param name="probe">What reaches the port from Windows.</param>
    /// <param name="gap">How long to sleep between attempts, so a test does not.</param>
    /// <returns>The process exit code.</returns>
    internal static int ReadVerify(
        IEngineReads engine, string[] rest, TextWriter output, IServiceProbe probe, TimeSpan? gap = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(probe);

        var json = false;
        var wait = false;
        string? target = null;
        string? request = null;
        var timeout = TimeSpan.FromSeconds(30);

        for (var i = 0; i < rest.Length; i++)
        {
            var argument = rest[i];
            switch (argument)
            {
                case "--json":
                    json = true;
                    continue;
                case "--wait":
                    wait = true;
                    continue;
                case "--request" or "--timeout":
                    if (i + 1 >= rest.Length)
                    {
                        return Refuse($"{argument} needs a value after it");
                    }

                    var value = rest[++i];
                    if (string.Equals(argument, "--request", StringComparison.Ordinal))
                    {
                        request = value;
                        continue;
                    }

                    if (!TryParseSeconds(value, out timeout))
                    {
                        return Refuse($"{value} is not a timeout: seconds, as 30 or 30s");
                    }

                    continue;
                default:
                    if (argument.StartsWith('-'))
                    {
                        return Refuse($"unexpected argument {argument}: read verify takes a name, "
                            + "--request, --wait, --timeout and --json");
                    }

                    if (target is not null)
                    {
                        return Refuse($"unexpected argument {argument}: read verify takes one name");
                    }

                    target = argument;
                    continue;
            }
        }

        if (!Core.Agent.Address.TryParse(target, out var address, out var refusal))
        {
            return Refuse(refusal);
        }

        try
        {
            if (!engine.PingAsync().GetAwaiter().GetResult())
            {
                output.WriteLine("engine  stopped  nothing is answering the pipe");
                return NotReady;
            }

            var deadline = DateTimeOffset.UtcNow + timeout;
            Core.Preflight.PreflightReport report;
            while (true)
            {
                var containers = engine.ContainersAsync().GetAwaiter().GetResult();
                var summary = Match(containers, address);
                ContainerInspect? inspect = null;
                if (summary is not null)
                {
                    try
                    {
                        inspect = engine.InspectAsync(summary.Id).GetAwaiter().GetResult();
                    }
                    catch (DockerApiException)
                    {
                        // Gone between the list and the inspect. The rows still say what the list knew.
                    }
                }

                var published = ServiceVerify.Published(inspect);
                var running = string.Equals(inspect?.State.Status, "running", StringComparison.Ordinal)
                    || string.Equals(summary?.State, "running", StringComparison.Ordinal);

                // Nothing is probed for a container that is not running: a port it published would be
                // answered by whatever took it, and reporting that as this service would be the
                // confidently wrong answer this verb exists to prevent.
                var answers = running
                    ? published
                        .Select(p => probe.Connect(p.Host, p.Container, ServiceVerify.Attempt))
                        .ToList()
                    : [];

                RequestAnswer? answered = null;
                if (request is not null && running)
                {
                    if (!TryTargetRequest(request, published, out var port, out var path, out var why))
                    {
                        return Refuse(why);
                    }

                    answered = probe.Get(port, path, ServiceVerify.Attempt);
                }

                report = ServiceVerify.Verify(
                    new VerifyFacts(address, summary, inspect, answers, answered));

                if (!wait || report.CanHostEngine || DateTimeOffset.UtcNow >= deadline)
                {
                    break;
                }

                Thread.Sleep(gap ?? PollGap);
            }

            output.Write(json
                ? System.Text.Json.JsonSerializer.Serialize(
                    report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
                    + Environment.NewLine
                : Core.Preflight.ReportText.Render(
                    report,
                    heading: $"dockerdesk read verify {address}",
                    summary: report.CanHostEngine
                        ? "It answers."
                        : (wait ? $"Still not answering after {Seconds(timeout)}: " : "Not answering: ")
                            + string.Join(", ", report.Blockers.Select(b => b.Title))
                            + ". The remedy on each row is the action."));

            return report.CanHostEngine ? Ok : Failed;
        }
        catch (DockerApiException exception)
        {
            output.WriteLine($"engine  unreachable  {exception.Message}");
            return NotReady;
        }
    }

    /// <summary>Which port a <c>--request</c> goes to, and to what path.</summary>
    /// <remarks>
    /// A container publishing one port needs no <c>:port</c> and a container publishing several cannot
    /// have one guessed for it: picking the lowest would answer confidently about the wrong service,
    /// which is the failure mode this whole verb exists to remove.
    /// </remarks>
    private static bool TryTargetRequest(
        string request,
        IReadOnlyList<(int Host, string Container)> published,
        out int port,
        out string path,
        out string why)
    {
        port = 0;
        path = request;
        why = "";

        if (request.StartsWith(':'))
        {
            var slash = request.IndexOf('/', StringComparison.Ordinal);
            if (slash < 2
                || !int.TryParse(request[1..slash], System.Globalization.CultureInfo.InvariantCulture, out port))
            {
                why = $"{request} is not a request target: :8080/healthz, or /healthz where one port "
                    + "is published";
                return false;
            }

            path = request[slash..];
            return true;
        }

        if (!request.StartsWith('/'))
        {
            why = $"{request} is not a path: it begins with a slash";
            return false;
        }

        switch (published.Count)
        {
            case 1:
                port = published[0].Host;
                return true;
            case 0:
                why = "this container publishes no port, so there is nothing to request";
                return false;
            default:
                why = "this container publishes "
                    + string.Join(
                        ", ",
                        published.Select(p => p.Host.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    + $": name one, as --request :{published[0].Host.ToString(System.Globalization.CultureInfo.InvariantCulture)}{request}";
                return false;
        }
    }

    /// <summary>Seconds, written as a number or with an s after it.</summary>
    private static bool TryParseSeconds(string value, out TimeSpan span)
    {
        span = default;
        var digits = value.EndsWith('s') ? value[..^1] : value;
        if (!int.TryParse(digits, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            || seconds < 0)
        {
            return false;
        }

        span = TimeSpan.FromSeconds(seconds);
        return true;
    }

    private static string Seconds(TimeSpan span) =>
        ((int)span.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture) + "s";

    private static int RunDo(AgentVerb verb, string[] rest) => verb.Name switch
    {
        "engine" => DoEngine(rest),
        "reclaim" => DoReclaimHere(rest),
        _ => Refuse($"{verb} is registered and not implemented"),
    };

    /// <summary>The reclaim against this machine's engine.</summary>
    private static int DoReclaimHere(string[] rest)
    {
        using var api = new DockerApi();
        return DoReclaim(api, rest, Console.Out);
    }

    /// <summary>
    /// Remove exactly what one session created, once somebody has seen the list (DD29).
    /// </summary>
    /// <remarks>
    /// Two calls by design. The first prints the plan and a token computed over it; the second carries
    /// that token back, and matches only if the list is still the one that was printed. A container that
    /// arrived in between changes the token, so the second call refuses and names what would go now
    /// rather than quietly taking something nobody approved.
    ///
    /// <para>The removal is forced, because the plan said which of them were running and the caller
    /// confirmed that list. A confirm that then failed on the one container the caller could see was
    /// running would have moved the work back to them for no safety at all — the safety is the list.</para>
    /// </remarks>
    /// <param name="engine">The engine, on a handle that can remove and cannot start.</param>
    /// <param name="rest">Everything after the two words.</param>
    /// <param name="output">Where the plan goes.</param>
    /// <returns>The process exit code.</returns>
    internal static int DoReclaim(IEngineRemovals engine, string[] rest, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(output);

        var json = false;
        var volumes = false;
        string? session = null;
        string? confirm = null;

        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--json":
                    json = true;
                    continue;
                case "--volumes":
                    volumes = true;
                    continue;
                case "--session":
                    // The value is optional: bare, it means the session this process is already in.
                    if (i + 1 < rest.Length && !rest[i + 1].StartsWith('-'))
                    {
                        session = rest[++i];
                    }

                    continue;
                case "--confirm":
                    if (i + 1 >= rest.Length)
                    {
                        return Refuse("--confirm needs the token the plan printed");
                    }

                    confirm = rest[++i];
                    continue;
                default:
                    return Refuse(
                        $"unexpected argument {rest[i]}: do reclaim takes --session, --volumes, "
                        + "--confirm and --json");
            }
        }

        if (confirm is not null && !confirm.StartsWith(Reclaim.TokenPrefix, StringComparison.Ordinal))
        {
            // Named rather than compared, because the two other prefixes on this surface are a context
            // cursor and a log cursor, and a caller that pasted one of those is one word from correct.
            return Refuse(
                $"{confirm} is not a confirm token: they start with {Reclaim.TokenPrefix} and are "
                + "printed by `dockerdesk do reclaim --session`");
        }

        session ??= SessionLabel.Resolve();

        try
        {
            if (!engine.PingAsync().GetAwaiter().GetResult())
            {
                return RefuseWith(CannotConnect(), json, output);
            }

            var plan = Reclaim.Plan(
                session,
                engine.ContainersAsync().GetAwaiter().GetResult(),
                engine.VolumesAsync().GetAwaiter().GetResult(),
                volumes);

            if (confirm is null)
            {
                output.Write(json ? Reclaim.RenderJson(plan) : Reclaim.Render(plan));
                return Ok;
            }

            if (!Reclaim.Confirms(plan, confirm))
            {
                var problem = Reclaim.Stale(plan, confirm);
                output.Write(json ? problem.ToJson() : problem.ToText());
                if (!json && plan.Removing.Count > 0)
                {
                    output.Write(Reclaim.Render(plan));
                }

                return Failed;
            }

            // Containers first: a volume this session's own container still mounts is refused by the
            // daemon, and the container is on its way out anyway.
            var failures = 0;
            foreach (var item in plan.Removing)
            {
                try
                {
                    if (string.Equals(item.Kind, Reclaim.Container, StringComparison.Ordinal))
                    {
                        engine.RemoveContainerAsync(item.Name, force: true).GetAwaiter().GetResult();
                    }
                    else
                    {
                        engine.RemoveVolumeAsync(item.Name).GetAwaiter().GetResult();
                    }

                    output.WriteLine($"removed  {item.Kind.PadRight(10)}{item.Name}");
                }
                catch (DockerApiException exception)
                {
                    // Reported and carried on: one volume the daemon would not release is not a reason
                    // to leave the other nine behind, and a partial reclaim that says which part failed
                    // is actionable.
                    failures++;
                    output.WriteLine($"FAILED   {item.Kind.PadRight(10)}{item.Name}  {exception.Message}");
                }
            }

            return failures == 0 ? Ok : Failed;
        }
        catch (DockerApiException)
        {
            return RefuseWith(CannotConnect(), json, output);
        }
    }

    /// <summary>Start or stop the engine, through the same code the tray and the flags use.</summary>
    private static int DoEngine(string[] rest)
    {
        if (rest.Length != 1)
        {
            return Refuse("do engine takes start or stop");
        }

        switch (rest[0])
        {
            case "start":
            {
                // The same detached launch the tray's menu item makes, for the same reason: the relay
                // has to outlive the command that started it.
                var failure = new EngineHolder(EngineHolder.ThisProcess(), new DetachedLauncher())
                    .Start();
                if (failure is not null)
                {
                    Console.Error.WriteLine($"{CommandLine.ExecutableName}: {failure}");
                    return Failed;
                }

                Console.Out.WriteLine("engine  starting  serving \\\\.\\pipe\\" + DockerApi.DefaultPipeName);
                return Ok;
            }

            case "stop":
            {
                var lifecycle = new EngineLifecycle(new Wsl(), new WslDaemonProcess(), new WslSocatBackend());
                try
                {
                    var status = lifecycle.StopAsync().GetAwaiter().GetResult();
                    Console.Out.WriteLine($"engine  {status.State.ToString().ToLowerInvariant()}  {status.Detail}");
                    return Ok;
                }
                finally
                {
                    lifecycle.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }

            default:
                return Refuse($"do engine takes start or stop, not {rest[0]}");
        }
    }

    /// <summary>Every verb, for the console's own help.</summary>
    public static string HelpText
    {
        get
        {
            var text = new StringBuilder();
            text.AppendLine("The agent surface. Reads mutate nothing, which is what lets one");
            text.AppendLine("allowlist line cover all of them:");
            text.AppendLine();
            text.AppendLine("  Bash(dockerdesk read:*)");
            text.AppendLine();
            foreach (var half in new[] { AgentNamespace.Read, AgentNamespace.Do })
            {
                foreach (var verb in All.Where(v => v.Namespace == half))
                {
                    text.Append("  ").Append(verb.ToString().PadRight(18))
                        .AppendLine(verb.Summary);
                }
            }

            text.AppendLine();
            text.AppendLine("Addresses are names: a container by its name, a compose service as");
            text.AppendLine("svc:<project>/<service>. An id changes when a container is recreated.");
            return text.ToString();
        }
    }

    private const int Ok = 0;
    private const int Failed = 1;
    private const int Usage = 2;

    /// <summary>The engine is not answering, which is not the caller's mistake.</summary>
    private const int NotReady = 3;

    private static int Refuse(string problem)
    {
        Console.Error.WriteLine($"{CommandLine.ExecutableName}: {problem}");
        Console.Error.Write(HelpText);
        return Usage;
    }
}
