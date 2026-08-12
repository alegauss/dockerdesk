using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DockerDesk.Core.Engine;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// The manifest, the digest gate, and the invocations provisioning builds. The digest gate is the
/// one that matters: it is the only check that stops a mirror, a proxy or a truncated download from
/// being unpacked, so it is exercised failing before it is trusted passing.
/// </summary>
public sealed class EngineProvisioningTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("dockerdesk-test").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static string Sha256Of(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static Artefact Pinned(string content, string id = "thing") =>
        new(id, "1.2.3", $"https://example.invalid/{id}.bin", $"{id}.bin", Sha256Of(content));

    // ---- the manifest ------------------------------------------------------------------------

    [Fact]
    public void The_manifest_is_embedded_and_pins_three_artefacts()
    {
        var manifest = EngineManifest.Current;

        Assert.Equal(3, manifest.Artefacts.Count);
        Assert.Equal(["rootfs", "engine", "cli"], manifest.Artefacts.Select(a => a.Id));
    }

    [Fact]
    public void Every_pinned_artefact_carries_a_version_an_https_url_and_a_sha256()
    {
        Assert.All(EngineManifest.Current.Artefacts, artefact =>
        {
            Assert.False(string.IsNullOrWhiteSpace(artefact.Version));
            Assert.StartsWith("https://", artefact.Url, StringComparison.Ordinal);
            Assert.Equal(64, artefact.Sha256.Length);
            Assert.True(
                artefact.Sha256.All(char.IsAsciiHexDigitLower),
                $"{artefact.Id} digest is not lower-case hex: {artefact.Sha256}");
            Assert.DoesNotContain("latest", artefact.Url, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void The_engine_and_the_cli_are_pinned_to_the_same_version()
    {
        // A dockerd and a docker CLI from different releases is a combination nobody upstream tests.
        var manifest = EngineManifest.Current;

        Assert.Equal(manifest.Engine.Version, manifest.Cli.Version);
    }

    // ---- the digest gate --------------------------------------------------------------------

    [Fact]
    public async Task An_artefact_whose_digest_matches_is_verified()
    {
        var store = new ArtefactStore(FakeFetcher.Writing("engine bytes"), _temp);

        var acquired = await store.AcquireAsync(Pinned("engine bytes"));

        Assert.True(acquired.Verified);
        Assert.False(acquired.Cached);
        Assert.True(File.Exists(acquired.Path));
    }

    [Fact]
    public async Task An_artefact_whose_digest_is_wrong_is_refused_and_deleted()
    {
        // The injected failure: the bytes arrive, and they are not the bytes this build pins.
        var artefact = Pinned("what we pinned");
        var store = new ArtefactStore(FakeFetcher.Writing("what actually arrived"), _temp);

        var acquired = await store.AcquireAsync(artefact);

        Assert.False(acquired.Verified);
        Assert.Null(acquired.Path);
        Assert.Contains(artefact.Sha256, acquired.Failure!, StringComparison.Ordinal);
        Assert.Contains(Sha256Of("what actually arrived"), acquired.Failure!, StringComparison.Ordinal);
        Assert.False(
            File.Exists(Path.Combine(_temp, artefact.FileName)),
            "a file that failed its digest must not be left where a retry can find it");
    }

    [Fact]
    public async Task A_download_that_failed_is_reported_as_a_download_and_not_as_a_digest()
    {
        var store = new ArtefactStore(new FakeFetcher(_ => null), _temp);

        var acquired = await store.AcquireAsync(Pinned("never arrives"));

        Assert.False(acquired.Verified);
        Assert.Contains("downloading", acquired.Failure!, StringComparison.Ordinal);
        Assert.DoesNotContain("pins", acquired.Failure!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_verified_file_already_on_disk_is_not_downloaded_again()
    {
        var artefact = Pinned("cached bytes");
        await File.WriteAllTextAsync(Path.Combine(_temp, artefact.FileName), "cached bytes");
        var fetcher = FakeFetcher.Writing("cached bytes");

        var acquired = await new ArtefactStore(fetcher, _temp).AcquireAsync(artefact);

        Assert.True(acquired.Verified);
        Assert.True(acquired.Cached);
        Assert.Empty(fetcher.Requested);
    }

    [Fact]
    public async Task A_corrupt_file_on_disk_is_replaced_rather_than_trusted_or_refused()
    {
        // A half-written file from an interrupted run is the common case, and re-fetching is the
        // answer to it. Trusting it would install a truncated engine.
        var artefact = Pinned("good bytes");
        await File.WriteAllTextAsync(Path.Combine(_temp, artefact.FileName), "truncat");
        var fetcher = FakeFetcher.Writing("good bytes");

        var acquired = await new ArtefactStore(fetcher, _temp).AcquireAsync(artefact);

        Assert.True(acquired.Verified);
        Assert.False(acquired.Cached);
        Assert.Single(fetcher.Requested);
    }

    // ---- path translation -------------------------------------------------------------------

    [Theory]
    [InlineData(@"C:\Users\x\docker.tgz", "/mnt/c/Users/x/docker.tgz")]
    [InlineData(@"D:\a\b\c.tar.gz", "/mnt/d/a/b/c.tar.gz")]
    public void A_windows_path_is_translated_to_the_automatic_drive_mount(
        string windows, string expected) =>
        Assert.Equal(expected, Wsl.ToDistributionPath(windows));

    [Fact]
    public void A_unc_path_has_no_drive_mount_and_is_refused() =>
        Assert.Throws<ArgumentException>(
            () => Wsl.ToDistributionPath(@"\\server\share\docker.tgz"));

    // ---- the invocations --------------------------------------------------------------------

    [Fact]
    public void The_import_names_the_owned_distribution_and_pins_wsl_version_2()
    {
        var provisioner = Provisioner(new FakeWsl(), out var paths);

        var argv = provisioner.ImportArguments(@"C:\downloads\rootfs.tar.gz");

        Assert.Equal(
            ["--import", "dockerdesk", paths.Distribution, @"C:\downloads\rootfs.tar.gz",
             "--version", "2"],
            argv);
    }

    [Fact]
    public void The_install_script_is_non_interactive_and_stops_at_the_first_error()
    {
        var script = EngineProvisioner.InstallScript(@"C:\downloads\docker.tgz");

        Assert.StartsWith("set -e", script, StringComparison.Ordinal);
        Assert.Contains("--no-cache", script, StringComparison.Ordinal);
        Assert.Contains("iptables", script, StringComparison.Ordinal);
        Assert.Contains("/mnt/c/downloads/docker.tgz", script, StringComparison.Ordinal);
        Assert.Contains("dockerd --version", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provisioning_imports_then_installs_then_places_the_cli()
    {
        var wsl = new FakeWsl();
        wsl.Answer(0, "Ubuntu\n");            // --list --quiet: ours is not registered
        var provisioner = Provisioner(wsl, out var paths, WithRealArtefacts());

        var outcome = await provisioner.ProvisionAsync();

        Assert.True(outcome.Succeeded, outcome.Failure?.Detail);
        Assert.Equal(
            [ProvisioningStep.AcquireRootfs, ProvisioningStep.AcquireEngine,
             ProvisioningStep.AcquireCli, ProvisioningStep.ImportDistribution,
             ProvisioningStep.InstallEngine, ProvisioningStep.PlaceCli],
            outcome.Steps.Select(step => step.Step));
        Assert.NotNull(wsl.WithVerb("--import"));
        Assert.True(File.Exists(paths.DockerCli));
    }

    [Fact]
    public async Task An_already_registered_distribution_is_left_alone()
    {
        var wsl = new FakeWsl();
        wsl.Answer(0, "Ubuntu\r\ndockerdesk\r\n");
        var provisioner = Provisioner(wsl, out _, WithRealArtefacts());

        var outcome = await provisioner.ProvisionAsync();

        Assert.True(outcome.Succeeded, outcome.Failure?.Detail);
        Assert.Null(wsl.WithVerb("--import"));
        Assert.Contains("already registered",
            outcome.Steps.Single(s => s.Step == ProvisioningStep.ImportDistribution).Detail,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_digest_stops_before_any_wsl_command_runs()
    {
        var wsl = new FakeWsl();
        var provisioner = Provisioner(wsl, out _, _ => Encoding.UTF8.GetBytes("wrong bytes"));

        var outcome = await provisioner.ProvisionAsync();

        Assert.False(outcome.Succeeded);
        Assert.Equal(ProvisioningStep.AcquireRootfs, outcome.Failure!.Step);
        Assert.Single(outcome.Steps);
        Assert.Empty(wsl.Invocations);
    }

    [Fact]
    public async Task A_failed_import_stops_before_the_engine_is_unpacked()
    {
        var wsl = new FakeWsl();
        wsl.Answer(0, "Ubuntu\n");
        wsl.Answer(1, "There is not enough space on the disk.");
        var provisioner = Provisioner(wsl, out _, WithRealArtefacts());

        var outcome = await provisioner.ProvisionAsync();

        Assert.False(outcome.Succeeded);
        Assert.Equal(ProvisioningStep.ImportDistribution, outcome.Failure!.Step);
        Assert.Contains("exited 1", outcome.Failure.Detail, StringComparison.Ordinal);
        Assert.Contains("not enough space", outcome.Failure.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(ProvisioningStep.InstallEngine, outcome.Steps.Select(s => s.Step));
    }

    [Fact]
    public async Task A_wsl_that_never_ran_is_reported_as_that_and_not_as_an_exit_code()
    {
        var wsl = new FakeWsl();
        wsl.Answer(0, "Ubuntu\n");
        wsl.Answer(null, "", "wsl.exe did not finish within 15 seconds");
        var provisioner = Provisioner(wsl, out _, WithRealArtefacts());

        var outcome = await provisioner.ProvisionAsync();

        Assert.Equal(ProvisioningStep.ImportDistribution, outcome.Failure!.Step);
        Assert.Contains("did not finish", outcome.Failure.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("exited", outcome.Failure.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Acquire_stops_after_verifying_and_touches_no_wsl_and_no_path()
    {
        var wsl = new FakeWsl();
        var provisioner = Provisioner(wsl, out var paths, WithRealArtefacts());

        var outcome = await provisioner.AcquireAsync();

        Assert.True(outcome.Succeeded, outcome.Failure?.Detail);
        Assert.Equal(3, outcome.Steps.Count);
        Assert.Empty(wsl.Invocations);
        Assert.False(File.Exists(paths.DockerCli));
    }

    [Fact]
    public async Task A_cli_archive_without_docker_exe_fails_at_the_place_step()
    {
        var wsl = new FakeWsl();
        wsl.Answer(0, "Ubuntu\n");
        var provisioner = Provisioner(wsl, out _, WithRealArtefacts(cliHasDockerExe: false));

        var outcome = await provisioner.ProvisionAsync();

        Assert.False(outcome.Succeeded);
        Assert.Equal(ProvisioningStep.PlaceCli, outcome.Failure!.Step);
        Assert.Contains("docker/docker.exe", outcome.Failure.Detail, StringComparison.Ordinal);
    }

    // ---- wiring -----------------------------------------------------------------------------

    /// <summary>
    /// A manifest whose digests are of bytes this test produces, so the whole pipeline runs without
    /// the network and the digest gate is still the real one.
    /// </summary>
    private EngineManifest _manifest = null!;

    private Func<string, byte[]?> WithRealArtefacts(bool cliHasDockerExe = true)
    {
        var rootfs = Encoding.UTF8.GetBytes("pretend rootfs tarball");
        var engine = Encoding.UTF8.GetBytes("pretend engine tarball");
        var cli = ZipBytes(cliHasDockerExe ? "docker/docker.exe" : "docker/dockerd.exe");

        _manifest = new EngineManifest
        {
            Rootfs = new Artefact("rootfs", "3.24.1",
                "https://example.invalid/rootfs.tar.gz", "rootfs.tar.gz", Digest(rootfs)),
            Engine = new Artefact("engine", "29.7.2",
                "https://example.invalid/docker.tgz", "docker.tgz", Digest(engine)),
            Cli = new Artefact("cli", "29.7.2",
                "https://example.invalid/docker.zip", "docker.zip", Digest(cli)),
        };

        return url => url switch
        {
            var u when u.EndsWith("rootfs.tar.gz", StringComparison.Ordinal) => rootfs,
            var u when u.EndsWith("docker.tgz", StringComparison.Ordinal) => engine,
            var u when u.EndsWith("docker.zip", StringComparison.Ordinal) => cli,
            _ => null,
        };
    }

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static byte[] ZipBytes(string entryName)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = archive.CreateEntry(entryName).Open();
            entry.Write(Encoding.UTF8.GetBytes("MZ pretend windows binary"));
        }

        return buffer.ToArray();
    }

    private EngineProvisioner Provisioner(FakeWsl wsl, out EnginePaths paths) =>
        Provisioner(wsl, out paths, _ => null);

    private EngineProvisioner Provisioner(
        FakeWsl wsl, out EnginePaths paths, Func<string, byte[]?> bytes)
    {
        _manifest ??= EngineManifest.Current;
        paths = new EnginePaths(Path.Combine(_temp, "install"));
        return new EngineProvisioner(
            _manifest,
            new ArtefactStore(new FakeFetcher(bytes), paths.Downloads),
            wsl,
            paths);
    }
}
