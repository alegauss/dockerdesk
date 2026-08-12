using System.Text.Json.Serialization;

namespace DockerDesk.Core.Api;

/// <summary>What the daemon says about itself.</summary>
/// <remarks>
/// Five fields out of a response with thirty. The client is typed only where a field is read: a
/// generated model of an API surface this tool will use a tenth of is a maintenance cost with no
/// reader.
/// </remarks>
public sealed record EngineVersion
{
    /// <summary>The engine version, e.g. <c>29.7.2</c>.</summary>
    [JsonPropertyName("Version")]
    public string Version { get; init; } = "";

    /// <summary>The newest API version this daemon speaks, e.g. <c>1.55</c>.</summary>
    [JsonPropertyName("ApiVersion")]
    public string ApiVersion { get; init; } = "";

    /// <summary>The oldest API version it still answers.</summary>
    [JsonPropertyName("MinAPIVersion")]
    public string MinApiVersion { get; init; } = "";

    /// <summary>The daemon's operating system — <c>linux</c> for an engine in WSL2.</summary>
    [JsonPropertyName("Os")]
    public string Os { get; init; } = "";

    /// <summary>Its architecture.</summary>
    [JsonPropertyName("Arch")]
    public string Arch { get; init; } = "";
}

/// <summary>One published port.</summary>
public sealed record PortBinding
{
    /// <summary>The address on the host, when the port is published.</summary>
    [JsonPropertyName("IP")]
    public string? Ip { get; init; }

    /// <summary>The port inside the container.</summary>
    [JsonPropertyName("PrivatePort")]
    public int PrivatePort { get; init; }

    /// <summary>The port on the host, absent when nothing is published.</summary>
    [JsonPropertyName("PublicPort")]
    public int? PublicPort { get; init; }

    /// <summary>tcp or udp.</summary>
    [JsonPropertyName("Type")]
    public string Type { get; init; } = "";

    /// <summary>How a list renders this row — <c>8080->80/tcp</c>, or just the private port.</summary>
    public override string ToString() => PublicPort is { } published
        ? $"{published}->{PrivatePort}/{Type}"
        : $"{PrivatePort}/{Type}";
}

/// <summary>A container, as the list endpoint reports it.</summary>
public sealed record ContainerSummary
{
    /// <summary>The full id.</summary>
    [JsonPropertyName("Id")]
    public string Id { get; init; } = "";

    /// <summary>The image it was created from.</summary>
    [JsonPropertyName("Image")]
    public string Image { get; init; } = "";

    /// <summary>One word: running, exited, created, paused.</summary>
    [JsonPropertyName("State")]
    public string State { get; init; } = "";

    /// <summary>The human sentence, e.g. <c>Exited (0) 2 minutes ago</c>.</summary>
    [JsonPropertyName("Status")]
    public string Status { get; init; } = "";

    /// <summary>Its names, each with a leading slash as the API returns them.</summary>
    [JsonPropertyName("Names")]
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>Its ports.</summary>
    [JsonPropertyName("Ports")]
    public IReadOnlyList<PortBinding> Ports { get; init; } = [];

    /// <summary>The first name without its leading slash, or the short id when it has none.</summary>
    public string DisplayName => Names.Count > 0
        ? Names[0].TrimStart('/')
        : ShortId;

    /// <summary>The twelve characters every Docker tool shows.</summary>
    public string ShortId => Id.Length > 12 ? Id[..12] : Id;

    /// <summary>
    /// The ports as a list renders them, with rows that would read identically collapsed.
    /// </summary>
    /// <remarks>
    /// A published port comes back twice — once for <c>0.0.0.0</c> and once for <c>::</c> — and both
    /// render as <c>8099-&gt;80/tcp</c> once the address is dropped. Measured against a real daemon:
    /// one <c>-p 8099:80</c> produced exactly that, printed twice. <see cref="Ports"/> keeps what the
    /// API said; this is what a person should see.
    /// </remarks>
    public IReadOnlyList<string> PublishedPorts =>
        [.. Ports.Select(port => port.ToString()).Distinct(StringComparer.Ordinal)];
}
