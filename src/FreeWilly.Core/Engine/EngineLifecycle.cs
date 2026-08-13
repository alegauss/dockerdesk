using System.Diagnostics;

namespace FreeWilly.Core.Engine;

/// <summary>The daemon process, behind a seam so the lifecycle's decisions are testable.</summary>
public interface IDaemonProcess : IDisposable
{
    /// <summary>Whether it is running right now.</summary>
    bool Alive { get; }

    /// <summary>Start it. Returns once launched, which is long before the socket exists.</summary>
    void Launch();

    /// <summary>Stop it.</summary>
    void Stop();
}

/// <summary>
/// Starts and stops the engine, and reports a state that is never ahead of the truth.
/// </summary>
/// <remarks>
/// The order matters and each step's failure is named separately, because there are three ways this
/// goes wrong and they have three different remedies: the distribution is not provisioned, the
/// daemon starts and dies, or the daemon runs and nothing on Windows is serving the pipe.
/// </remarks>
public sealed class EngineLifecycle : IAsyncDisposable
{
    /// <summary>Where the daemon listens inside the distribution.</summary>
    public const string SocketPath = "/var/run/docker.sock";

    /// <summary>Where the daemon's own log is kept inside the distribution.</summary>
    public const string LogPath = "/var/log/dockerd.log";

    private readonly IWsl _wsl;
    private readonly IDaemonProcess _daemon;
    private readonly IEngineBackend _backend;
    private readonly string _pipeName;
    private EnginePipeRelay? _relay;

    /// <summary>Construct a lifecycle.</summary>
    /// <param name="wsl">The WSL command.</param>
    /// <param name="daemon">The daemon process.</param>
    /// <param name="backend">How the relay reaches the daemon.</param>
    /// <param name="pipeName">The pipe to serve; overridden in tests.</param>
    public EngineLifecycle(
        IWsl wsl,
        IDaemonProcess daemon,
        IEngineBackend backend,
        string pipeName = EnginePipeRelay.DefaultPipeName)
    {
        ArgumentNullException.ThrowIfNull(wsl);
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(backend);
        _wsl = wsl;
        _daemon = daemon;
        _backend = backend;
        _pipeName = pipeName;
    }

    /// <summary>Whether the distribution this tool owns is registered at all.</summary>
    public bool DistributionRegistered =>
        _wsl.Run("--list", "--quiet").Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.Trim().Equals(
                EnginePaths.DistributionName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Read the state, by asking the engine rather than by remembering what was asked.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The state and what was observed.</returns>
    public async Task<EngineStatus> StatusAsync(CancellationToken cancellation = default)
    {
        var ping = await EnginePing.AskAsync(_pipeName, cancellation: cancellation)
            .ConfigureAwait(false);
        if (ping.Answered)
        {
            return new EngineStatus(EngineState.Running,
                $"the engine answered on \\\\.\\pipe\\{_pipeName}", ping.ApiVersion);
        }

        if (!DistributionRegistered)
        {
            return new EngineStatus(EngineState.Stopped,
                $"{EnginePaths.DistributionName} is not registered — the engine is not installed");
        }

        return _daemon.Alive
            ? new EngineStatus(EngineState.Starting, $"the daemon is running and {ping.Detail}")
            : new EngineStatus(EngineState.Stopped, "the daemon is not running");
    }

    /// <summary>
    /// Boot the distribution, launch the daemon, serve the pipe, and wait until the engine answers.
    /// </summary>
    /// <param name="timeout">How long the engine has to answer before this gives up.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>Running, or the state it got stuck in with the step named.</returns>
    public async Task<EngineStatus> StartAsync(
        TimeSpan? timeout = null, CancellationToken cancellation = default)
    {
        var budget = timeout ?? TimeSpan.FromSeconds(60);

        if (!DistributionRegistered)
        {
            return new EngineStatus(EngineState.Stopped,
                $"{EnginePaths.DistributionName} is not registered — run the install first");
        }

        var already = await EnginePing.AskAsync(_pipeName, cancellation: cancellation)
            .ConfigureAwait(false);
        if (already.Answered)
        {
            return new EngineStatus(EngineState.Running,
                "the engine was already answering", already.ApiVersion);
        }

        if (!_daemon.Alive)
        {
            _daemon.Launch();
        }

        _relay ??= StartRelay();

        // Poll, because there is no event for "the socket is open now". The daemon dying is checked
        // on every turn: waiting the full minute for a process that is already gone reports a
        // timeout where the real answer is in its log.
        var deadline = DateTimeOffset.UtcNow + budget;
        var lastDetail = already.Detail;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellation.ThrowIfCancellationRequested();

            if (!_daemon.Alive)
            {
                return new EngineStatus(EngineState.Stopped,
                    $"the daemon exited while starting — read {LogPath} inside "
                    + EnginePaths.DistributionName);
            }

            var ping = await EnginePing.AskAsync(
                _pipeName, TimeSpan.FromSeconds(2), cancellation).ConfigureAwait(false);
            if (ping.Answered)
            {
                return new EngineStatus(EngineState.Running,
                    $"the engine answered on \\\\.\\pipe\\{_pipeName}", ping.ApiVersion);
            }

            lastDetail = ping.Detail;
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellation).ConfigureAwait(false);
        }

        return new EngineStatus(EngineState.Starting,
            $"the daemon is running and the pipe did not answer within {budget.TotalSeconds:0}s "
            + $"({lastDetail})");
    }

    /// <summary>
    /// Stop serving, stop the daemon, and terminate the distribution — an idle WSL2 virtual machine
    /// holds memory a laptop user notices, which is the complaint this project exists about.
    /// </summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>Stopped, and what was done.</returns>
    public async Task<EngineStatus> StopAsync(CancellationToken cancellation = default)
    {
        var done = new List<string>();

        if (_relay is not null)
        {
            await _relay.DisposeAsync().ConfigureAwait(false);
            _relay = null;
            done.Add("stopped serving the pipe");
        }

        if (_daemon.Alive)
        {
            _daemon.Stop();
            done.Add("stopped the daemon");
        }

        if (DistributionRegistered)
        {
            var terminated = _wsl.Run("--terminate", EnginePaths.DistributionName);
            done.Add(terminated.Succeeded
                ? $"terminated {EnginePaths.DistributionName}"
                : $"could not terminate {EnginePaths.DistributionName}: {terminated.Output.Trim()}");
        }

        _ = cancellation;
        return new EngineStatus(EngineState.Stopped,
            done.Count == 0 ? "nothing was running" : string.Join(", ", done));
    }

    private EnginePipeRelay StartRelay()
    {
        var relay = new EnginePipeRelay(_backend, _pipeName);
        relay.Start();
        return relay;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_relay is not null)
        {
            await _relay.DisposeAsync().ConfigureAwait(false);
        }

        _daemon.Dispose();
    }
}

/// <summary>
/// The daemon as a held Windows child process.
/// </summary>
/// <remarks>
/// Held, and not detached, because measured on a real distribution neither <c>nohup … &amp;</c> nor
/// <c>setsid</c> survives: WSL2 reaps the user processes shortly after the launching <c>wsl.exe</c>
/// exits, and the daemon's log came back zero bytes. A process on this side is what keeps it up —
/// which also means stopping the engine is killing something we own, rather than hunting a pid.
/// </remarks>
public sealed class WslDaemonProcess : IDaemonProcess
{
    private readonly string _distribution;
    private Process? _process;

    /// <summary>Construct a daemon launcher.</summary>
    /// <param name="distribution">The owned distribution.</param>
    public WslDaemonProcess(string distribution = EnginePaths.DistributionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(distribution);
        _distribution = distribution;
    }

    /// <inheritdoc/>
    public bool Alive => _process is { HasExited: false };

    /// <inheritdoc/>
    public void Launch()
    {
        var startInfo = new ProcessStartInfo(Wsl.LauncherPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
                 {
                     "-d", _distribution, "-u", "root", "--exec",
                     "/bin/sh", "-c",
                     $"exec /usr/local/bin/dockerd -H unix://{EngineLifecycle.SocketPath} "
                     + $">>{EngineLifecycle.LogPath} 2>&1",
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        _process = Process.Start(startInfo)
            ?? throw new IOException($"{Wsl.LauncherPath} could not be started");
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(10_000);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Already gone.
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
        _process?.Dispose();
        _process = null;
    }
}
