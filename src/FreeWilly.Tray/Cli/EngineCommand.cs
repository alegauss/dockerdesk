using FreeWilly.Core.Api;
using FreeWilly.Core.Engine;
using FreeWilly.Core.Preflight;
using FreeWilly.Core.Preflight.Windows;

namespace FreeWilly.Tray.Cli;

/// <summary>
/// Puts the engine on this machine, unattended. Three modes, because two of the three phases
/// change nothing outside this tool's own directory and are worth being able to run alone.
/// </summary>
internal static class EngineCommand
{
    private const int Ok = 0;
    private const int Failed = 1;
    private const int Usage = 2;

    /// <summary>Run an engine verb.</summary>
    /// <param name="args">The verb and, for <c>--autostart</c>, its value.</param>
    /// <returns>The process exit code.</returns>
    internal static int Run(string[] args)
    {
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
            "--acquire" => Provision(acquireOnly: true),
            "--provision" => Provision(acquireOnly: false),
            "--status" => Status(),
            "--api" => ApiProbe(),
            "--watch" => Watch(),
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
        // One engine host per session (DD133). Nothing held this before, and a second --run was not
        // refused so much as ignored: it found the pipe already answering, started neither a daemon
        // nor a relay, and settled into a poll loop whose only power was to terminate the
        // distribution the first one was serving. Two clicks of Start engine bought that.
        if (!SingleEngine.TryClaim(out var only))
        {
            // Not a failure. The caller wanted the engine served and it is being served, which is
            // the same reason a second tray launch exits zero.
            Console.Error.WriteLine(
                $"{CommandLine.ExecutableName}: another FreeWilly on this session is already "
                + @"serving \\.\pipe\" + EnginePipeRelay.DefaultPipeName + ".");
            return Ok;
        }

        using (only)
        {
            return Serve(only!);
        }
    }

    /// <summary>The engine host proper, once this process is the one that holds the slot.</summary>
    /// <param name="only">
    /// The claim, which since DD136 also carries the one thing <c>--stop</c> has to say out loud.
    /// </param>
    private static int Serve(SingleEngine only)
    {
        using var stopping = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stopping.Cancel();
        };

        // An asked-for stop is the one ending this host must not argue with (DD136). Without it,
        // `--stop` terminating the distribution from another process is indistinguishable in here
        // from WSL2 dying under a suspend — and the whole point of what follows is that the second
        // one gets the engine back.
        var asked = new CancellationTokenSource();
        only.OnStop(asked.Cancel);

        // A resume is not itself a failure, so this only shortens the wait for one. The virtual
        // machine is often gone when the machine comes back while the wsl.exe handle on this side
        // is still perfectly alive, which reads as a healthy daemon that has stopped answering —
        // true, and slow to act on. Setting this makes the next turn of the loop reconcile instead
        // of counting to six first.
        var resumed = new ManualResetEventSlim(false);
        void OnPower(object? _, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            if (e.Mode is Microsoft.Win32.PowerModes.Resume)
            {
                resumed.Set();
            }
        }

        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPower;

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

            return Supervise(lifecycle, stopping, asked, resumed);
        }
        catch (OperationCanceledException)
        {
            Report(lifecycle.StopAsync().GetAwaiter().GetResult());
            return Ok;
        }
        finally
        {
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPower;
            resumed.Dispose();
            asked.Dispose();
            lifecycle.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Keep the engine up until somebody asks for it to stop, or it proves it cannot be (DD136).
    /// </summary>
    /// <param name="lifecycle">The engine.</param>
    /// <param name="stopping">Ctrl+C.</param>
    /// <param name="asked">A <c>--stop</c> that announced itself.</param>
    /// <param name="resumed">Set when Windows says the machine came back.</param>
    /// <returns>The exit code.</returns>
    /// <remarks>
    /// What this replaced watched for the engine going away and came down with it, which was right
    /// while nothing was allowed to put it back. It is the wrong shape for a laptop: WSL2 does not
    /// survive every suspend, and a host that is awake, polling, and watching the daemon disappear
    /// is the one thing on the machine in a position to do something about it.
    ///
    /// <para><b>Every ending is still reachable.</b> Ctrl+C and an announced <c>--stop</c> come down
    /// at once and restart nothing, because both are somebody saying what they want. Running out of
    /// attempts comes down too, and says so — an engine that cannot start is a fact the user needs,
    /// and a loop that hides it behind another retry is worse than the failure.</para>
    /// </remarks>
    private static int Supervise(
        EngineLifecycle lifecycle,
        CancellationTokenSource stopping,
        CancellationTokenSource asked,
        ManualResetEventSlim resumed)
    {
        var watch = new EngineWatch();
        var revival = new EngineRevival();

        using var ending = CancellationTokenSource.CreateLinkedTokenSource(
            stopping.Token, asked.Token);

        try
        {
            while (!ending.IsCancellationRequested)
            {
                Task.Delay(TimeSpan.FromSeconds(2), ending.Token).GetAwaiter().GetResult();
                var now = lifecycle.StatusAsync(ending.Token).GetAwaiter().GetResult();

                // A resume only skips the waiting. It never decides on its own that the engine is
                // gone — the status still has to say so — because a machine that came back with a
                // perfectly good daemon must not have it restarted for waking up.
                var justResumed = resumed.IsSet;
                if (justResumed)
                {
                    resumed.Reset();
                }

                if (watch.KeepServing(now) && !(justResumed && !now.Usable))
                {
                    continue;
                }

                Console.WriteLine($"  {watch.WhyItStopped(now)}");
                if (!Revive(lifecycle, revival, ending.Token))
                {
                    Console.WriteLine($"  {revival.WhyItGaveUp(now)}");
                    break;
                }

                // Back to a clean slate: the engine answered, so the run of silence that led here
                // describes an engine that no longer exists.
                watch = new EngineWatch();
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
        }

        Report(lifecycle.StopAsync().GetAwaiter().GetResult());
        return Ok;
    }

    /// <summary>Try to get the engine back, backing off between attempts (DD136).</summary>
    /// <param name="lifecycle">The engine.</param>
    /// <param name="revival">How many attempts are left and how long to wait.</param>
    /// <param name="ending">Cancelled by Ctrl+C or an announced stop.</param>
    /// <returns><see langword="true"/> where it came back.</returns>
    /// <remarks>
    /// The stop before the start is not tidiness. Whatever is left of the previous engine is holding
    /// the pipe name and a <c>wsl.exe</c> child that may still be alive, and a relay serving a
    /// socket inside a virtual machine that no longer exists is exactly the state being recovered
    /// from — starting on top of it would leave two of them.
    /// </remarks>
    private static bool Revive(
        EngineLifecycle lifecycle, EngineRevival revival, CancellationToken ending)
    {
        while (revival.WorthAnotherTry)
        {
            Task.Delay(revival.Wait, ending).GetAwaiter().GetResult();

            lifecycle.StopAsync(ending).GetAwaiter().GetResult();
            var back = lifecycle.StartAsync(cancellation: ending).GetAwaiter().GetResult();
            if (back.Usable)
            {
                revival.Revived();
                Console.WriteLine(
                    $"  {back.State,-8}  brought the engine back (restart {revival.Revivals})");
                return true;
            }

            revival.Failed();
            Console.WriteLine($"  {back.State,-8}  {back.Detail}");
        }

        return false;
    }

    /// <summary>
    /// Ask the engine everything the client can ask, through the Engine API rather than through
    /// docker.exe. This is what proves the client against a real daemon.
    /// </summary>
    private static int ApiProbe()
    {
        using var api = new DockerApi();
        if (!api.PingAsync().GetAwaiter().GetResult())
        {
            Console.Error.WriteLine(
                @"  the engine is not answering on \\.\pipe\" + DockerApi.DefaultPipeName);
            return Failed;
        }

        try
        {
            var version = api.VersionAsync().GetAwaiter().GetResult();
            Console.WriteLine(
                $"  engine {version.Version}, API {version.ApiVersion} "
                + $"(oldest {version.MinApiVersion}), {version.Os}/{version.Arch}");
            Console.WriteLine($"  this client asks for {DockerApi.ApiVersion}");
            Console.WriteLine();

            var containers = api.ContainersAsync().GetAwaiter().GetResult();
            if (containers.Count == 0)
            {
                Console.WriteLine("  no containers");
                return Ok;
            }

            foreach (var container in containers)
            {
                var ports = container.PublishedPorts.Count == 0
                    ? ""
                    : "  " + string.Join(", ", container.PublishedPorts);
                Console.WriteLine(
                    $"  {container.ShortId}  {container.State,-8}  {container.Image,-22}  "
                    + $"{container.DisplayName}{ports}");
            }

            return Ok;
        }
        catch (DockerApiException exception)
        {
            Console.Error.WriteLine($"  {exception.Message}");
            return Failed;
        }
    }

    /// <summary>
    /// Read /events until Ctrl+C, printing each one. What proves the watcher against a real daemon,
    /// including the part that matters most: the engine going away and coming back.
    /// </summary>
    private static int Watch()
    {
        using var stopping = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

        using var api = new DockerApi();
        var events = new EngineEvents(new DockerApiEventSource(api));
        events.StateChanged += state => Console.WriteLine($"  [{state}]");
        events.Received += e => Console.WriteLine(
            $"  {e.Type,-9} {e.Action,-28} {e.ShortId,-12} {e.Name}"
            + (e.ChangesTheContainerList ? "  (refresh)" : ""));
        events.Start();

        try
        {
            Task.Delay(Timeout.InfiniteTimeSpan, stopping.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C is how this ends.
        }

        Console.WriteLine(
            $"  {events.Reconnects} reconnect(s), {events.Unreadable} unreadable line(s)");
        events.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return Ok;
    }

    private static int Stop()
    {
        // Announced before it is done (DD136). Terminating the distribution kills the daemon and
        // whatever `--run` is serving the pipe notices — which was the whole mechanism, and worked
        // from any process where a pid would not. What changed is that the host now puts back an
        // engine it loses, and from in there this teardown is indistinguishable from WSL2 dying
        // under a suspend. So the one thing it cannot infer is said out loud, and said first, or
        // the host starts reviving the engine this is in the middle of taking down.
        _ = SingleEngine.TellTheLiveOneToStop();

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

                // The tray's entry is a different setting under a different name (DD97), and it is
                // named here because the split would otherwise read as a bug: somebody who ticked
                // "Start FreeWilly with Windows" in the installer asks this verb, is told off, and
                // concludes the box did nothing. This says what is true — the tray starts, the
                // engine does not — and it is the only place both facts are visible at once.
                var tray = new Autostart(exe, Autostart.TrayEntryName);
                if (tray.Registered is { } atLogon)
                {
                    Console.WriteLine($"  tray      on   {atLogon}");
                }

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

        Console.WriteLine("FreeWilly engine — pinned artefacts");
        Console.WriteLine();
        foreach (var artefact in manifest.Artefacts)
        {
            Console.WriteLine($"  {artefact.Id,-7} {artefact.Version,-9} {artefact.FileName}");
            Console.WriteLine($"  {"",-7} {"",-9} {artefact.Url}");
            Console.WriteLine($"  {"",-7} {"",-9} sha256 {artefact.Sha256}");
            Console.WriteLine();
        }

        Console.WriteLine($"  distribution   {paths.DistributionName}");
        Console.WriteLine($"  imported to    {paths.Distribution}");
        Console.WriteLine($"  downloads      {paths.Downloads}");
        Console.WriteLine($"  docker.exe     {paths.DockerCli}");
        Console.WriteLine($"  cli-plugins    {paths.PluginsDirectory}");
        Console.WriteLine();
        Console.WriteLine("  PATH is the installer's to change; this places the binary only.");

        // The plugin is placed under this install's own config directory, so `do compose up` finds
        // it and a plain shell does not (DD73). The variable that closes that gap is the user's
        // own environment, which is theirs to set — so the command is printed and never run.
        Console.WriteLine();
        Console.WriteLine("  `docker compose` in your own shell needs the config directory too:");
        Console.WriteLine(
            $"    setx {Core.Agent.BundledComposeCli.ConfigVariable} \"{paths.ConfigDirectory}\"");
        Console.WriteLine("  Not set for you: it is your environment, and it changes every docker");
        Console.WriteLine("  command in it. `freewilly do compose up` needs none of this.");

        // DD76. A developer whose shell is their own WSL2 distribution finds nothing: the daemon's
        // socket is inside the distribution this tool owns, and a Linux client cannot dial the
        // Windows pipe that carries it out. Docker Desktop answers that by writing a CLI and a
        // socket into each distribution the user ticks, which is the largest version of the thing
        // this project has refused to do since DD32 — and it hands the Engine API to every
        // distribution, which is what the pipe's single-account ACL exists to prevent.
        //
        // So the integration is not built and the fact is stated instead, here and in the README,
        // with the one thing that does work. Measured: WSL interop is on by default, and the
        // Windows docker.exe invoked from a Linux shell reaches the engine.
        Console.WriteLine();
        Console.WriteLine("  From a WSL shell of your own, the Linux `docker` reaches nothing —");
        Console.WriteLine("  the engine is on the Windows side of a named pipe. Run the Windows");
        Console.WriteLine("  binary instead, which WSL's interop makes work:");
        Console.WriteLine($"    {Wsl.ToDistributionPath(paths.DockerCli)} ps");
        Console.WriteLine("  It is a Windows process, so every path you hand it is read as a");
        Console.WriteLine("  Windows path: `-v $(pwd):/data` in a Linux shell mounts nothing.");
        return Ok;
    }

    private static int Provision(bool acquireOnly)
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

        // Printed as each step lands rather than gathered and printed at the end (DD119). The lines
        // are identical either way; what changes is that the installer's page, which reads this
        // stream a line at a time, has something on it while a quarter of a gigabyte comes down.
        // Console.Out flushes on every write even when redirected, so no line waits on a buffer.
        var outcome = acquireOnly
            ? provisioner.AcquireAsync(Say).GetAwaiter().GetResult()
            : provisioner.ProvisionAsync(Say).GetAwaiter().GetResult();

        Console.WriteLine();
        if (outcome.Succeeded)
        {
            Console.WriteLine(acquireOnly
                ? "Every artefact is on disk and verified."
                : $"The engine is installed in {paths.DistributionName}.");
            return Ok;
        }

        Console.Error.WriteLine($"Stopped at {outcome.Failure!.Step}: {outcome.Failure.Detail}");
        return Failed;
    }

    /// <summary>Print one provisioning step, the moment it lands.</summary>
    private static void Say(StepResult step) => Console.WriteLine(StepLine(step));

    /// <summary>
    /// How one provisioning step is written, and the whole of what the installer parses (DD119).
    /// </summary>
    /// <param name="step">The step that just landed.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// A method rather than an interpolation inside the loop, because installer.iss counts these
    /// lines to move its progress bar and matches the two verdicts at position 1 of the trimmed
    /// line. That makes the leading marker a contract between a C# format string and a Pascal
    /// <c>Pos</c> call, which nothing but a test would notice drifting — so there is one, and it
    /// asserts against this.
    /// </remarks>
    internal static string StepLine(StepResult step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return $"  [{(step.Ok ? "ok  " : "FAIL")}]  {step.Step,-19} {step.Detail}";
    }

    private static int Complain(string problem)
    {
        Console.Error.WriteLine($"{CommandLine.ExecutableName}: {problem}");
        return Help(Usage);
    }

    /// <summary>
    /// The one help text, and deliberately not a second one.
    /// </summary>
    /// <remarks>
    /// This used to print its own list of engine modes, which was correct while the engine was its
    /// own executable and became a duplicate the moment it stopped being one. A verb documented in
    /// one of two lists is a verb somebody cannot find.
    /// </remarks>
    private static int Help(int code)
    {
        (code == Ok ? Console.Out : Console.Error).Write(CommandLine.HelpText);
        return code;
    }
}
