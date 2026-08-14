using System.Text;

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
/// their work. So the labels are injected the way compose itself supports: a second file. The
/// service names come from <c>compose config --services</c> rather than from a YAML parser here —
/// the CLI is already the authority on what the merged project contains, and a parser of our own
/// would be a second opinion about somebody's file.</para>
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

    /// <summary>The override that stamps every service with the session label.</summary>
    /// <param name="services">The service names, as the CLI listed them.</param>
    /// <param name="session">The session id.</param>
    /// <returns>The YAML to write.</returns>
    /// <remarks>
    /// The value is quoted, and that is not tidiness: a derived id is <c>dir:8f21a0</c>, and YAML
    /// reads an unquoted colon as a mapping. Unquoted, the label a reclaim looks for would be
    /// written as a nested key and the stamp would silently not be there.
    /// </remarks>
    public static string Override(IEnumerable<string> services, string session)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(session);

        var text = new StringBuilder();
        text.AppendLine("# Generated by `freewilly do compose up`, outside your project on purpose.");
        text.AppendLine("# It stamps this session's label so `do reclaim --session` can take it back.");
        text.AppendLine("services:");
        foreach (var service in services)
        {
            text.Append("  ").Append(service).AppendLine(":");
            text.AppendLine("    labels:");
            text.Append("      ").Append(SessionLabel.Key).Append(": \"").Append(session)
                .AppendLine("\"");
        }

        return text.ToString();
    }

    /// <summary>The arguments that list a project's services.</summary>
    /// <param name="composeFile">The user's file.</param>
    /// <returns>The argument list handed to the CLI.</returns>
    public static string[] ServicesArguments(string composeFile) =>
        ["compose", "-f", composeFile, "config", "--services"];

    /// <summary>The arguments that bring the project up, stamped.</summary>
    /// <param name="composeFile">The user's file, first so it stays the project.</param>
    /// <param name="overrideFile">The generated stamp.</param>
    /// <returns>The argument list handed to the CLI.</returns>
    /// <remarks>
    /// Detached, because a <c>do</c> verb answers and returns: an agent that blocked on a foreground
    /// <c>up</c> would hold the call open for the life of the containers and read the whole of their
    /// output as its answer, which is the token sink <c>read logs</c> exists to bound.
    /// </remarks>
    public static string[] UpArguments(string composeFile, string overrideFile) =>
        ["compose", "-f", composeFile, "-f", overrideFile, "up", "-d"];

    /// <summary>The service names out of what <c>config --services</c> wrote.</summary>
    /// <param name="output">The CLI's output.</param>
    /// <returns>One name per line it printed, in order.</returns>
    public static IReadOnlyList<string> Services(string? output) =>
        [.. (output ?? "")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)];
}
