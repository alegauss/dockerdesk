using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreeWilly.Core.Engine;

/// <summary>One file this project downloads, pinned to a version and a digest.</summary>
/// <param name="Id">Short name, used in step detail and as the cache key prefix.</param>
/// <param name="Version">The upstream version, stated rather than resolved.</param>
/// <param name="Url">Where it comes from.</param>
/// <param name="FileName">What it is called on disk.</param>
/// <param name="Sha256">The digest it must have, lower-case hex.</param>
public sealed record Artefact(
    string Id,
    string Version,
    string Url,
    string FileName,
    string Sha256);

/// <summary>
/// Every artefact the engine is assembled from, pinned. Read from a JSON resource compiled into
/// this assembly, so a build carries its own answer and nothing is resolved at run time.
/// </summary>
/// <remarks>
/// "Latest" is the failure this exists to prevent: an engine that moves under a user is a support
/// case nobody can reproduce, and a digest is the only part of a download that a mirror, a proxy
/// or a bad disk cannot quietly change.
/// </remarks>
public sealed record EngineManifest
{
    private const string ResourceName = "FreeWilly.Core.Engine.engine-manifest.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lazy<EngineManifest> Embedded = new(Load);

    /// <summary>The Linux root filesystem the owned distribution is imported from.</summary>
    [JsonPropertyName("rootfs")]
    public required Artefact Rootfs { get; init; }

    /// <summary>The static Linux engine binaries: dockerd, containerd, runc and friends.</summary>
    [JsonPropertyName("engine")]
    public required Artefact Engine { get; init; }

    /// <summary>The Windows-side archive the <c>docker</c> CLI is taken out of.</summary>
    [JsonPropertyName("cli")]
    public required Artefact Cli { get; init; }

    /// <summary>
    /// The Compose CLI plugin, which the CLI archive does not carry (DD73).
    /// </summary>
    /// <remarks>
    /// A fourth pinned artefact and nothing more: Compose is a separate upstream release with its
    /// own version and its own digest, and the Windows static zip has only <c>docker.exe</c> in it.
    /// Without this, <c>docker compose</c> is not a command on a machine that never had Docker
    /// Desktop — so every compose file a user already has, and this project's own <c>do compose
    /// up</c>, fail on a subcommand that does not exist.
    /// </remarks>
    [JsonPropertyName("compose")]
    public required Artefact Compose { get; init; }

    /// <summary>
    /// The Buildx CLI plugin, which is what makes <c>docker build</c> BuildKit (DD74).
    /// </summary>
    /// <remarks>
    /// Measured on the pinned CLI with no plugin present: <c>docker build</c> falls back to the
    /// legacy builder and prints "DEPRECATED: The legacy builder is deprecated and will be removed
    /// in a future release". So it is a limited path today rather than a dead one — and limited is
    /// enough, because a <c>RUN --mount=type=cache</c> fails there with "the --mount option
    /// requires BuildKit", after the base image has already been pulled and on a line the message
    /// blames on the Dockerfile rather than on a missing plugin.
    /// </remarks>
    [JsonPropertyName("buildx")]
    public required Artefact Buildx { get; init; }

    /// <summary>The manifest compiled into this assembly.</summary>
    public static EngineManifest Current => Embedded.Value;

    /// <summary>Every artefact, in the order they are acquired.</summary>
    public IReadOnlyList<Artefact> Artefacts => [Rootfs, Engine, Cli, Compose, Buildx];

    private static EngineManifest Load()
    {
        using var stream = typeof(EngineManifest).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"{ResourceName} is not embedded in {typeof(EngineManifest).Assembly.GetName().Name}");

        return JsonSerializer.Deserialize<EngineManifest>(stream, Options)
            ?? throw new InvalidOperationException($"{ResourceName} deserialized to null");
    }

    /// <summary>Every embedded resource name, for a test that wants to say which are there.</summary>
    internal static IReadOnlyList<string> ResourceNames() =>
        [.. typeof(EngineManifest).Assembly.GetManifestResourceNames()];
}
