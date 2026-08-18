using System.Text;
using System.Text.Json;

namespace FreeWilly.Core.Agent;

/// <summary>What one <c>docker compose</c> invocation produced.</summary>
/// <param name="ExitCode">Its exit code, or <see langword="null"/> if it never ran.</param>
/// <param name="Output">What it wrote, decoded.</param>
/// <param name="Failure">Why it never ran or never finished.</param>
public sealed record ComposeResult(int? ExitCode, string Output, string? Failure)
{
    /// <summary>Whether it ran and reported success.</summary>
    public bool Succeeded => Failure is null && ExitCode == 0;
}

/// <summary>The bundled <c>docker</c> CLI, behind a seam.</summary>
/// <remarks>
/// A subprocess and not the Engine API, and that is the one place this surface reaches for the CLI:
/// composing is a client-side algorithm — merge the files, resolve the variables, build what needs
/// building, order the dependencies — and none of it lives in the daemon. Reimplementing it against
/// <c>/containers/create</c> would be a second compose, which is a larger thing to be wrong than
/// this whole surface.
/// </remarks>
public interface IComposeCli
{
    /// <summary>Run <c>docker</c> with these arguments.</summary>
    /// <param name="workingDirectory">Where the caller is, which is where the project is.</param>
    /// <param name="arguments">The arguments, already split — never a command line to be parsed.</param>
    /// <returns>What it produced.</returns>
    ComposeResult Run(string workingDirectory, params string[] arguments);
}

/// <summary>
/// Bringing a compose project up with everything it creates stamped for the session (DD63).
/// </summary>
/// <remarks>
/// DD29 shipped the label, the plan and the confirm token, and until this verb existed every one of
/// them was exercised against fixtures alone: the surface had two <c>do</c> verbs and neither
/// created anything, so <c>read changes --session</c> answered "nothing carries this session's
/// label" on every real machine and always would.
///
/// <para><b>The hard part is that compose has no <c>--label</c>.</b> Labels are a property of a
/// service in the file, the Engine API cannot add one to a container that already exists, and
/// recreating somebody's container to relabel it is exactly the thing this surface refuses to do to
/// their work. So the labels are injected the way compose itself supports: a second file. What that
/// file has to say comes from <c>compose config --format json</c> rather than from a YAML parser
/// here — the CLI is already the authority on what the merged project contains, and a parser of our
/// own would be a second opinion about somebody's file.</para>
///
/// <para><b>The same file carries the bind sources (DD75).</b> A bind source is a path the daemon
/// resolves, and the daemon is Linux: compose turns <c>./data</c> into <c>D:\project\data</c> before
/// anything here sees it, and an upstream daemon refuses that with <c>invalid mode: /data</c> —
/// measured. So every bind whose source carries a drive letter is respelled the distribution's way,
/// which resolves because WSL mounts the drives under <c>/mnt</c> and this install writes no
/// <c>[automount]</c> section that would turn that off. Compose merges <c>volumes</c> on the target,
/// so an entry in the override replaces the project's own for that container path.</para>
///
/// <para><b>The override is written outside the project.</b> A <c>do</c> verb may create containers;
/// it may not leave a file in a directory somebody is working in, and a generated
/// <c>compose.override.yml</c> is exactly the file that gets committed by accident. Compose takes
/// <c>-f</c> at any path and takes its project directory from the <em>first</em> one, so the user's
/// file stays the project and every relative build context and bind mount in it still resolves.</para>
///
/// <para><b>What must not be inferred.</b> Compose stamps what it creates with its own project and
/// service labels, and a container it made carries them whether this stamped it or not. Two label
/// sets on one object is fine. A reclaim that read ownership off the compose project would not be:
/// a project outlives a session, and the user's own <c>docker compose up</c> writes the same project
/// label — so it would offer to delete work this tool never touched.</para>
/// </remarks>
public static class ComposeUp
{
    /// <summary>What the generated override is called, wherever it is written.</summary>
    public const string OverrideFileName = "freewilly-session.override.yml";

    /// <summary>
    /// The file names compose looks for, in its own order of preference.
    /// </summary>
    /// <remarks>
    /// Compose's order, not this project's taste: a directory holding both <c>compose.yaml</c> and
    /// <c>docker-compose.yml</c> is a directory where the CLI has already decided, and picking
    /// differently here would bring up a project the caller cannot see in their own file.
    /// </remarks>
    public static readonly IReadOnlyList<string> FileNames =
        ["compose.yaml", "compose.yml", "docker-compose.yaml", "docker-compose.yml"];

    /// <summary>The compose file a directory holds, or <see langword="null"/> where it holds none.</summary>
    /// <param name="directory">Where the caller is.</param>
    /// <param name="exists">Whether a file is there — a parameter so this is testable.</param>
    /// <returns>The full path, or null.</returns>
    public static string? FileIn(string directory, Func<string, bool> exists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(exists);

        foreach (var name in FileNames)
        {
            var candidate = Path.Combine(directory, name);
            if (exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// One bind mount a service declares, as compose resolved it.
    /// </summary>
    /// <param name="Source">Where it comes from, absolute, in whatever spelling compose produced.</param>
    /// <param name="Target">Where it lands inside the container.</param>
    /// <param name="ReadOnly">Whether the service asked for it read-only.</param>
    public sealed record ComposeBind(string Source, string Target, bool ReadOnly);

    /// <summary>One service, and the bind mounts it declares.</summary>
    /// <param name="Name">The service name.</param>
    /// <param name="Binds">Its bind mounts. Named volumes are not here — they need no translating.</param>
    public sealed record ComposeService(string Name, IReadOnlyList<ComposeBind> Binds);

    /// <summary>
    /// The override file names compose looks for, in its own order of preference (DD143).
    /// </summary>
    /// <remarks>
    /// Measured against the real CLI rather than derived, because the obvious derivation is wrong.
    /// An override does <em>not</em> belong to the file it overrides: a directory holding
    /// <c>docker-compose.yml</c> beside <c>compose.override.yaml</c> gets both applied. And the
    /// order is not <see cref="FileNames"/>'s — that one prefers <c>.yaml</c>, and this one prefers
    /// <c>.yml</c>. With all four present compose says so out loud: "Found multiple override files
    /// with supported names: compose.override.yml, compose.override.yaml,
    /// docker-compose.override.yml" and then "Using compose.override.yml", which is where this list
    /// and its order come from.
    /// </remarks>
    public static readonly IReadOnlyList<string> OverrideFileNames =
    [
        "compose.override.yml",
        "compose.override.yaml",
        "docker-compose.override.yml",
        "docker-compose.override.yaml",
    ];

    /// <summary>
    /// The override compose would apply here by convention, or <see langword="null"/> (DD143).
    /// </summary>
    /// <param name="directory">Where the caller is.</param>
    /// <param name="exists">Whether a file is there — a parameter so this is testable.</param>
    /// <returns>The full path, or null where the project is one file.</returns>
    /// <remarks>
    /// <b>Passing <c>-f</c> is what makes this necessary.</b> Compose reads a base file and an
    /// optional override of its own accord, and it stops the moment a caller names files explicitly
    /// — so this verb, which has to name files because it injects one, silently turned a two-file
    /// project into a one-file project. Reproduced against the real CLI in an empty directory:
    /// with no <c>-f</c>, <c>config --services</c> answered <c>base</c> and <c>extra</c>; with one
    /// <c>-f</c>, <c>base</c> alone; with both files named, <c>base</c> and <c>extra</c> again.
    /// </remarks>
    public static string? OverrideIn(string directory, Func<string, bool> exists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(exists);

        foreach (var name in OverrideFileNames)
        {
            var candidate = Path.Combine(directory, name);
            if (exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Every file of the user's project, in the order compose would read them (DD143).</summary>
    /// <param name="directory">Where the caller is.</param>
    /// <param name="exists">Whether a file is there — a parameter so this is testable.</param>
    /// <returns>The files, base first; empty where the directory holds no compose file.</returns>
    public static IReadOnlyList<string> ProjectFiles(string directory, Func<string, bool> exists)
    {
        if (FileIn(directory, exists) is not { } composeFile)
        {
            return [];
        }

        return OverrideIn(directory, exists) is { } over
            ? [composeFile, over]
            : [composeFile];
    }

    /// <summary>The arguments that ask compose what the project resolves to.</summary>
    /// <param name="projectFiles">The user's files, in compose's own order.</param>
    /// <returns>The argument list handed to the CLI.</returns>
    /// <remarks>
    /// JSON, and one call rather than two. This replaced <c>config --services</c>, which answered the
    /// service names and nothing else — and DD75 needs the resolved bind sources from the same read,
    /// because compose has already turned <c>./data</c> into an absolute Windows path by the time
    /// anything here can see it.
    ///
    /// <para>Every file and not the first one (DD143): this read is what decides which services get
    /// stamped and which binds get respelled, so a project read here more narrowly than it is
    /// brought up would stamp some of what it created and not the rest.</para>
    /// </remarks>
    public static string[] ConfigArguments(IReadOnlyList<string> projectFiles)
    {
        ArgumentNullException.ThrowIfNull(projectFiles);
        return [.. Named(projectFiles), "config", "--format", "json"];
    }

    /// <summary><c>compose</c> followed by a <c>-f</c> for each file, in order.</summary>
    private static IEnumerable<string> Named(IEnumerable<string> files)
    {
        yield return "compose";
        foreach (var file in files)
        {
            yield return "-f";
            yield return file;
        }
    }

    /// <summary>
    /// The services and their bind mounts, out of what <c>config --format json</c> wrote.
    /// </summary>
    /// <param name="json">The CLI's output.</param>
    /// <returns>One entry per service, in the order compose listed them.</returns>
    /// <exception cref="FormatException">Where the output is not the document compose emits.</exception>
    public static IReadOnlyList<ComposeService> Project(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("services", out var services)
                || services.ValueKind is not JsonValueKind.Object)
            {
                return [];
            }

            var found = new List<ComposeService>();
            foreach (var service in services.EnumerateObject())
            {
                var binds = new List<ComposeBind>();
                if (service.Value.TryGetProperty("volumes", out var volumes)
                    && volumes.ValueKind is JsonValueKind.Array)
                {
                    foreach (var volume in volumes.EnumerateArray())
                    {
                        // Only binds. A named volume's "source" is a volume name, and translating
                        // one would turn a managed volume into a path.
                        if (volume.ValueKind is not JsonValueKind.Object
                            || volume.TryGetProperty("type", out var type) is false
                            || !string.Equals(type.GetString(), "bind", StringComparison.Ordinal)
                            || !volume.TryGetProperty("source", out var source)
                            || !volume.TryGetProperty("target", out var target))
                        {
                            continue;
                        }

                        binds.Add(new ComposeBind(
                            source.GetString() ?? "",
                            target.GetString() ?? "",
                            volume.TryGetProperty("read_only", out var ro)
                                && ro.ValueKind is JsonValueKind.True));
                    }
                }

                found.Add(new ComposeService(service.Name, binds));
            }

            return found;
        }
        catch (JsonException exception)
        {
            throw new FormatException(
                "compose config did not answer with the JSON document it documents: "
                + exception.Message,
                exception);
        }
    }

    /// <summary>Whether a bind source is a Windows path the Linux daemon cannot resolve.</summary>
    /// <param name="source">The source compose resolved.</param>
    /// <returns><see langword="true"/> where it has to be translated before the daemon sees it.</returns>
    /// <remarks>
    /// A drive letter is the whole test, and it is what makes this decidable rather than a guess.
    /// Measured against an upstream daemon (DD75): <c>D:\project:/data</c> is refused with
    /// <c>invalid mode: /data</c>, because the daemon splits the spec on <c>:</c> and the drive
    /// letter's colon lands in the middle of it. A source already spelled <c>/mnt/d/…</c> is left
    /// alone — that one resolves, because the distribution this engine runs in writes no
    /// <c>[automount]</c> section and WSL mounts the drives by default.
    /// </remarks>
    public static bool NeedsTranslating(string source) => Engine.Wsl.HasDriveLetter(source);

    /// <summary>The override that stamps every service with the session label.</summary>
    /// <param name="services">The service names, as the CLI listed them.</param>
    /// <param name="session">The session id.</param>
    /// <returns>The YAML to write.</returns>
    /// <remarks>
    /// The value is quoted, and that is not tidiness: a derived id is <c>dir:8f21a0</c>, and YAML
    /// reads an unquoted colon as a mapping. Unquoted, the label a reclaim looks for would be
    /// written as a nested key and the stamp would silently not be there.
    ///
    /// <para><b>Every label the session carries, not the one this file knows the name of</b> (DD79).
    /// The keys come from <see cref="SessionLabel.For"/>, so a second one added there arrives here
    /// without this method being touched — which it would not have before, when the key was spelled
    /// into the string by hand.</para>
    /// </remarks>
    public static string Override(IEnumerable<ComposeService> services, string session)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(session);

        // Sorted, because a dictionary promises no order and this is a file: the same session has to
        // generate the same bytes, or a diff of two runs shows a change nobody made.
        var labels = SessionLabel.For(session)
            .OrderBy(label => label.Key, StringComparer.Ordinal)
            .ToList();

        var text = new StringBuilder();
        text.AppendLine("# Generated by `freewilly do compose up`, outside your project on purpose.");
        text.AppendLine("# It stamps this session's label so `do reclaim --session` can take it back,");
        text.AppendLine("# and respells any bind source the Linux daemon could not have resolved.");
        text.AppendLine("services:");
        foreach (var service in services)
        {
            text.Append("  ").Append(service.Name).AppendLine(":");
            text.AppendLine("    labels:");
            foreach (var label in labels)
            {
                text.Append("      ").Append(label.Key).Append(": \"").Append(label.Value)
                    .AppendLine("\"");
            }

            // Compose merges `volumes` on the target, so an entry here replaces the project's own
            // for that container path rather than adding a second mount — measured against the real
            // CLI, because the merge rule for a sequence differs per key and guessing it wrong would
            // mount the same target twice.
            var translated = service.Binds
                .Where(bind => NeedsTranslating(bind.Source))
                .ToList();
            if (translated.Count == 0)
            {
                continue;
            }

            text.AppendLine("    volumes:");
            foreach (var bind in translated)
            {
                var mode = bind.ReadOnly ? ":ro" : "";
                text.Append("      - \"")
                    .Append(Engine.Wsl.ToDistributionPath(bind.Source))
                    .Append(':').Append(bind.Target).Append(mode).AppendLine("\"");
            }
        }

        return text.ToString();
    }

    /// <summary>The arguments that bring the project up, stamped.</summary>
    /// <param name="projectFiles">The user's files, first so the first one stays the project.</param>
    /// <param name="overrideFile">The generated stamp.</param>
    /// <returns>The argument list handed to the CLI.</returns>
    /// <remarks>
    /// Detached, because a <c>do</c> verb answers and returns: an agent that blocked on a foreground
    /// <c>up</c> would hold the call open for the life of the containers and read the whole of their
    /// output as its answer, which is the token sink <c>read logs</c> exists to bound.
    ///
    /// <para>The generated file is last, and that is what makes it an override rather than a
    /// project: compose merges in order, and its project directory comes from the first file — so
    /// every relative build context and bind mount in the user's own files still resolves against
    /// the directory they are in.</para>
    /// </remarks>
    public static string[] UpArguments(IReadOnlyList<string> projectFiles, string overrideFile)
    {
        ArgumentNullException.ThrowIfNull(projectFiles);
        return [.. Named([.. projectFiles, overrideFile]), "up", "-d"];
    }
}
