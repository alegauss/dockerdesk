using System.Text.Json;
using FreeWilly.Core.Api;

namespace FreeWilly.Core.Preflight.Windows;

/// <summary>
/// Where the user's own <c>docker</c> will send a request, read the way the CLI decides it.
/// </summary>
/// <remarks>
/// The engine is reached through <c>\\.\pipe\docker_engine</c>, which is what the CLI's
/// <c>default</c> context names. But the CLI does not read that context unless it is the active one,
/// and the active one is a per-user setting any Docker distribution may have written. Measured on the
/// development machine, with <c>currentContext</c> set to <c>desktop-linux</c>: <c>docker version</c>
/// reported the daemon absent while the engine was answering, and
/// <c>docker --context default version</c> reached it. The tool looks broken and nothing is wrong
/// with it (DD20).
///
/// Nothing here writes. DD20 had two candidate answers and this is the one chosen: registering a
/// context of this project's own and making it active is what a rival does, and it takes a per-user
/// setting over. Saying where the CLI points leaves the choice with the person whose setting it is.
///
/// Reading, not running <c>docker context ls</c>: the whole situation is one where the CLI on PATH
/// belongs to somebody else, and asking it to describe itself would be asking the suspect.
/// </remarks>
public static class DockerContextProbe
{
    /// <summary>The context name the CLI uses when nothing has selected another.</summary>
    public const string DefaultContextName = "default";

    /// <summary>Read where this user's CLI points.</summary>
    /// <returns>The target, or why it could not be read.</returns>
    public static DockerClientTarget Read()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var root = Path.Combine(home, ".docker");

        return Resolve(
            Environment.GetEnvironmentVariable("DOCKER_HOST"),
            ReadCurrentContext(Path.Combine(root, "config.json")),
            ReadStore(Path.Combine(root, "contexts", "meta")));
    }

    /// <summary>
    /// Decide where the CLI points, from the three things that decide it.
    /// </summary>
    /// <param name="dockerHost">The <c>DOCKER_HOST</c> variable, or null.</param>
    /// <param name="currentContext">
    /// <c>currentContext</c> from <c>config.json</c>, or null where the file or key is absent.
    /// </param>
    /// <param name="store">Every context in the store, as name and endpoint.</param>
    /// <returns>The target.</returns>
    /// <remarks>
    /// The precedence is the CLI's own and it matters for the remedy: <c>DOCKER_HOST</c> wins over
    /// the active context, so telling somebody to run <c>docker context use</c> while that variable
    /// is set would be advice that changes nothing.
    /// </remarks>
    public static DockerClientTarget Resolve(
        string? dockerHost,
        string? currentContext,
        IReadOnlyList<(string Name, string? Host)> store)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (!string.IsNullOrWhiteSpace(dockerHost))
        {
            return new DockerClientTarget
            {
                ContextName = null,
                Host = dockerHost.Trim(),
                FromEnvironment = true,
            };
        }

        var name = string.IsNullOrWhiteSpace(currentContext)
            ? DefaultContextName
            : currentContext.Trim();

        if (name.Equals(DefaultContextName, StringComparison.Ordinal))
        {
            // `default` is never in the store — the CLI synthesises it, and on Windows it is the
            // named pipe this engine serves. Verified: the store on the development machine held
            // desktop-linux and desktop-windows and no default.
            return new DockerClientTarget
            {
                ContextName = DefaultContextName,
                Host = $"npipe:////./pipe/{DockerApi.DefaultPipeName}",
            };
        }

        var found = store.FirstOrDefault(entry =>
            entry.Name.Equals(name, StringComparison.Ordinal));

        return new DockerClientTarget
        {
            ContextName = name,
            Host = found.Name is null ? null : found.Host,
            // An active context with nothing behind it is a real state — a rival's uninstall can
            // take the store entry and leave the setting — and the CLI fails outright there. Worth
            // distinguishing from "points somewhere else".
            Unreadable = found.Name is null
                ? $"the active context {name} is not in this user's context store"
                : null,
        };
    }

    /// <summary>The pipe name inside an <c>npipe:</c> endpoint, or null for anything else.</summary>
    /// <param name="host">The endpoint.</param>
    /// <returns>The pipe name.</returns>
    /// <remarks>
    /// Compared by pipe name rather than by string, because the same pipe is spelled
    /// <c>npipe:////./pipe/x</c> and <c>npipe://./pipe/x</c> and both reach it.
    /// </remarks>
    public static string? PipeName(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        const string marker = "/pipe/";
        var at = host.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0 || !host.StartsWith("npipe:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = host[(at + marker.Length)..].Trim().TrimEnd('/');
        return name.Length == 0 ? null : name;
    }

    /// <summary>Whether an endpoint reaches the pipe this project's engine serves.</summary>
    /// <param name="host">The endpoint.</param>
    /// <returns><see langword="true"/> when it is this engine's pipe.</returns>
    public static bool ReachesThisEngine(string? host) =>
        PipeName(host) is { } pipe
        && pipe.Equals(DockerApi.DefaultPipeName, StringComparison.OrdinalIgnoreCase);

    private static string? ReadCurrentContext(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(configPath));
            return document.RootElement.TryGetProperty("currentContext", out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Every context in the store, by reading each <c>meta.json</c> rather than by hashing a name.
    /// </summary>
    /// <remarks>
    /// The directory under <c>meta</c> is a digest of the context's name, and reproducing that
    /// scheme would be depending on an implementation detail of somebody else's tool. Each file
    /// already states its own <c>Name</c>, so the answer is in the files.
    /// </remarks>
    private static IReadOnlyList<(string Name, string? Host)> ReadStore(string metaRoot)
    {
        var found = new List<(string, string?)>();
        try
        {
            if (!Directory.Exists(metaRoot))
            {
                return found;
            }

            foreach (var directory in Directory.EnumerateDirectories(metaRoot))
            {
                var file = Path.Combine(directory, "meta.json");
                if (!File.Exists(file))
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllBytes(file));
                    if (!document.RootElement.TryGetProperty("Name", out var name)
                        || name.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string? host = null;
                    if (document.RootElement.TryGetProperty("Endpoints", out var endpoints)
                        && endpoints.TryGetProperty("docker", out var docker)
                        && docker.TryGetProperty("Host", out var value)
                        && value.ValueKind == JsonValueKind.String)
                    {
                        host = value.GetString();
                    }

                    found.Add((name.GetString()!, host));
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException or JsonException)
                {
                    // One unreadable context is not a reason to stop reading the rest.
                }
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or ArgumentException)
        {
            // No store is the same as an empty one for this row's purposes.
        }

        return found;
    }
}
