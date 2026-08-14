using System.Diagnostics;
using System.Text;
using FreeWilly.Core.Engine;

namespace FreeWilly.Core.Agent;

/// <summary>
/// The <c>docker</c> this install placed, run in the caller's own directory (DD63).
/// </summary>
/// <remarks>
/// <see cref="EnginePaths.DockerCli"/> and not whatever <c>docker</c> is on <c>PATH</c>. The
/// difference is the whole of DD20: a machine can carry a rival's CLI and a context pointing at its
/// engine, and composing through that would bring the project up somewhere else entirely — on an
/// engine this tool does not manage, with a stamp whose reclaim would then find nothing. The
/// executable beside our own install is the one that talks to our own pipe.
///
/// <para>The working directory is the caller's, because compose resolves a relative build context, a
/// bind mount and an <c>env_file</c> against the project — and the project is where the user ran
/// this, not where this executable happens to live.</para>
///
/// <para>Not <c>ConsoleTool</c>: that one is a preflight probe with a fifteen-second deadline, and
/// pulling an image is minutes. The deadline here is generous and still finite, so a build waiting
/// on a prompt nobody can answer ends as a refusal rather than as an agent that never returns.</para>
/// </remarks>
public sealed class BundledComposeCli : IComposeCli
{
    /// <summary>How long an up may take before this gives up on it.</summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromMinutes(10);

    private readonly string _docker;

    /// <summary>Construct against this install's own CLI.</summary>
    public BundledComposeCli()
        : this(new EnginePaths().DockerCli)
    {
    }

    /// <summary>Construct against an explicit executable.</summary>
    /// <param name="dockerCli">The <c>docker.exe</c> to run.</param>
    public BundledComposeCli(string dockerCli)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dockerCli);
        _docker = dockerCli;
    }

    /// <inheritdoc/>
    public ComposeResult Run(string workingDirectory, params string[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

        if (!File.Exists(_docker))
        {
            // Named rather than thrown: an install that never provisioned has no CLI, and that is a
            // sentence the caller can act on rather than a stack trace.
            return new ComposeResult(null, "", $"{_docker} is not there — run the install first");
        }

        var startInfo = new ProcessStartInfo(_docker)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ComposeResult(null, "", $"{_docker} could not be started");
            }

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)Deadline.TotalMilliseconds))
            {
                Kill(process);
                return new ComposeResult(
                    null, "", $"docker did not finish within {Deadline.TotalMinutes:0} minutes");
            }

            Task.WaitAll(stdout, stderr);

            // Compose writes its progress to stderr and its answers to stdout, so both are the
            // output: a failure whose reason was only on stderr would come back as an exit code and
            // no words at all.
            var text = new StringBuilder(stdout.Result).Append(stderr.Result).ToString();
            return new ComposeResult(process.ExitCode, text, null);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new ComposeResult(null, "", $"{_docker}: {exception.Message}");
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // It exited between the wait and the kill.
        }
    }
}
