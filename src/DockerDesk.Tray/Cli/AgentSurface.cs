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
            "ps" => ReadPs(engine, rest, output),
            _ => Refuse($"{verb} is registered and not implemented"),
        };
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
