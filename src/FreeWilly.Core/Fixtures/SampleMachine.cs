using System.Globalization;
using FreeWilly.Core.Api;

namespace FreeWilly.Core.Fixtures;

/// <summary>
/// A machine that is always there, so a window can be looked at without one running (DD38).
/// </summary>
/// <remarks>
/// <b>Chosen to cover the states, not to look plausible.</b> Running and exited, a published port and
/// an exposed-only one, a kill with its exit code beside a clean exit, a dangling image beside an
/// in-use one, an anonymous volume beside a named one a container still holds, and a volume large
/// enough that the size column says something. Each row is here because something in the window
/// renders differently for it; a row that changed no pixel would be a row nobody could justify keeping
/// correct.
///
/// <para><b>Every name begins with <see cref="Prefix"/>.</b> A screenshot of this in a README should be
/// obviously a fixture. The alternative — plausible names — is how somebody's real project ends up in
/// documentation, which is half of why this exists.</para>
///
/// <para><b>Writes refuse, and say so in the engine's voice.</b> A preview whose buttons deleted things
/// would be a preview you have to be careful with, and the refusal line under a row is one of the
/// states worth seeing anyway. So every write here throws the same
/// <see cref="DockerApiException"/> the daemon's refusal arrives as.</para>
/// </remarks>
public sealed class SampleMachine : IEngineClient
{
    /// <summary>What every name here starts with, so a capture is never mistaken for a real machine.</summary>
    public const string Prefix = "sample-";

    /// <summary>What a write answers with, in the shape a refusal arrives in.</summary>
    public const string ReadOnly = "this is a fixture: nothing here is a real container";

    /// <summary>
    /// The engine this fixture pretends to be, pinned so a capture of the About page is the same
    /// picture on every machine (DD83).
    /// </summary>
    /// <remarks>
    /// The manifest's own engine version, so the fixture and the thing this install would place do
    /// not disagree in a screenshot somebody reads as documentation.
    /// </remarks>
    public static EngineVersion Version { get; } = new()
    {
        Version = Engine.EngineManifest.Current.Engine.Version,
        ApiVersion = "1.55",
        MinApiVersion = "1.24",
        Os = "linux",
        Arch = "amd64",
    };

    /// <inheritdoc/>
    public Task<EngineVersion> VersionAsync(CancellationToken cancellation = default) =>
        Task.FromResult(Version);

    private static ContainerSummary Container(
        string id, string name, string image, string state, string status,
        string? service = null, IReadOnlyList<PortBinding>? ports = null) => new()
    {
        Id = id,
        Names = ["/" + name],
        Image = image,
        State = state,
        Status = status,
        Ports = ports ?? [],
        Labels = service is null
            ? null
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["com.docker.compose.project"] = "sample",
                ["com.docker.compose.service"] = service,
            },
    };

    private static PortBinding Published(int host, int inside) => new()
    {
        Ip = "0.0.0.0", PublicPort = host, PrivatePort = inside, Type = "tcp",
    };

    private static PortBinding Exposed(int inside) => new()
    {
        PrivatePort = inside, Type = "tcp",
    };

    private readonly List<ContainerSummary> _containers =
    [
        // Running with a published port: the row whose port is a link.
        Container("c1aaaaaaaaaa0000", Prefix + "api-1", "sample/api:1.4.2", "running",
            "Up 6 minutes", "api", [Published(8080, 8080)]),

        // Running and healthy, so the status column carries something other than a duration.
        Container("c2bbbbbbbbbb0000", Prefix + "db-1", "postgres:16-alpine", "running",
            "Up 6 minutes (healthy)", "db", [Published(5432, 5432)]),

        // Killed. 137 is the exit code the whole diagnostic half of this product is about.
        Container("c3cccccccccc0000", Prefix + "worker-1", "sample/api:1.4.2", "exited",
            "Exited (137) 12 seconds ago", "worker"),

        // Exposed but not published: the port that is text rather than a link.
        Container("c4dddddddddd0000", Prefix + "cache-1", "redis:7-alpine", "running",
            "Up 2 hours", ports: [Exposed(6379)]),

        // A clean exit, which reads differently from a kill and shares a row shape with it.
        Container("c5eeeeeeeeee0000", Prefix + "migrate-1", "sample/api:1.4.2", "exited",
            "Exited (0) 3 minutes ago"),
    ];

    private readonly List<ImageSummary> _images =
    [
        new() { Id = "sha256:1111111111111111", RepoTags = ["sample/api:1.4.2"], Size = 184320000 },
        new() { Id = "sha256:2222222222222222", RepoTags = ["postgres:16-alpine"], Size = 247000000 },
        new() { Id = "sha256:3333333333333333", RepoTags = ["redis:7-alpine"], Size = 41200000 },

        // Dangling, twice: enough for the prune button to have a number worth confirming.
        new() { Id = "sha256:4444444444444444", RepoTags = ["<none>:<none>"], Size = 183900000 },
        new() { Id = "sha256:5555555555555555", RepoTags = ["<none>:<none>"], Size = 96400000 },
    ];

    private readonly List<VolumeSummary> _volumes =
    [
        new() { Name = "sample_db-data", Driver = "local" },
        new() { Name = "sample_uploads", Driver = "local" },

        // Anonymous: the sixty-four hex characters `docker run -v /data` leaves behind, and the one
        // the prune button is actually for.
        new()
        {
            Name = "9f2c1d4b6a8e0f3c5d7b9a1e2f4c6d8b0a2e4f6c8d0b2a4e6f8c0d2b4a6e8f0c",
            Driver = "local",
        },
    ];

    /// <inheritdoc/>
    public Task<IReadOnlyList<ContainerSummary>> ContainersAsync(
        bool all = true, CancellationToken cancellation = default) =>
        Task.FromResult<IReadOnlyList<ContainerSummary>>(
            all ? _containers : [.. _containers.Where(c => c.State == "running")]);

    /// <inheritdoc/>
    public Task<ContainerInspect> InspectAsync(string id, CancellationToken cancellation = default) =>
        Task.FromResult(new ContainerInspect
        {
            Id = id,
            Name = "/" + (_containers.FirstOrDefault(c => c.Id == id)?.DisplayName ?? id),
            State = new ContainerState { Status = "running" },
            HostConfig = new ContainerHostConfig(),
            Mounts = [],
        });

    /// <summary>A log with something in it, including a line on stderr so the red one is visible.</summary>
    /// <remarks>
    /// Framed the way the daemon frames a log from a container with no TTY: an eight-byte header per
    /// chunk whose first byte is the stream. Faking the text without the frames would have made the
    /// preview prove the opposite of what it is for — the de-framing is part of what the log window is.
    /// </remarks>
    public Task<Stream> LogsAsync(
        string id,
        int tail = 2000,
        bool follow = true,
        bool timestamps = false,
        DateTimeOffset? since = null,
        CancellationToken cancellation = default)
    {
        var body = new MemoryStream();
        foreach (var (stream, line) in Lines)
        {
            var text = System.Text.Encoding.UTF8.GetBytes(line + "\n");
            var header = new byte[8];
            header[0] = stream;
            header[4] = (byte)(text.Length >> 24);
            header[5] = (byte)(text.Length >> 16);
            header[6] = (byte)(text.Length >> 8);
            header[7] = (byte)text.Length;
            body.Write(header);
            body.Write(text);
        }

        body.Position = 0;
        return Task.FromResult<Stream>(body);
    }

    private static readonly (byte Stream, string Line)[] Lines =
    [
        (1, "listening on :8080"),
        (1, "connected to postgres://sample-db-1:5432/sample"),
        (1, "GET /healthz 200 1ms"),
        (2, "warn: retrying migration 0004 (attempt 2)"),
        (1, "GET /api/orders 200 14ms"),
        (2, "java.net.BindException: Address already in use"),
        (1, "shutting down"),
    ];

    /// <inheritdoc/>
    public Task<IReadOnlyList<ImageSummary>> ImagesAsync(CancellationToken cancellation = default) =>
        Task.FromResult<IReadOnlyList<ImageSummary>>(_images);

    /// <inheritdoc/>
    public Task<IReadOnlyList<VolumeSummary>> VolumesAsync(CancellationToken cancellation = default) =>
        Task.FromResult<IReadOnlyList<VolumeSummary>>(_volumes);

    /// <summary>The same volumes, measured.</summary>
    /// <remarks>
    /// A separate answer because it is a separate call: the list is metadata and comes back at once,
    /// the sizes walk the filesystem. The window fills that column after the list is already up, and a
    /// fixture answering both from one payload would hide the shape of that.
    /// </remarks>
    public Task<IReadOnlyList<VolumeSummary>> VolumeSizesAsync(
        CancellationToken cancellation = default) =>
        Task.FromResult<IReadOnlyList<VolumeSummary>>(
        [
            new() { Name = "sample_db-data", Driver = "local",
                    UsageData = new VolumeUsage { Size = 412000000, RefCount = 1 } },
            new() { Name = "sample_uploads", Driver = "local",
                    UsageData = new VolumeUsage { Size = 1870000000, RefCount = 0 } },
            new() { Name = _volumes[2].Name, Driver = "local",
                    UsageData = new VolumeUsage { Size = 24000000, RefCount = 0 } },
        ]);

    /// <inheritdoc/>
    public Task StartContainerAsync(string id, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task StopContainerAsync(string id, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task RestartContainerAsync(string id, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task RemoveContainerAsync(
        string id, bool force = false, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task RemoveImageAsync(
        string id, bool force = false, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task RemoveVolumeAsync(string name, CancellationToken cancellation = default) => Refuse();

    /// <inheritdoc/>
    public Task<ImagesPruned> PruneDanglingImagesAsync(CancellationToken cancellation = default) =>
        Refuse<ImagesPruned>();

    /// <inheritdoc/>
    public Task<VolumesPruned> PruneAnonymousVolumesAsync(CancellationToken cancellation = default) =>
        Refuse<VolumesPruned>();

    /// <summary>No shell, so the row says so rather than opening a terminal that closes.</summary>
    public Task<int> RunInContainerAsync(
        string id, IReadOnlyList<string> command, CancellationToken cancellation = default) =>
        Task.FromResult(1);

    /// <summary>How many containers this machine has, for a test that would otherwise count by hand.</summary>
    public int ContainerCount => _containers.Count;

    /// <summary>How many images, likewise.</summary>
    public int ImageCount => _images.Count;

    /// <summary>How many volumes, likewise.</summary>
    public int VolumeCount => _volumes.Count;

    /// <summary>What every published host port is, so a test can name one without a literal.</summary>
    public IReadOnlyList<int> PublishedPorts =>
        [.. _containers.SelectMany(c => c.Ports)
            .Where(p => p.PublicPort is > 0)
            .Select(p => p.PublicPort!.Value)
            .Order()];

    private static Task Refuse() =>
        Task.FromException(new DockerApiException(ReadOnly, detail: ReadOnly));

    private static Task<T> Refuse<T>() =>
        Task.FromException<T>(new DockerApiException(ReadOnly, detail: ReadOnly));

    /// <summary>The size column's own words, so a test can assert against the fixture's intent.</summary>
    public static string Bytes(long size) =>
        (size / 1_000_000d).ToString("0", CultureInfo.InvariantCulture) + " MB";
}
