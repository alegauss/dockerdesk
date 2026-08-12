using System.Diagnostics;
using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;

namespace DockerDesk.Tray;

/// <summary>How a process is started. The seam the holder is testable through.</summary>
public interface IProcessLauncher
{
    /// <summary>Start <paramref name="fileName"/> with <paramref name="arguments"/>, detached.</summary>
    /// <param name="fileName">The executable.</param>
    /// <param name="arguments">Its command line.</param>
    void Launch(string fileName, string arguments);
}

/// <summary>
/// Starts the engine as a process that outlives this one.
/// </summary>
/// <remarks>
/// Quitting the tray must not stop the engine: a container running a database another process is
/// using cannot die because somebody closed an icon. And the engine only stays up while a Windows
/// process holds the <c>wsl.exe</c> children and the pipe relay — measured, since neither
/// <c>nohup</c> nor <c>setsid</c> survives inside WSL2. Both facts together leave one shape: the
/// tray launches a separate <c>--run</c> and does not own it.
///
/// A process and not a Windows service. The service is what the non-goal rules out, and it is also
/// what would put the engine back on every boot without being asked.
/// </remarks>
public sealed class EngineHolder(string enginePath, IProcessLauncher launcher)
{
    /// <summary>The executable that holds the engine.</summary>
    public const string EngineExecutable = "dockerdesk-engine.exe";

    /// <summary>Where the engine executable is expected: beside whatever is running.</summary>
    /// <returns>The path.</returns>
    public static string BesideThisProcess()
    {
        var here = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        return Path.Combine(here, EngineExecutable);
    }

    /// <summary>The engine executable this holder drives.</summary>
    public string EnginePath { get; } = enginePath;

    /// <summary>Start the engine, in a process this one does not own.</summary>
    public void Start() => launcher.Launch(EnginePath, "--run");

    /// <summary>
    /// Stop the engine.
    /// </summary>
    /// <remarks>
    /// Through the same executable rather than by killing a pid: <c>--stop</c> terminates the
    /// distribution, and whatever is holding the engine notices and comes down with it. That works
    /// whoever started it, including a run from a terminal before the tray existed.
    /// </remarks>
    public void Stop() => launcher.Launch(EnginePath, "--stop");
}

/// <summary>Launches through the shell, so the child inherits no console from this process.</summary>
public sealed class DetachedLauncher : IProcessLauncher
{
    /// <inheritdoc/>
    public void Launch(string fileName, string arguments)
    {
        // UseShellExecute, deliberately: started with it false the child inherits this process's
        // console, and a Ctrl+C meant for the tray would reach the engine too.
        using var started = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }
}

/// <summary>What the tray shows, derived from what the event stream is doing.</summary>
/// <remarks>
/// Nothing here polls the engine. The stream's own connection state is the indicator: it is
/// connected exactly when the engine is answering, which is the same definition the lifecycle uses,
/// arrived at without a timer.
/// </remarks>
public static class TrayState
{
    /// <summary>The engine state to show.</summary>
    /// <param name="stream">What the event loop is doing.</param>
    /// <param name="startRequested">Whether the user asked for a start that has not landed yet.</param>
    /// <returns>The state.</returns>
    public static EngineState For(EventStreamState stream, bool startRequested)
    {
        if (stream is EventStreamState.Watching)
        {
            return EngineState.Running;
        }

        // Connecting and Reconnecting both mean "not answering". Which of Stopped and Starting that
        // is depends on whether anybody asked for it to come up, and only the tray knows that.
        return startRequested ? EngineState.Starting : EngineState.Stopped;
    }
}
