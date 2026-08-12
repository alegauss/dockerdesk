using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DockerDesk.Core.Engine;

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
    private const string ResourceName = "DockerDesk.Core.Engine.engine-manifest.json";

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

    /// <summary>The manifest compiled into this assembly.</summary>
    public static EngineManifest Current => Embedded.Value;

    /// <summary>Every artefact, in the order they are acquired.</summary>
    public IReadOnlyList<Artefact> Artefacts => [Rootfs, Engine, Cli];

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
