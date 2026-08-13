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
        new(AgentNamespace.Read, "context", "read context",
            "the whole machine in one budgeted payload: engine, containers, disk, cursor"),
        new(AgentNamespace.Read, "doctor", "read doctor",
            "<name> — why one container is not answering, as a verdict and a remedy"),
        new(AgentNamespace.Read, "logs", "read logs",
            "<name> [--since t:..] [--level x] [--dedup] [--budget n] [--out path]"),
        new(AgentNamespace.Read, "ports", "read ports",
            "[port] — every published port, and what holds it on Windows"),
        new(AgentNamespace.Read, "ps", "read ps",
            "every container as one line each: name, state, image, ports"),
        new(AgentNamespace.Do, "engine", "do engine",
            "start | stop — bring the engine up or take it down"),
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
            "context" => ReadContext(engine, rest, output),
            "doctor" => ReadDoctor(engine, rest, output),
            "logs" => ReadLogs(engine, rest, output),
            "ports" => ReadPorts(engine, rest, output, new PortOwners()),
            "ps" => ReadPs(engine, rest, output),
            _ => Refuse($"{verb} is registered and not implemented"),
        };
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
        foreach (var argument in rest)
        {
            if (string.Equals(argument, "--json", StringComparison.Ordinal))
            {
                json = true;
                continue;
            }

            return Refuse($"unexpected argument {argument}: read context takes --json or nothing");
        }

        try
        {
            if (!engine.PingAsync().GetAwaiter().GetResult())
            {
                // State stated rather than probed: the engine line says it is down, so the caller does
                // not spend a call finding out.
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
            output.Write(Show(new ContextFacts(
                EngineState: "running",
                Distribution: EnginePaths.DistributionName,
                ApiVersion: version.ApiVersion,
                ContextName: client.FromEnvironment ? "DOCKER_HOST" : client.ContextName,
                ContextReachesEngine:
                    Core.Preflight.Windows.DockerContextProbe.ReachesThisEngine(client.Host),
                Containers: containers,
                Diagnoses: diagnoses,
                Images: engine.ImagesAsync().GetAwaiter().GetResult(),
                VolumeCount: engine.VolumesAsync().GetAwaiter().GetResult().Count),
                json));
            return Ok;
        }
        catch (DockerApiException exception)
        {
            output.Write(Show(Down($"unreachable: {exception.Message}"), json));
            return NotReady;
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

    private static int RunDo(AgentVerb verb, string[] rest) => verb.Name switch
    {
        "engine" => DoEngine(rest),
        _ => Refuse($"{verb} is registered and not implemented"),
    };

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
