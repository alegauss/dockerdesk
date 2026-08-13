using System.IO.Compression;

namespace FreeWilly.Core.Engine;

/// <summary>The steps provisioning goes through, in order.</summary>
/// <remarks>
/// This runs from an installer where there is no terminal to answer a prompt in, so a failure has
/// only one way to be useful: name the step it happened at. That is what this enum is for.
/// </remarks>
public enum ProvisioningStep
{
    /// <summary>Download and verify the Linux root filesystem.</summary>
    AcquireRootfs,

    /// <summary>Download and verify the static Linux engine binaries.</summary>
    AcquireEngine,

    /// <summary>
    /// Read the engine tarball's member list and check the binaries it must carry are there.
    /// </summary>
    InspectEngine,

    /// <summary>Download and verify the archive holding the Windows CLI.</summary>
    AcquireCli,

    /// <summary>Import the owned WSL2 distribution from the root filesystem.</summary>
    ImportDistribution,

    /// <summary>Unpack the engine binaries inside the distribution and configure it.</summary>
    InstallEngine,

    /// <summary>Put <c>docker.exe</c> where an installer can add it to PATH.</summary>
    PlaceCli,
}

/// <summary>What one step did.</summary>
/// <param name="Step">Which step.</param>
/// <param name="Ok">Whether it succeeded.</param>
/// <param name="Detail">What happened, in one line — the path, the version, or the error.</param>
public sealed record StepResult(ProvisioningStep Step, bool Ok, string Detail);

/// <summary>Every step attempted, and whether the whole thing worked.</summary>
/// <param name="Steps">The steps, in the order they ran.</param>
public sealed record ProvisioningOutcome(IReadOnlyList<StepResult> Steps)
{
    /// <summary>Whether every attempted step succeeded and none was skipped by a failure.</summary>
    public bool Succeeded => Steps.Count > 0 && Steps.All(step => step.Ok);

    /// <summary>The step that failed, or <see langword="null"/>.</summary>
    public StepResult? Failure => Steps.FirstOrDefault(step => !step.Ok);
}

/// <summary>
/// Puts upstream Moby into a WSL2 distribution this tool owns, unattended.
/// </summary>
/// <remarks>
/// Stops at the first failing step rather than pressing on: every step after an import that did not
/// happen would fail for a reason that is not its own, and a report naming six failures where there
/// was one is a report nobody can act on.
/// </remarks>
public sealed class EngineProvisioner
{
    private readonly EngineManifest _manifest;
    private readonly ArtefactStore _store;
    private readonly IWsl _wsl;
    private readonly EnginePaths _paths;

    /// <summary>Construct a provisioner.</summary>
    /// <param name="manifest">The pinned artefacts.</param>
    /// <param name="store">Where artefacts are acquired and verified.</param>
    /// <param name="wsl">The WSL command.</param>
    /// <param name="paths">Where things are installed.</param>
    public EngineProvisioner(
        EngineManifest manifest, ArtefactStore store, IWsl wsl, EnginePaths paths)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(wsl);
        ArgumentNullException.ThrowIfNull(paths);
        _manifest = manifest;
        _store = store;
        _wsl = wsl;
        _paths = paths;
    }

    /// <summary>
    /// Download and verify every artefact, and stop. The half that needs no WSL2 and changes
    /// nothing outside this tool's own directory.
    /// </summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The three acquisition steps.</returns>
    public Task<ProvisioningOutcome> AcquireAsync(CancellationToken cancellation = default) =>
        RunAsync(installing: false, cancellation);

    /// <summary>Acquire, import the distribution, install the engine and place the CLI.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>Every step attempted.</returns>
    public Task<ProvisioningOutcome> ProvisionAsync(CancellationToken cancellation = default) =>
        RunAsync(installing: true, cancellation);

    private async Task<ProvisioningOutcome> RunAsync(
        bool installing, CancellationToken cancellation)
    {
        var steps = new List<StepResult>();
        _paths.Create();

        var rootfs = await Acquire(
            steps, ProvisioningStep.AcquireRootfs, _manifest.Rootfs, cancellation)
            .ConfigureAwait(false);
        if (rootfs is null)
        {
            return new ProvisioningOutcome(steps);
        }

        var engine = await Acquire(
            steps, ProvisioningStep.AcquireEngine, _manifest.Engine, cancellation)
            .ConfigureAwait(false);
        if (engine is null)
        {
            return new ProvisioningOutcome(steps);
        }

        if (!Record(steps, InspectEngine(engine)))
        {
            return new ProvisioningOutcome(steps);
        }

        var cli = await Acquire(steps, ProvisioningStep.AcquireCli, _manifest.Cli, cancellation)
            .ConfigureAwait(false);
        if (cli is null || !installing)
        {
            return new ProvisioningOutcome(steps);
        }

        if (!Record(steps, ImportDistribution(rootfs)))
        {
            return new ProvisioningOutcome(steps);
        }

        if (!Record(steps, InstallEngine(engine)))
        {
            return new ProvisioningOutcome(steps);
        }

        Record(steps, PlaceCli(cli));
        return new ProvisioningOutcome(steps);
    }

    /// <summary>Append a step and say whether the run continues.</summary>
    private static bool Record(List<StepResult> steps, StepResult result)
    {
        steps.Add(result);
        return result.Ok;
    }

    private async Task<string?> Acquire(
        List<StepResult> steps,
        ProvisioningStep step,
        Artefact artefact,
        CancellationToken cancellation)
    {
        var acquired = await _store.AcquireAsync(artefact, cancellation).ConfigureAwait(false);
        if (!acquired.Verified)
        {
            steps.Add(new StepResult(step, false, acquired.Failure ?? "no file and no reason"));
            return null;
        }

        var how = acquired.Cached ? "already verified on disk" : "downloaded and verified";
        steps.Add(new StepResult(
            step, true, $"{artefact.Id} {artefact.Version}, {how}: {acquired.Path}"));
        return acquired.Path;
    }

    /// <summary>The arguments that import the owned distribution. Non-interactive by construction.</summary>
    /// <param name="rootfsPath">The verified root filesystem tarball.</param>
    /// <returns>The argument list handed to wsl.exe.</returns>
    public string[] ImportArguments(string rootfsPath) =>
    [
        "--import",
        EnginePaths.DistributionName,
        _paths.Distribution,
        rootfsPath,
        "--version",
        "2",
    ];

    /// <summary>The shell script run inside the distribution to install the engine.</summary>
    /// <param name="engineTarballPath">The verified engine tarball, as a Windows path.</param>
    /// <returns>The script.</returns>
    public static string InstallScript(string engineTarballPath)
    {
        var inside = Wsl.ToDistributionPath(engineTarballPath);

        // One script, every command non-interactive, and `set -e` so it stops where it broke rather
        // than reporting the last command's status. dockerd needs iptables, which a minimal root
        // filesystem has no copy of, so apk fetches it — which is also why wsl.conf leaves
        // generateResolvConf on: WSL's own DNS is what makes that fetch resolve. systemd stays off
        // because nothing here is a service yet; starting the engine is its own task.
        return string.Join(" && ",
        [
            "set -e",
            // socat is what carries the Engine API out to Windows: a Linux daemon cannot create a
            // Windows named pipe, and every IP route to it is reachable by any local process, so the
            // hop is over wsl.exe's stdio and needs a tool that speaks unix sockets. BusyBox's nc
            // does not — measured: its usage line offers HOST PORT and no -U.
            "apk add --no-cache --no-progress iptables ip6tables ca-certificates socat",
            "mkdir -p /usr/local/bin",
            // --no-same-owner, because tar as root otherwise restores the uid the archive was built
            // with: measured on a real install, every binary landed owned by 1001:1001, a user this
            // distribution does not have. Harmless today and a trap the first time one exists.
            $"tar -xzf '{inside}' -C /usr/local/bin --strip-components=1 --no-same-owner",
            // A glob, not eight names: naming them makes `set -e` abort the whole install the day
            // upstream renames or drops one, and which binaries have to be there is already decided
            // locally by InspectEngine, before the distribution is touched.
            "chmod 0755 /usr/local/bin/*",
            "printf '[boot]\\nsystemd=false\\n[network]\\ngenerateResolvConf=true\\n' > /etc/wsl.conf",
            "/usr/local/bin/dockerd --version",
        ]);
    }

    private StepResult InspectEngine(string engineTarballPath)
    {
        try
        {
            var contents = EngineArchive.Read(engineTarballPath);
            if (contents.TopDirectory is null)
            {
                return new StepResult(ProvisioningStep.InspectEngine, false,
                    "the tarball's entries do not share one top directory, so unpacking it with "
                    + "--strip-components=1 would scatter them");
            }

            var missing = EngineArchive.Missing(contents);
            if (missing.Count > 0)
            {
                return new StepResult(ProvisioningStep.InspectEngine, false,
                    $"{_manifest.Engine.FileName} is missing {string.Join(", ", missing)} — "
                    + $"it carries {contents.Binaries.Count} file(s) under {contents.TopDirectory}/");
            }

            return new StepResult(ProvisioningStep.InspectEngine, true,
                $"{contents.Binaries.Count} binaries under {contents.TopDirectory}/, "
                + $"including {string.Join(", ", EngineArchive.RequiredBinaries)}");
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException or UnauthorizedAccessException)
        {
            return new StepResult(ProvisioningStep.InspectEngine, false,
                $"reading {_manifest.Engine.FileName} failed: {exception.Message}");
        }
    }

    private StepResult ImportDistribution(string rootfsPath)
    {
        if (DistributionExists())
        {
            return new StepResult(ProvisioningStep.ImportDistribution, true,
                $"{EnginePaths.DistributionName} is already registered, left as it is");
        }

        var result = _wsl.Run(ImportArguments(rootfsPath));
        return result.Succeeded
            ? new StepResult(ProvisioningStep.ImportDistribution, true,
                $"{EnginePaths.DistributionName} imported into {_paths.Distribution}")
            : new StepResult(ProvisioningStep.ImportDistribution, false,
                Explain($"importing {EnginePaths.DistributionName}", result));
    }

    private StepResult InstallEngine(string engineTarballPath)
    {
        var result = _wsl.Run(
            "-d", EnginePaths.DistributionName, "-u", "root", "--",
            "/bin/sh", "-c", InstallScript(engineTarballPath));

        return result.Succeeded
            ? new StepResult(ProvisioningStep.InstallEngine, true,
                $"engine {_manifest.Engine.Version} unpacked into "
                + $"{EnginePaths.DistributionName}:/usr/local/bin")
            : new StepResult(ProvisioningStep.InstallEngine, false,
                Explain("installing the engine binaries", result));
    }

    private StepResult PlaceCli(string cliZipPath)
    {
        const string entryName = "docker/docker.exe";
        try
        {
            using var archive = ZipFile.OpenRead(cliZipPath);
            var entry = archive.GetEntry(entryName)
                ?? throw new InvalidOperationException(
                    $"{Path.GetFileName(cliZipPath)} has no {entryName}");

            Directory.CreateDirectory(_paths.CliDirectory);
            entry.ExtractToFile(_paths.DockerCli, overwrite: true);

            return new StepResult(ProvisioningStep.PlaceCli, true,
                $"docker {_manifest.Cli.Version} at {_paths.DockerCli}");
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            return new StepResult(ProvisioningStep.PlaceCli, false,
                $"placing the Windows CLI failed: {exception.Message}");
        }
    }

    private bool DistributionExists()
    {
        var listed = _wsl.Run("--list", "--quiet");
        if (!listed.Succeeded)
        {
            return false;
        }

        return listed.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.Trim().Equals(
                EnginePaths.DistributionName, StringComparison.OrdinalIgnoreCase));
    }

    private static string Explain(string what, WslResult result)
    {
        if (result.Failure is not null)
        {
            return $"{what} failed: {result.Failure}";
        }

        var said = result.Output.Trim();
        var because = said.Length == 0 ? "and said nothing" : $"saying: {Shorten(said)}";
        return $"{what} exited {result.ExitCode} {because}";
    }

    private static string Shorten(string text)
    {
        var oneLine = string.Join(" ", text.Split(
            ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return oneLine.Length <= 300 ? oneLine : oneLine[..300] + "…";
    }
}
