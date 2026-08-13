using System.Net.NetworkInformation;

namespace FreeWilly.Core.Agent;

/// <summary>What Windows is listening on. The half of the port question Docker cannot answer.</summary>
public interface IHostPorts
{
    /// <summary>Every TCP port something on this machine is accepting connections on.</summary>
    /// <returns>The ports.</returns>
    IReadOnlySet<int> Listening();
}

/// <summary>
/// The host's own socket table.
/// </summary>
/// <remarks>
/// DD26. A container with a published port and nothing behind it is indistinguishable from a working
/// one as far as the daemon is concerned: the daemon knows what was published, and only Windows knows
/// whether anything holds the socket. Being a Windows process is what lets this tool answer both halves
/// in one row.
///
/// <para>Read from the listener table rather than by connecting. A connect is a side effect — it reaches
/// somebody's service, appears in their access log, and can be refused by a firewall for reasons that
/// have nothing to do with whether the port is bound — and a verb under <c>read</c> has promised not to
/// have side effects. The table is the fact; whether the service answers is DD30's question and needs a
/// request.</para>
/// </remarks>
public sealed class HostPorts : IHostPorts
{
    /// <inheritdoc/>
    public IReadOnlySet<int> Listening()
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Select(endpoint => endpoint.Port)
                .ToHashSet();
        }
        catch (NetworkInformationException)
        {
            // An empty set is not the same as "nothing is listening", and the doctor would report every
            // port as unbound. So this is the one place the distinction matters enough to throw the
            // caller a visibly wrong answer rather than a quietly wrong one.
            throw new InvalidOperationException(
                "the Windows TCP listener table could not be read, so whether a host port is bound "
                + "cannot be answered on this machine");
        }
    }
}
