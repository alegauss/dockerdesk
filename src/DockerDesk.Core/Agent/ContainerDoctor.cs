using System.Globalization;
using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;
using DockerDesk.Core.Preflight;

namespace DockerDesk.Core.Agent;

/// <summary>Everything the doctor joins, gathered before any of it is judged.</summary>
/// <param name="Address">What the caller asked about.</param>
/// <param name="Summary">The container's row in the list, where it has one.</param>
/// <param name="Inspect">Its whole entity tree.</param>
/// <param name="ListeningHostPorts">
/// The host ports something is listening on, read from Windows rather than from the daemon.
/// </param>
/// <param name="StandardError">The last lines that went to stderr, newest last.</param>
/// <param name="Now">When this was gathered, so a restart window is a span rather than a date.</param>
public sealed record DoctorFacts(
    Address Address,
    ContainerSummary? Summary,
    ContainerInspect? Inspect,
    IReadOnlySet<int> ListeningHostPorts,
    IReadOnlyList<string> StandardError,
    DateTimeOffset Now);

/// <summary>
/// Why a container is not answering, as a conclusion rather than a field dump.
/// </summary>
/// <remarks>
/// DD26. Asking that question costs <c>ps -a</c>, <c>logs</c>, <c>inspect</c>, <c>port</c> and
/// <c>network inspect</c>, and the join across them is done in the caller's head. The expensive one is
/// the inspect: DD23 measured it at 1603 estimated tokens for four leaves — <c>State.ExitCode</c>,
/// <c>State.OOMKilled</c>, <c>HostConfig.PortBindings</c> and <c>Mounts</c>.
///
/// <para><b>Not a new framework.</b> The preflight already carries exactly this vocabulary — a row, a
/// <see cref="Verdict"/> and a remedy, with an exit code that means something — so this returns
/// <see cref="PreflightCheck"/> rows and <see cref="PreflightReport"/> renders them. That is reuse of a
/// concept this repository has already paid for, and it means a caller who has read one preflight can
/// read this without learning anything.</para>
///
/// <para><b>The verdict is the deliverable.</b> A command that returns forty facts and no conclusion has
/// moved the join rather than closed it, and the caller pays for the thirty-six it did not need. Where
/// there is no conclusion to draw, saying so is also a conclusion and it costs less than the fields
/// would have.</para>
/// </remarks>
public static class ContainerDoctor
{
    /// <summary>The row ids, so a caller names one without spelling a string twice.</summary>
    public static class Rows
    {
        /// <summary>Whether it is running, and what it exited with.</summary>
        public const string State = "container-state";

        /// <summary>Whether the kernel killed it, and against which limit.</summary>
        public const string Memory = "container-memory";

        /// <summary>How often it has restarted, over what window.</summary>
        public const string Restarts = "container-restarts";

        /// <summary>What its own healthcheck says.</summary>
        public const string Health = "container-health";

        /// <summary>The declared ports beside what Windows is listening on.</summary>
        public const string Ports = "container-ports";

        /// <summary>Each mount, beside whether its source resolves.</summary>
        public const string Mounts = "container-mounts";

        /// <summary>The last lines that went to stderr.</summary>
        public const string StandardError = "container-stderr";
    }

    /// <summary>How many restarts in how short a window reads as a loop.</summary>
    /// <remarks>
    /// Three inside ten minutes. A count on its own is not a story — three restarts over a month is a
    /// healthy service that was redeployed — so the window is what makes it one.
    /// </remarks>
    public const int LoopRestarts = 3;

    /// <summary>The window a restart count is read over.</summary>
    public static readonly TimeSpan LoopWindow = TimeSpan.FromMinutes(10);

    /// <summary>Diagnose one container.</summary>
    /// <param name="facts">What was gathered.</param>
    /// <returns>The report, whose rows carry the verdicts and the remedies.</returns>
    public static PreflightReport Diagnose(DoctorFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.Summary is null && facts.Inspect is null)
        {
            return new PreflightReport([new PreflightCheck
            {
                Id = Rows.State,
                Title = facts.Address.ToString(),
                Verdict = Verdict.Fail,
                Detail = "no such container on this engine",
                Remedy = "Run `dockerdesk read context` to see what is there. Addresses are names, "
                    + "so a recreated container answers to the same one.",
                Blocking = true,
            }]);
        }

        var rows = new List<PreflightCheck> { StateRow(facts) };

        if (MemoryRow(facts) is { } memory)
        {
            rows.Add(memory);
        }

        if (RestartRow(facts) is { } restarts)
        {
            rows.Add(restarts);
        }

        if (HealthRow(facts) is { } health)
        {
            rows.Add(health);
        }

        if (PortRow(facts) is { } ports)
        {
            rows.Add(ports);
        }

        if (MountRow(facts) is { } mounts)
        {
            rows.Add(mounts);
        }

        if (StandardErrorRow(facts) is { } stderr)
        {
            rows.Add(stderr);
        }

        return new PreflightReport(rows);
    }

    private static PreflightCheck StateRow(DoctorFacts facts)
    {
        var state = facts.Inspect?.State;
        var running = string.Equals(state?.Status, "running", StringComparison.Ordinal)
            || string.Equals(facts.Summary?.State, "running", StringComparison.Ordinal);

        if (running)
        {
            return new PreflightCheck
            {
                Id = Rows.State,
                Title = "state",
                Verdict = Verdict.Pass,
                Detail = facts.Summary?.Status is { Length: > 0 } status ? status : "running",
            };
        }

        var code = state?.ExitCode ?? 0;
        return new PreflightCheck
        {
            Id = Rows.State,
            Title = "state",
            Verdict = Verdict.Fail,
            Detail = $"{state?.Status ?? facts.Summary?.State ?? "unknown"}, exit {code.ToString(CultureInfo.InvariantCulture)}",
            Remedy = code == 0
                ? "It stopped cleanly. Run it again."
                : "Read the stderr row first: it will exit again for the same reason.",
            Blocking = true,
        };
    }

    /// <summary>
    /// The row that closes the canonical task.
    /// </summary>
    /// <remarks>
    /// An exit code of 137 is SIGKILL and says nothing about who sent it. <c>OOMKilled</c> says the
    /// limit did, and the limit beside it says which one — which turns two facts into the one sentence
    /// somebody can act on.
    /// </remarks>
    private static PreflightCheck? MemoryRow(DoctorFacts facts)
    {
        var state = facts.Inspect?.State;
        var limit = facts.Inspect?.HostConfig.Memory ?? 0;

        if (state?.OomKilled == true)
        {
            return new PreflightCheck
            {
                Id = Rows.Memory,
                Title = "memory",
                Verdict = Verdict.Fail,
                Detail = limit > 0
                    ? $"the kernel killed it for exceeding {ContextPack.Bytes(limit)}"
                    : "the kernel killed it for memory, and no limit is declared on the container",
                Remedy = limit > 0
                    ? $"Raise it above {ContextPack.Bytes(limit)}, or hold less."
                    : "The limit is on the host or in compose, not on the container.",
                Blocking = true,
            };
        }

        // Not a row at all when there is nothing to say. A limit that has not been hit is
        // configuration, and a doctor that lists configuration is a field dump.
        return state?.ExitCode == 137
            ? new PreflightCheck
            {
                Id = Rows.Memory,
                Title = "memory",
                Verdict = Verdict.Warn,
                Detail = "exit 137 is SIGKILL and the kernel did not report an OOM kill",
                Remedy = "Something sent it SIGKILL: a short stop timeout, an orchestrator, or the "
                    + "host running out of memory before the container's limit.",
            }
            : null;
    }

    private static PreflightCheck? RestartRow(DoctorFacts facts)
    {
        var restarts = facts.Inspect?.RestartCount ?? 0;
        if (restarts == 0)
        {
            return null;
        }

        var window = Window(facts);
        var looping = restarts >= LoopRestarts && window is not null && window <= LoopWindow;

        return new PreflightCheck
        {
            Id = Rows.Restarts,
            Title = "restarts",
            Verdict = looping ? Verdict.Fail : Verdict.Warn,
            Detail = window is null
                ? $"×{restarts.ToString(CultureInfo.InvariantCulture)}"
                : $"×{restarts.ToString(CultureInfo.InvariantCulture)} in {Brief(window.Value)}",
            Remedy = looping
                ? "A loop, so the log repeats one failure. Read the stderr row, not the log."
                : "Restarts over a long window are usually redeploys.",
            Blocking = looping,
        };
    }

    private static PreflightCheck? HealthRow(DoctorFacts facts)
    {
        if (facts.Inspect?.State.Health is not { } health || health.Status.Length == 0)
        {
            // No healthcheck declared is not a finding. Saying "none" on every container would be a
            // row that never changes and always costs.
            return null;
        }

        return string.Equals(health.Status, "healthy", StringComparison.Ordinal)
            ? new PreflightCheck
            {
                Id = Rows.Health,
                Title = "health",
                Verdict = Verdict.Pass,
                Detail = "healthy",
            }
            : new PreflightCheck
            {
                Id = Rows.Health,
                Title = "health",
                Verdict = string.Equals(health.Status, "starting", StringComparison.Ordinal)
                    ? Verdict.Warn
                    : Verdict.Fail,
                Detail = health.FailingStreak > 0
                    ? $"{health.Status}, {health.FailingStreak.ToString(CultureInfo.InvariantCulture)} failing in a row"
                    : health.Status,
                Remedy = "Its own healthcheck decided this; read that command first.",
                Blocking = !string.Equals(health.Status, "starting", StringComparison.Ordinal),
            };
    }

    /// <summary>
    /// The declared ports beside what Windows is actually listening on.
    /// </summary>
    /// <remarks>
    /// The one row Docker structurally cannot answer, because half of it is not in the daemon: the
    /// daemon knows what was published and Windows knows whether anything holds the socket. A binding
    /// with nothing behind it is the exact confusion this row removes.
    ///
    /// It says listening, not answering. Whether the service behind the port replies is DD30, needs a
    /// request rather than a socket table, and a weaker word here would make that one mean less.
    /// </remarks>
    private static PreflightCheck? PortRow(DoctorFacts facts)
    {
        var declared = new List<(int Host, string Container)>();
        foreach (var (containerPort, publishes) in facts.Inspect?.HostConfig.PortBindings
            ?? new Dictionary<string, IReadOnlyList<PortPublish>?>(StringComparer.Ordinal))
        {
            foreach (var publish in publishes ?? [])
            {
                if (int.TryParse(publish.HostPort, CultureInfo.InvariantCulture, out var host))
                {
                    declared.Add((host, containerPort));
                }
            }
        }

        if (declared.Count == 0)
        {
            return null;
        }

        var missing = new List<string>();
        var text = new List<string>();
        foreach (var (host, containerPort) in declared
            .DistinctBy(d => d.Host)
            .OrderBy(d => d.Host))
        {
            var listening = facts.ListeningHostPorts.Contains(host);
            text.Add($":{host.ToString(CultureInfo.InvariantCulture)}→{containerPort} "
                + (listening ? "listening" : "nothing listening"));
            if (!listening)
            {
                missing.Add(host.ToString(CultureInfo.InvariantCulture));
            }
        }

        return new PreflightCheck
        {
            Id = Rows.Ports,
            Title = "ports",
            Verdict = missing.Count == 0 ? Verdict.Pass : Verdict.Fail,
            Detail = string.Join(", ", text),
            Remedy = missing.Count == 0
                ? null
                : $"Port {string.Join(" and ", missing)} is published and nothing on Windows holds it: "
                    + "it is not running, or its process never bound.",
            Blocking = missing.Count > 0,
        };
    }

    /// <summary>
    /// Each mount beside whether its source resolves.
    /// </summary>
    /// <remarks>
    /// Only for a bind whose source is a mapped Windows drive, because that is the only source this
    /// tool can check from here. A volume lives inside the distribution and a path from another
    /// engine's convention is not ours to judge — both are reported as unchecked rather than as
    /// broken, since a false "does not resolve" is worse than no answer.
    /// </remarks>
    private static PreflightCheck? MountRow(DoctorFacts facts)
    {
        var mounts = facts.Inspect?.Mounts ?? [];
        if (mounts.Count == 0)
        {
            return null;
        }

        var text = new List<string>();
        var broken = 0;
        var unchecked_ = 0;

        foreach (var mount in mounts.OrderBy(m => m.Destination, StringComparer.Ordinal))
        {
            if (!string.Equals(mount.Type, "bind", StringComparison.Ordinal))
            {
                text.Add($"{mount.Destination} ← {mount.Type}:{mount.Name ?? mount.Source}");
                unchecked_++;
                continue;
            }

            var windows = Wsl.ToWindowsPath(mount.Source);
            if (windows is null)
            {
                text.Add($"{mount.Destination} ← {mount.Source} (not a mapped drive, unchecked)");
                unchecked_++;
                continue;
            }

            var resolves = Directory.Exists(windows) || File.Exists(windows);
            text.Add($"{mount.Destination} ← {windows}{(resolves ? "" : " MISSING")}");
            if (!resolves)
            {
                broken++;
            }
        }

        return new PreflightCheck
        {
            Id = Rows.Mounts,
            Title = "mounts",
            Verdict = broken > 0 ? Verdict.Fail : unchecked_ == mounts.Count ? Verdict.Unknown : Verdict.Pass,
            Detail = string.Join(", ", text),
            Remedy = broken > 0
                ? "A missing bind source gives the container an empty directory rather than an "
                    + "error, so this reads as missing code."
                : null,
            Blocking = broken > 0,
        };
    }

    /// <summary>
    /// The last lines that went to stderr, rather than the whole log.
    /// </summary>
    /// <remarks>
    /// The log is the largest token sink here and a restart loop repeats the same trace, so the whole
    /// of it is the wrong thing to return. A bounded tail of the stream a failure actually writes to is
    /// what a conclusion needs; making that read cheap in general — dedup, a cursor, a level — is DD27.
    /// </remarks>
    private static PreflightCheck? StandardErrorRow(DoctorFacts facts)
    {
        if (facts.StandardError.Count == 0)
        {
            return null;
        }

        return new PreflightCheck
        {
            Id = Rows.StandardError,
            Title = "stderr",
            Verdict = Verdict.Warn,
            Detail = string.Join(" | ", facts.StandardError),
        };
    }

    /// <summary>How long ago it last started, where the daemon said.</summary>
    private static TimeSpan? Window(DoctorFacts facts)
    {
        var started = facts.Inspect?.State.StartedAt;
        return DateTimeOffset.TryParse(
            started, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            && at > DateTimeOffset.UnixEpoch
            ? facts.Now - at
            : null;
    }

    private static string Brief(TimeSpan span) => span switch
    {
        { TotalSeconds: < 90 } => $"{Math.Max(1, (int)span.TotalSeconds).ToString(CultureInfo.InvariantCulture)}s",
        { TotalMinutes: < 90 } => $"{(int)span.TotalMinutes}m",
        { TotalHours: < 48 } => $"{(int)span.TotalHours}h",
        _ => $"{(int)span.TotalDays}d",
    };
}
