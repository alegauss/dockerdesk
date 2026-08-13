using System.IO;
using FreeWilly.Core.Api;

namespace FreeWilly.Tray.Ui;

/// <summary>What to run, and with what arguments, to put a terminal in front of the user.</summary>
/// <param name="FileName">The terminal program.</param>
/// <param name="Arguments">Its arguments, unquoted — the caller hands these to an argument list.</param>
public sealed record ShellLaunch(string FileName, IReadOnlyList<string> Arguments);

/// <summary>
/// Opening a shell by launching the terminal the user already has.
/// </summary>
/// <remarks>
/// The alternative is a terminal emulator inside the window: attaching to the exec stream, handling
/// resize, and interpreting ANSI escape sequences. That is a large amount of code to reimplement a
/// program already installed on the machine, and it would compete for effort with the engine work
/// that is this project's reason to exist.
///
/// So the whole feature is one process launch — and everything worth testing is the decision in
/// front of it: which shell the image actually has, and which terminal to hand it to.
/// </remarks>
public static class ContainerShell
{
    /// <summary>
    /// The shells to try, in order.
    /// </summary>
    /// <remarks>
    /// bash first because it is what a person expects when they get a prompt, sh second because it
    /// is what an alpine image has. A distroless or scratch image has neither, which is a thing to
    /// say rather than a window to open and close.
    /// </remarks>
    public static readonly IReadOnlyList<string> Candidates = ["/bin/bash", "/bin/sh"];

    /// <summary>
    /// A command that succeeds if <paramref name="shell"/> is there and fails if it is not.
    /// </summary>
    /// <param name="shell">The shell to test for.</param>
    /// <returns>The command to exec.</returns>
    public static IReadOnlyList<string> ProbeFor(string shell) => [shell, "-c", "exit 0"];

    /// <summary>
    /// Find the first shell the container actually has.
    /// </summary>
    /// <remarks>
    /// Asked before the terminal is launched, because the section this implements says the answer
    /// for an image with no shell is to say so — and that is only possible before a window exists.
    ///
    /// A missing binary comes back as a refusal from the daemon rather than a non-zero exit, so
    /// both are the same answer here: not this one, try the next.
    /// </remarks>
    /// <param name="run">Runs a command in the container and returns its exit code.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The shell, or <see langword="null"/> when the image has none.</returns>
    public static async Task<string?> FindAsync(
        Func<IReadOnlyList<string>, CancellationToken, Task<int>> run,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        foreach (var shell in Candidates)
        {
            try
            {
                if (await run(ProbeFor(shell), cancellation).ConfigureAwait(false) == 0)
                {
                    return shell;
                }
            }
            catch (DockerApiException)
            {
                // "stat /bin/bash: no such file or directory" is the daemon answering the question,
                // not the call going wrong.
            }
        }

        return null;
    }

    /// <summary>What the row says when the image turned out to have no shell.</summary>
    /// <param name="name">The container.</param>
    /// <returns>One sentence.</returns>
    public static string NoShellMessage(string name) =>
        $"{name} has no shell: the image has neither {Candidates[0]} nor {Candidates[1]}. "
        + "A distroless or scratch image is built that way on purpose.";

    /// <summary>
    /// What to launch: the terminal, running this project's own docker CLI against the container.
    /// </summary>
    /// <remarks>
    /// The CLI path is the one this tool installed, not whatever <c>docker</c> resolves to on PATH.
    /// A machine with another Docker installed would otherwise get that client talking to this
    /// project's engine, or to its own.
    /// </remarks>
    /// <param name="terminal">The terminal program, from <see cref="Terminals.Choose"/>.</param>
    /// <param name="dockerCli">The full path to <c>docker.exe</c>.</param>
    /// <param name="containerId">The container.</param>
    /// <param name="shell">The shell found by <see cref="FindAsync"/>.</param>
    /// <returns>What to start.</returns>
    public static ShellLaunch LaunchFor(
        string terminal, string dockerCli, string containerId, string shell)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(terminal);
        ArgumentException.ThrowIfNullOrWhiteSpace(dockerCli);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(shell);

        return new ShellLaunch(terminal, [dockerCli, "exec", "-it", containerId, shell]);
    }
}

/// <summary>Which terminal to hand the command to.</summary>
public static class Terminals
{
    /// <summary>
    /// Where a per-user Windows Terminal install puts its launcher.
    /// </summary>
    /// <remarks>
    /// The full path rather than the bare name: <c>wt.exe</c> on PATH is an app-execution alias,
    /// which a user can turn off in Settings and which does not exist for a process started before
    /// the alias is registered. Naming the file is one less thing that can be true on the
    /// developer's machine and false on somebody else's.
    /// </remarks>
    public static string WindowsTerminal => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "WindowsApps", "wt.exe");

    /// <summary>The console host, which is on every Windows machine.</summary>
    public static string ConsoleHost => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "conhost.exe");

    /// <summary>
    /// Windows Terminal when it is there, the console host when it is not.
    /// </summary>
    /// <param name="exists">Whether a path is a file; replaced in tests.</param>
    /// <returns>The terminal to launch.</returns>
    public static string Choose(Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        return exists(WindowsTerminal) ? WindowsTerminal : ConsoleHost;
    }
}
