using System.Text.Json.Serialization;

namespace FreeWilly.Core.Api;

/// <summary>Who an event happened to.</summary>
public sealed record EventActor
{
    /// <summary>The container, image or volume id.</summary>
    [JsonPropertyName("ID")]
    public string Id { get; init; } = "";

    /// <summary>
    /// Whatever the daemon attached — <c>name</c> and <c>image</c> for a container, and a dozen
    /// labels this tool does not read.
    /// </summary>
    [JsonPropertyName("Attributes")]
    public IReadOnlyDictionary<string, string> Attributes { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>One line off <c>/events</c>.</summary>
public sealed record DockerEvent
{
    /// <summary>What kind of object: container, image, volume, network.</summary>
    [JsonPropertyName("Type")]
    public string Type { get; init; } = "";

    /// <summary>What happened: create, start, die, destroy, and so on.</summary>
    [JsonPropertyName("Action")]
    public string Action { get; init; } = "";

    /// <summary>Who it happened to.</summary>
    [JsonPropertyName("Actor")]
    public EventActor Actor { get; init; } = new();

    /// <summary>When, in seconds since the epoch, as the daemon reports it.</summary>
    [JsonPropertyName("time")]
    public long Time { get; init; }

    /// <summary>The object's id.</summary>
    public string Id => Actor.Id;

    /// <summary>The container's name, when the daemon attached one.</summary>
    public string? Name =>
        Actor.Attributes.TryGetValue("name", out var name) ? name : null;

    /// <summary>The twelve characters a list shows.</summary>
    public string ShortId => Id.Length > 12 ? Id[..12] : Id;

    /// <summary>
    /// Whether a container list rendered before this event is now wrong.
    /// </summary>
    /// <remarks>
    /// The daemon emits a great deal that changes nothing a list shows — <c>exec_create</c>,
    /// <c>exec_start</c>, health checks, every image pull layer. Refreshing on all of them turns an
    /// event stream back into a poll with extra steps, which is what this exists to avoid.
    /// </remarks>
    public bool ChangesTheContainerList =>
        Type == "container" && Action is
            "create" or "start" or "stop" or "die" or "kill" or "destroy"
            or "pause" or "unpause" or "restart" or "rename" or "update";

    /// <summary>
    /// Whether an image list rendered before this event is now wrong.
    /// </summary>
    /// <remarks>
    /// Not the same set as the container one. An image arriving or going is obvious; a container
    /// being created or destroyed is here because the image list's other column is what holds each
    /// image, and that answer changes without any image changing. A container merely starting or
    /// stopping does not: it held the image either way.
    /// </remarks>
    public bool ChangesTheImageList =>
        (Type == "image" && Action is "pull" or "import" or "load" or "delete" or "untag" or "tag")
        || (Type == "container" && Action is "create" or "destroy");
}
