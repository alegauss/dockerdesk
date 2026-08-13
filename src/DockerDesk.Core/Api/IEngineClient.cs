namespace DockerDesk.Core.Api;

/// <summary>
/// Everything the window asks the engine for, as a seam a fixture can stand in at (DD38).
/// </summary>
/// <remarks>
/// Nothing here could be looked at without a running daemon behind it. Every window took a concrete
/// <see cref="DockerApi"/> and asked it for the rows it draws, so seeing the images page meant having
/// images, seeing a failure line meant causing one, and the three designed empty states were the
/// hardest things in the product to reach. A change to a window was reviewed by describing it, and a
/// screenshot was whatever the machine happened to be running that afternoon — which is also somebody's
/// container names in a public README.
///
/// <para><b>Why an interface and not a pipe.</b> The first attempt served the fixture from a real
/// named-pipe daemon and handed the window a real client, because that is the seam the rest of this
/// repository trusts. It deadlocked: the page's first read neither returned nor threw on the WPF
/// dispatcher, while the identical call returned at once from a test thread, and the capture succeeded
/// anyway — rendering the XAML defaults, which is a picture that lies. DD66 carries the measurement.
/// So the fixture is injected, which is what DD38 asked for in the first place.</para>
///
/// <para><b>Exactly what the window uses.</b> Not the whole client: this is the vocabulary the pages
/// and the log window actually speak, so a fixture has a bounded thing to implement and a reader can
/// see the window's whole appetite in one screen. The agent surface has its own narrower handles for
/// its own reason — see <see cref="Agent.IEngineReads"/>.</para>
/// </remarks>
public interface IEngineClient
{
    /// <summary>Every container, stopped ones included.</summary>
    /// <param name="all">Whether to include the ones that are not running.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The containers.</returns>
    Task<IReadOnlyList<ContainerSummary>> ContainersAsync(
        bool all = true, CancellationToken cancellation = default);

    /// <summary>One container's whole entity tree.</summary>
    /// <param name="id">The container.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The inspect.</returns>
    Task<ContainerInspect> InspectAsync(string id, CancellationToken cancellation = default);

    /// <summary>A container's log, as the daemon frames it.</summary>
    /// <param name="id">The container.</param>
    /// <param name="tail">How many lines of history to open with.</param>
    /// <param name="follow">Whether to keep the stream open for new output.</param>
    /// <param name="timestamps">Whether each line carries the time the daemon wrote it.</param>
    /// <param name="since">Only output written after this.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The response body.</returns>
    Task<Stream> LogsAsync(
        string id,
        int tail = 2000,
        bool follow = true,
        bool timestamps = false,
        DateTimeOffset? since = null,
        CancellationToken cancellation = default);

    /// <summary>Every image, dangling ones included.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The images.</returns>
    Task<IReadOnlyList<ImageSummary>> ImagesAsync(CancellationToken cancellation = default);

    /// <summary>Every volume, without sizing any of them.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The volumes.</returns>
    Task<IReadOnlyList<VolumeSummary>> VolumesAsync(CancellationToken cancellation = default);

    /// <summary>The same volumes, with what they cost on disk.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The volumes, with usage filled in.</returns>
    Task<IReadOnlyList<VolumeSummary>> VolumeSizesAsync(CancellationToken cancellation = default);

    /// <summary>Start a container.</summary>
    /// <param name="id">The container.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    Task StartContainerAsync(string id, CancellationToken cancellation = default);

    /// <summary>Stop a container.</summary>
    /// <param name="id">The container.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    Task StopContainerAsync(string id, CancellationToken cancellation = default);

    /// <summary>Restart a container.</summary>
    /// <param name="id">The container.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    Task RestartContainerAsync(string id, CancellationToken cancellation = default);

    /// <summary>Remove a container.</summary>
    /// <param name="id">The container.</param>
    /// <param name="force">Whether to kill it first.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    Task RemoveContainerAsync(string id, bool force = false, CancellationToken cancellation = default);

    /// <summary>Remove one image.</summary>
    /// <param name="id">The image.</param>
    /// <param name="force">Whether to remove it despite a container referencing it.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    Task RemoveImageAsync(string id, bool force = false, CancellationToken cancellation = default);

    /// <summary>Remove one volume.</summary>
    /// <param name="name">The volume's name.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    Task RemoveVolumeAsync(string name, CancellationToken cancellation = default);

    /// <summary>Delete dangling images and report what came back.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>What was deleted.</returns>
    Task<ImagesPruned> PruneDanglingImagesAsync(CancellationToken cancellation = default);

    /// <summary>Delete anonymous unused volumes and report what came back.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>What was deleted.</returns>
    Task<VolumesPruned> PruneAnonymousVolumesAsync(CancellationToken cancellation = default);

    /// <summary>Run one command inside a running container and hand back what it exited with.</summary>
    /// <param name="id">The container.</param>
    /// <param name="command">The command and its arguments.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The exit code.</returns>
    Task<int> RunInContainerAsync(
        string id, IReadOnlyList<string> command, CancellationToken cancellation = default);
}
