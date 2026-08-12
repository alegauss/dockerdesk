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
        if (args.Length > 1)
        {
            return Complain($"unexpected argument {args[1]}");
        }

        return mode switch
        {
            "--plan" => Plan(),
            "--acquire" => Run(acquireOnly: true),
            "--provision" => Run(acquireOnly: false),
            "-h" or "--help" => Help(Ok),
            _ => Complain($"unknown argument {mode}"),
        };
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
              --help        this

            Exit code 0 means the mode finished; 1 names the step it stopped at.

            """);
        return code;
    }
}
