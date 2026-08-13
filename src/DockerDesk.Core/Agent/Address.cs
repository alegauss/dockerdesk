using System.Diagnostics.CodeAnalysis;

namespace DockerDesk.Core.Agent;

/// <summary>What an address names.</summary>
public enum AddressKind
{
    /// <summary>A container, by the name it was created with.</summary>
    Container,

    /// <summary>A compose service, as <c>svc:&lt;project&gt;/&lt;service&gt;</c>.</summary>
    Service,
}

/// <summary>
/// How the agent surface names a thing: by name, never by a 64-hex id.
/// </summary>
/// <remarks>
/// DD24, and landed here rather than later because retrofitting it is a rewrite. A container id
/// changes on recreate, so an agent that learned one has to thread it across every subsequent call and
/// re-learn it the moment the container comes back — which is a round trip spent on bookkeeping. A name
/// survives the recreate, and a compose service survives even the container.
///
/// A full 64-hex id is refused rather than accepted. That is deliberate: accepting it would make the
/// expensive path the easy one, and the refusal is the only place the reason can be said out loud.
/// A short id is indistinguishable from a name and is not refused — there is nothing to check it
/// against without a round trip, and a check that needs a call is worse than the thing it prevents.
/// </remarks>
public sealed record Address
{
    private Address(AddressKind kind, string name, string? project)
    {
        Kind = kind;
        Name = name;
        Project = project;
    }

    /// <summary>What this names.</summary>
    public AddressKind Kind { get; }

    /// <summary>The container or service name.</summary>
    public string Name { get; }

    /// <summary>The compose project, for a service.</summary>
    public string? Project { get; }

    /// <summary>The prefix that marks a compose service.</summary>
    public const string ServicePrefix = "svc:";

    /// <summary>How this reads back, which is how it was written.</summary>
    /// <returns>The address.</returns>
    public override string ToString() =>
        Kind is AddressKind.Service ? $"{ServicePrefix}{Project}/{Name}" : Name;

    /// <summary>Read an address.</summary>
    /// <param name="text">The address as an agent typed it.</param>
    /// <param name="address">The address, when it is one.</param>
    /// <param name="refusal">Why it is not, when it is not.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is an address.</returns>
    public static bool TryParse(
        string? text,
        [NotNullWhen(true)] out Address? address,
        [NotNullWhen(false)] out string? refusal)
    {
        address = null;
        refusal = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            refusal = "an address is required: a container name, or svc:<project>/<service>";
            return false;
        }

        var trimmed = text.Trim();

        if (trimmed.StartsWith(ServicePrefix, StringComparison.Ordinal))
        {
            var rest = trimmed[ServicePrefix.Length..];
            var slash = rest.IndexOf('/', StringComparison.Ordinal);
            if (slash <= 0 || slash == rest.Length - 1)
            {
                refusal = $"{trimmed} is not a service address: it is svc:<project>/<service>";
                return false;
            }

            var project = rest[..slash];
            var service = rest[(slash + 1)..];
            if (service.Contains('/', StringComparison.Ordinal))
            {
                refusal = $"{trimmed} has more than one slash: it is svc:<project>/<service>";
                return false;
            }

            address = new Address(AddressKind.Service, service, project);
            return true;
        }

        if (LooksLikeAFullId(trimmed))
        {
            refusal = $"{trimmed[..12]}… is a container id, and this surface addresses by name. "
                + "An id changes when a container is recreated, so it has to be re-learned and "
                + "threaded through every later call; a name does not.";
            return false;
        }

        address = new Address(AddressKind.Container, trimmed, project: null);
        return true;
    }

    /// <summary>Read an address, or throw.</summary>
    /// <param name="text">The address as an agent typed it.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentException">Where it is not an address.</exception>
    public static Address Parse(string? text) =>
        TryParse(text, out var address, out var refusal)
            ? address
            : throw new ArgumentException(refusal, nameof(text));

    /// <summary>Whether this is the 64 hex characters a daemon calls a container.</summary>
    private static bool LooksLikeAFullId(string text) =>
        text.Length == 64 && text.All(Uri.IsHexDigit);
}
