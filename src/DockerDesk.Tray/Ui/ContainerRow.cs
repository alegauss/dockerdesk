using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;

namespace DockerDesk.Tray.Ui;

/// <summary>One published port, and where clicking it goes.</summary>
/// <param name="Text">What the cell reads, e.g. <c>8080-&gt;80/tcp</c>.</param>
/// <param name="Url">
/// Where to open, or <see langword="null"/> when there is nowhere: a port the container exposes and
/// nothing published has no address on this machine, so it is text and not a link.
/// </param>
public sealed record PortLink(string Text, string? Url)
{
    /// <summary>Whether this one can be clicked.</summary>
    public bool IsLink => Url is not null;

    /// <summary>Read a binding.</summary>
    /// <param name="port">The binding.</param>
    /// <returns>The cell.</returns>
    public static PortLink From(PortBinding port)
    {
        ArgumentNullException.ThrowIfNull(port);

        // Only TCP gets a link. A published UDP port is real and http://localhost:x is not where it
        // is, and a link that opens a browser onto nothing is worse than plain text.
        var url = port.PublicPort is { } published
            && port.Type.Equals("tcp", StringComparison.OrdinalIgnoreCase)
                ? $"http://localhost:{published}"
                : null;

        return new PortLink(port.ToString(), url);
    }
}

/// <summary>A container, as the window shows it.</summary>
/// <param name="Name">The name, or the short id when it has none.</param>
/// <param name="Image">The image.</param>
/// <param name="State">One word.</param>
/// <param name="Status">The daemon's own sentence: <c>Up 3 minutes</c>, <c>Exited (0) …</c>.</param>
/// <param name="Ports">The ports, deduplicated, each with its link or without one.</param>
/// <param name="Id">The full id, for the actions a later task adds.</param>
public sealed record ContainerRow(
    string Name,
    string Image,
    string State,
    string Status,
    IReadOnlyList<PortLink> Ports,
    string Id)
{
    /// <summary>Whether this container is running now.</summary>
    public bool IsRunning => State.Equals("running", StringComparison.OrdinalIgnoreCase);

    /// <summary>Project one summary.</summary>
    /// <param name="container">What the API returned.</param>
    /// <returns>The row.</returns>
    public static ContainerRow From(ContainerSummary container)
    {
        ArgumentNullException.ThrowIfNull(container);

        // Deduplicated the same way the list renders them: a port published on both address
        // families comes back twice and would otherwise be two identical cells.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ports = new List<PortLink>();
        foreach (var port in container.Ports)
        {
            var link = PortLink.From(port);
            if (seen.Add(link.Text))
            {
                ports.Add(link);
            }
        }

        return new ContainerRow(
            container.DisplayName,
            container.Image,
            container.State,
            container.Status,
            ports,
            container.Id);
    }
}

/// <summary>What the window says when the list is empty.</summary>
/// <param name="Headline">One line, large.</param>
/// <param name="Detail">One sentence under it.</param>
/// <param name="OffersStart">Whether the empty state should offer to start the engine.</param>
public sealed record EmptyState(string Headline, string Detail, bool OffersStart)
{
    /// <summary>
    /// The empty state for an engine in <paramref name="engine"/>.
    /// </summary>
    /// <remarks>
    /// Empty is a designed state and not a blank grid. The first screen a new user sees is usually
    /// this one, and a table with headers and nothing under them is where a free alternative loses
    /// them — so the two reasons a list is empty say different things, and only one of them is
    /// something the user can act on.
    /// </remarks>
    /// <param name="engine">What the engine is doing.</param>
    /// <returns>What to show.</returns>
    public static EmptyState For(EngineState engine) => engine switch
    {
        EngineState.Running => new EmptyState(
            "No containers",
            "The engine is running and has nothing to show. Start one and it appears here.",
            OffersStart: false),

        EngineState.Starting => new EmptyState(
            "Starting the engine",
            "This takes a few seconds while WSL2 boots the distribution.",
            OffersStart: false),

        _ => new EmptyState(
            "The engine is not running",
            "Start it to see your containers.",
            OffersStart: true),
    };
}
