using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FreeWilly.Core.Agent;

/// <summary>
/// A refusal an agent can act on without a second call.
/// </summary>
/// <param name="Type">
/// A stable identifier for the kind of refusal, so a caller branches on this rather than on prose.
/// </param>
/// <param name="Status">The HTTP status this corresponds to, which is the vocabulary already in use.</param>
/// <param name="Title">One line, which is what a reader sees first.</param>
/// <param name="Fix">The action that changes it. A refusal without one has moved the problem.</param>
/// <param name="Allowed">What would have been accepted, where the refusal is about an argument.</param>
/// <param name="NearestMatch">The closest thing that exists, where something close does.</param>
/// <param name="Example">A minimal correct call.</param>
/// <param name="Facts">
/// What Windows knows and the daemon does not — the field that makes this surface worth having.
/// </param>
public sealed record AgentProblem(
    string Type,
    int Status,
    string Title,
    string Fix,
    string? Allowed = null,
    string? NearestMatch = null,
    string? Example = null,
    IReadOnlyDictionary<string, string>? Facts = null)
{
    /// <summary>Where a type identifier lives, so it reads as a stable name rather than a URL to fetch.</summary>
    public const string TypeBase = "https://github.com/alegauss/dockerdesk/errors/";

    /// <summary>
    /// As lines, which is what a caller gets unless it asked otherwise.
    /// </summary>
    /// <returns>The text, ending in a newline.</returns>
    /// <remarks>
    /// The default because a refusal an agent can act on is read rather than parsed, and one convention
    /// for the whole surface is cheaper than two. <c>--json</c> turns every payload structured, answers
    /// and refusals alike.
    /// </remarks>
    public string ToText()
    {
        var text = new StringBuilder();
        text.AppendLine(Title);
        foreach (var (name, value) in Facts ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            text.Append("  ").Append(name.PadRight(8)).Append("  ").AppendLine(value);
        }

        if (NearestMatch is not null)
        {
            text.Append("  ").Append("nearest".PadRight(8)).Append("  ").AppendLine(NearestMatch);
        }

        if (Allowed is not null)
        {
            text.Append("  ").Append("allowed".PadRight(8)).Append("  ").AppendLine(Allowed);
        }

        text.Append("  ").Append("fix".PadRight(8)).Append("  ").AppendLine(Fix);

        if (Example is not null)
        {
            text.Append("  ").Append("example".PadRight(8)).Append("  ").AppendLine(Example);
        }

        return text.ToString();
    }

    /// <summary>As one JSON object, for a caller that parses.</summary>
    /// <returns>The document, ending in a newline.</returns>
    public string ToJson()
    {
        var document = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = TypeBase + Type,
            ["status"] = Status,
            ["title"] = Title,
            ["fix"] = Fix,
        };

        if (Allowed is not null)
        {
            document["allowed"] = Allowed;
        }

        if (NearestMatch is not null)
        {
            document["nearestMatch"] = NearestMatch;
        }

        if (Example is not null)
        {
            document["example"] = Example;
        }

        foreach (var (name, value) in Facts ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            document[name] = value;
        }

        return JsonSerializer.Serialize(document) + Environment.NewLine;
    }

    /// <summary>The one an agent cannot act on without this.</summary>
    /// <param name="port">The host port.</param>
    /// <param name="holder">What holds it, where anything does.</param>
    /// <returns>The refusal.</returns>
    /// <remarks>
    /// <c>heldBy</c> is the argument for this surface existing. A JSON re-wrapping of what
    /// <c>docker</c> already says adds nothing, because <c>--format json</c> exists; the joins the
    /// Engine API cannot make are the whole of the value, and they are available only because this is a
    /// Windows process rather than a client.
    /// </remarks>
    public static AgentProblem PortAllocated(int port, PortHolder? holder)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (holder is not null)
        {
            facts["heldBy"] =
                $"pid {holder.Pid.ToString(CultureInfo.InvariantCulture)}  {holder.Image}"
                + (holder.Path is null ? "  (path not readable by this process)" : "  " + holder.Path);
        }

        return new AgentProblem(
            Type: "port-allocated",
            Status: 409,
            Title: $"port {port.ToString(CultureInfo.InvariantCulture)} is already held on this machine",
            Fix: holder is null
                ? $"Something holds port {port.ToString(CultureInfo.InvariantCulture)} that this process "
                    + "may not identify. Publish a different host port."
                : $"Stop process {holder.Pid.ToString(CultureInfo.InvariantCulture)} "
                    + $"({holder.Image}), or publish a different host port.",
            Facts: facts);
    }

    /// <summary>
    /// One of the three unrelated causes of "cannot connect to the Docker daemon".
    /// </summary>
    /// <param name="rivals">Engines already on this machine, per DD16.</param>
    /// <param name="client">Where this user's CLI points, per DD20.</param>
    /// <param name="ourPipe">The pipe this engine serves.</param>
    /// <returns>The refusal, naming which of the three it is.</returns>
    /// <remarks>
    /// That one sentence covers three causes with three different remedies, and an error that names the
    /// wrong cause is worse than none. The facts to tell them apart already exist — DD16 reads what owns
    /// the docker command and DD20 reads where the CLI points — and were being thrown away at exactly
    /// the moment somebody needed them.
    /// </remarks>
    public static AgentProblem CannotConnect(
        IReadOnlyList<Preflight.RivalEngine> rivals,
        Preflight.DockerClientTarget? client,
        string ourPipe)
    {
        ArgumentNullException.ThrowIfNull(rivals);

        if (rivals.Count > 0)
        {
            var rival = rivals[0];
            return new AgentProblem(
                Type: "rival-engine",
                Status: 409,
                Title: $"another container engine is installed: {rival.Name}",
                Fix: "Uninstall it, or stop it while this engine runs. Two engines competing for "
                    + $"\\\\.\\pipe\\{ourPipe} leaves neither of them working.",
                Facts: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["found"] = rival.Evidence,
                });
        }

        if (client is not null
            && client.Host is { Length: > 0 } host
            && !Preflight.Windows.DockerContextProbe.ReachesThisEngine(host))
        {
            return new AgentProblem(
                Type: "context-elsewhere",
                Status: 409,
                Title: client.FromEnvironment
                    ? "DOCKER_HOST points somewhere other than this engine"
                    : $"your docker context ({client.ContextName}) points somewhere else",
                Fix: client.FromEnvironment
                    ? $"Clear DOCKER_HOST, or point it at \\\\.\\pipe\\{ourPipe}."
                    : "Run `docker context use default`. Nothing here changes it for you.",
                Facts: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["points"] = host,
                    ["engine"] = $"\\\\.\\pipe\\{ourPipe}",
                });
        }

        return new AgentProblem(
            Type: "engine-stopped",
            Status: 503,
            Title: "the engine is not running",
            Fix: "Run `freewilly do engine start`.",
            Example: "freewilly do engine start");
    }

    /// <summary>A name this surface does not have, with the closest one that it does.</summary>
    /// <param name="what">What kind of name it was.</param>
    /// <param name="given">What the caller said.</param>
    /// <param name="known">Every name there is.</param>
    /// <returns>The refusal.</returns>
    public static AgentProblem NoSuchName(string what, string given, IReadOnlyList<string> known)
    {
        ArgumentNullException.ThrowIfNull(known);

        var nearest = Nearest(given, known);
        return new AgentProblem(
            Type: "no-such-name",
            Status: 404,
            Title: $"no {what} named {given}",
            Fix: nearest is null
                ? "Run `freewilly read context` to see what is there."
                : $"Did you mean {nearest}?",
            Allowed: known.Count == 0 ? "(none)" : string.Join(", ", known.Order(StringComparer.Ordinal)),
            NearestMatch: nearest);
    }

    /// <summary>
    /// The closest known name, where one is close enough to be worth suggesting.
    /// </summary>
    /// <remarks>
    /// A suggestion that is not close is worse than none: it sends a caller to spend a call on the wrong
    /// thing. So the distance has to be within a third of the longer of the two names — measured against
    /// what was typed instead, truncating a name would refuse the one suggestion worth making.
    /// </remarks>
    internal static string? Nearest(string given, IReadOnlyList<string> known)
    {
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in known)
        {
            var distance = Distance(given, candidate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best is not null
               && bestDistance <= Math.Max(1, Math.Max(given.Length, best.Length) / 3)
            ? best
            : null;
    }

    /// <summary>Levenshtein distance, which is enough for a typo and cheap enough to be free.</summary>
    private static int Distance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
