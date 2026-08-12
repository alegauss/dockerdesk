using System.Text;
using DockerDesk.Core.Engine;
using DockerDesk.Core.Preflight;
using DockerDesk.Core.Preflight.Windows;

namespace DockerDesk.Engine.Cli;

/// <summary>
/// Puts the engine on this machine, unattended. Three modes, because two of the three phases
/// change nothing outside this tool's own directory and are worth being able to run alone.
/// </summary>
internal static class Program
{
    private const int Ok = 0;
    private const int Failed = 1;
    private const int Usage = 2;

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var mode = args.Length == 0 ? "--help" : args[0];

        // --autostart is the one mode that takes a value; everything else is a verb on its own.
        var allowed = mode == "--autostart" ? 2 : 1;
        if (args.Length > allowed)
        {
            return Complain($"unexpected argument {args[allowed]}");
        }

        return mode switch
        {
            "--plan" => Plan(),
            "--acquire" => Run(acquireOnly: true),
            "--provision" => Run(acquireOnly: false),
            "--status" => Status(),
            "--run" => RunEngine(),
            "--stop" => Stop(),
            "--autostart" => AutostartMode(args.Length > 1 ? args[1] : "status"),
            "-h" or "--help" => Help(Ok),
            _ => Complain($"unknown argument {mode}"),
        };
    }

    private static EngineLifecycle NewLifecycle() => new(
        new Wsl(), new WslDaemonProcess(), new WslSocatBackend());

    private static void Report(EngineStatus status)
    {
        Console.WriteLine($"  {status.State,-8}  {status.Detail}");
        if (status.ApiVersion is { } version)
        {
            Console.WriteLine($"  {"",-8}  Engine API {version}");
        }
    }

    private static int Status()
    {
        var status = NewLifecycle().StatusAsync().GetAwaiter().GetResult();
        Report(status);
        return status.Usable ? Ok : Failed;
    }

    /// <summary>
    /// Start the engine and stay in the foreground serving the pipe until interrupted.
    /// </summary>
    /// <remarks>
    /// Foreground on purpose. The relay has to outlive the start command — a Linux daemon cannot
    /// create the Windows pipe, so something here must hold it — and a resident background service
    /// is a stated non-goal. So the engine runs for exactly as long as somebody is running it, and
    /// Ctrl+C stops both halves.
    /// </remarks>
    private static int RunEngine()
    {
        using var stopping = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stopping.Cancel();
        };

        var lifecycle = NewLifecycle();
        try
        {
            var started = lifecycle.StartAsync(cancellation: stopping.Token)
                .GetAwaiter().GetResult();
            Report(started);
            if (!started.Usable)
            {
                return Failed;
            }

            Console.WriteLine();
            Console.WriteLine("Serving the engine. Ctrl+C stops it.");

            // Watch, rather than sleep forever. Two defects came from sleeping, both measured: a
            // `--stop` from another process left this one serving a pipe with nothing behind it, and
            // the wsl.exe children held here kept the distribution alive after it was terminated. So
            // the engine stopping by any means has to bring this down too.
            try
            {
                while (!stopping.IsCancellationRequested)
                {
                    Task.Delay(TimeSpan.FromSeconds(2), stopping.Token)
                        .GetAwaiter().GetResult();
                    var now = lifecycle.StatusAsync(stopping.Token).GetAwaiter().GetResult();
                    if (now.State is not EngineState.Running)
                    {
                        Console.WriteLine($"  {now.State,-8}  {now.Detail}");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine();
            }

            Report(lifecycle.StopAsync().GetAwaiter().GetResult());
            return Ok;
        }
        catch (OperationCanceledException)
        {
            Report(lifecycle.StopAsync().GetAwaiter().GetResult());
            return Ok;
        }
        finally
        {
            lifecycle.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static int Stop()
    {
        // Terminating the distribution kills the daemon, and whatever `--run` is serving the pipe
        // notices and comes down with it. So this works from any process, which a pid would not.
        var status = NewLifecycle().StopAsync().GetAwaiter().GetResult();
        Report(status);
        return Ok;
    }

    private static int AutostartMode(string mode)
    {
        var exe = Environment.ProcessPath
            ?? throw new InvalidOperationException("this process has no path");
        var autostart = new Autostart($"\"{exe}\" --run");

        switch (mode)
        {
            case "on":
                autostart.Enable();
                Console.WriteLine($"  autostart on   {autostart.Registered}");
                return Ok;
            case "off":
                autostart.Disable();
                Console.WriteLine("  autostart off  the registry entry is gone");
                return Ok;
            case "status":
                Console.WriteLine(autostart.Enabled
                    ? $"  autostart {(autostart.Current ? "on " : "stale")}  {autostart.Registered}"
                    : "  autostart off  nothing is registered");
                return Ok;
            default:
                return Complain($"--autostart takes on, off or status, not {mode}");
        }
    }

    /// <summary>What would be downloaded and where things would go. Reaches nothing.</summary>
    private static int Plan()
    {
        var manifest = EngineManifest.Current;
        var paths = new EnginePaths();

        Console.WriteLine("DockerDesk engine — pinned artefacts");
        Console.WriteLine();
        foreach (var artefact in manifest.Artefacts)
        {
            Console.WriteLine($"  {artefact.Id,-7} {artefact.Version,-9} {artefact.FileName}");
            Console.WriteLine($"  {"",-7} {"",-9} {artefact.Url}");
            Console.WriteLine($"  {"",-7} {"",-9} sha256 {artefact.Sha256}");
            Console.WriteLine();
        }

        Console.WriteLine($"  distribution   {EnginePaths.DistributionName}");
        Console.WriteLine($"  imported to    {paths.Distribution}");
        Console.WriteLine($"  downloads      {paths.Downloads}");
        Console.WriteLine($"  docker.exe     {paths.DockerCli}");
        Console.WriteLine();
        Console.WriteLine("  PATH is the installer's to change; this places the binary only.");
        return Ok;
    }

    private static int Run(bool acquireOnly)
    {
        // The preflight is the same code the installer runs, and running it here is the point: an
        // engine unpacked onto a machine that cannot host one fails halfway.
        var report = PreflightInspection.Run(new WindowsMachineFacts());
        if (!acquireOnly && !report.CanHostEngine)
        {
            Console.Error.WriteLine("preflight blocks this install:");
            foreach (var row in report.Blockers)
            {
                Console.Error.WriteLine($"  {row.Title}: {row.Detail}");
                Console.Error.WriteLine($"    -> {row.Remedy}");
            }

            return Failed;
        }

        var paths = new EnginePaths();
        using var fetcher = new HttpArtefactFetcher();
        var provisioner = new EngineProvisioner(
            EngineManifest.Current,
            new ArtefactStore(fetcher, paths.Downloads),
            new Wsl(),
            paths);

        var outcome = acquireOnly
            ? provisioner.AcquireAsync().GetAwaiter().GetResult()
            : provisioner.ProvisionAsync().GetAwaiter().GetResult();

        foreach (var step in outcome.Steps)
        {
            Console.WriteLine($"  [{(step.Ok ? "ok  " : "FAIL")}]  {step.Step,-19} {step.Detail}");
        }

        Console.WriteLine();
        if (outcome.Succeeded)
        {
            Console.WriteLine(acquireOnly
                ? "Every artefact is on disk and verified."
                : $"The engine is installed in {EnginePaths.DistributionName}.");
            return Ok;
        }

        Console.Error.WriteLine($"Stopped at {outcome.Failure!.Step}: {outcome.Failure.Detail}");
        return Failed;
    }

    private static int Complain(string problem)
    {
        Console.Error.WriteLine($"dockerdesk-engine: {problem}");
        return Help(Usage);
    }

    private static int Help(int code)
    {
        (code == Ok ? Console.Out : Console.Error).Write(
            """
            dockerdesk-engine — put upstream Moby into a WSL2 distribution this tool owns.

              --plan        the pinned versions, digests and paths; reaches nothing
              --acquire     download and verify every artefact, and stop
              --provision   acquire, import the distribution, install the engine, place docker.exe

              --run         start the engine and serve \\.\pipe\docker_engine until Ctrl+C
              --stop        stop the engine and terminate the distribution
              --status      what the engine is doing, by asking it
              --autostart   on | off | status  - off unless you turn it on

              --help        this

            Exit code 0 means the mode finished; 1 names the step it stopped at. For --status,
            1 means the engine is not answering.

            """);
        return code;
    }
}
