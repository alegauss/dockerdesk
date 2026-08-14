namespace FreeWilly.Tray.Ui;

/// <summary>
/// The order a project's services are started and stopped in (DD107).
/// </summary>
/// <remarks>
/// Compose starts in dependency order and stops in the reverse of it, and fanning a verb across a
/// project's containers in list order is the version of this that usually works: it leaves a service
/// talking to a database that already went, and on the way up it starts an api against a postgres
/// that has not opened its socket. Both look like the application's bug rather than the button's.
///
/// <para><b>The label is already on the container</b> — <c>com.docker.compose.depends_on</c>, beside
/// the project and service labels the list response carries — so the order costs no read of a compose
/// file this window does not have and no working directory it cannot know.</para>
///
/// <para><b>It falls back rather than refusing.</b> A cycle, a label naming a service that is not
/// here, a container with no service name at all: every one of those answers the input order instead
/// of throwing, because a button that stops nothing because a label was odd is worse than one that
/// stops things in a defensible order. The fallback is the caller's own ordering, which is already
/// the sorted list the window is showing.</para>
/// </remarks>
public static class ComposeOrder
{
    /// <summary>The compose label carrying what a service waits for.</summary>
    /// <remarks>
    /// Its value is <c>db:service_started:false,cache:service_healthy:true</c> — one entry per
    /// dependency, each the service name, the condition and whether compose restarts it. Only the
    /// first field is an ordering; the condition is about waiting, which is compose's job and not a
    /// window's.
    /// </remarks>
    public const string DependsOnLabel = "com.docker.compose.depends_on";

    /// <summary>Read the service names out of one <c>depends_on</c> label.</summary>
    /// <param name="label">The label's value, or nothing.</param>
    /// <returns>The services named, in the order they appear, without repeats.</returns>
    public static IReadOnlyList<string> DependenciesIn(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return [];
        }

        var named = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in label.Split(',', StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries))
        {
            var colon = entry.IndexOf(':', StringComparison.Ordinal);
            var service = colon < 0 ? entry : entry[..colon];
            if (service.Length > 0 && seen.Add(service))
            {
                named.Add(service);
            }
        }

        return named;
    }

    /// <summary>The order these containers are started in: what is depended on, first.</summary>
    /// <param name="children">The project's containers, in the order the window is showing them.</param>
    /// <returns>The same containers, reordered.</returns>
    public static IReadOnlyList<ContainerRow> ToStart(IReadOnlyList<ContainerRow> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        if (children.Count < 2)
        {
            return children;
        }

        // By service name, because that is what the label names. A container with none cannot be
        // depended on and cannot be ordered, so the whole project falls back rather than half of it
        // being sorted and half appended — a half-order is one nobody can reason about.
        var byService = new Dictionary<string, ContainerRow>(StringComparer.Ordinal);
        foreach (var child in children)
        {
            if (child.Service is not { Length: > 0 } service || !byService.TryAdd(service, child))
            {
                return children;
            }
        }

        // Kahn's, with the input order as the tie-break: two services that depend on nothing must
        // come out in the order the window is showing them, or pressing Stop twice reorders the
        // calls for no reason a reader could see.
        var waiting = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var blocking = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (service, child) in byService)
        {
            var needs = child.DependsOn.Where(byService.ContainsKey).Distinct(StringComparer.Ordinal);
            waiting[service] = [.. needs];
            blocking[service] = [];
        }

        foreach (var (service, needs) in waiting)
        {
            foreach (var need in needs)
            {
                blocking[need].Add(service);
            }
        }

        var order = new List<ContainerRow>(children.Count);
        var ready = children
            .Where(child => waiting[child.Service!].Count == 0)
            .Select(child => child.Service!)
            .ToList();

        var left = waiting.ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.Ordinal);
        while (ready.Count > 0)
        {
            var service = ready[0];
            ready.RemoveAt(0);
            order.Add(byService[service]);

            foreach (var dependent in blocking[service])
            {
                if (--left[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        // A cycle. Compose refuses to build one, so reaching here means a label this window did not
        // write — and an order that dropped half the project would stop half of it.
        return order.Count == children.Count ? order : children;
    }

    /// <summary>The order these containers are stopped in: the reverse of starting.</summary>
    /// <param name="children">The project's containers.</param>
    /// <returns>The same containers, reordered.</returns>
    public static IReadOnlyList<ContainerRow> ToStop(IReadOnlyList<ContainerRow> children) =>
        [.. ToStart(children).Reverse()];
}
