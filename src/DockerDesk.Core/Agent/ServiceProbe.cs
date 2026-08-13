using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Sockets;

namespace DockerDesk.Core.Agent;

/// <summary>What happened when Windows tried the socket.</summary>
/// <param name="HostPort">The published host port.</param>
/// <param name="ContainerPort">What it maps to inside, for the row's text.</param>
/// <param name="Accepted">Whether the connection was accepted.</param>
/// <param name="Milliseconds">How long it took, accepted or not.</param>
/// <param name="Failure">Why not, where it was not.</param>
public sealed record PortAnswer(
    int HostPort, string ContainerPort, bool Accepted, int Milliseconds, string? Failure);

/// <summary>What happened when Windows asked the service a question.</summary>
/// <param name="Target">What was asked, as it would be typed.</param>
/// <param name="Status">The HTTP status, where one came back.</param>
/// <param name="Milliseconds">How long it took.</param>
/// <param name="Failure">Why there was no status.</param>
public sealed record RequestAnswer(string Target, int? Status, int Milliseconds, string? Failure);

/// <summary>Reaching a published port from Windows, which is the fact the daemon cannot supply.</summary>
public interface IServiceProbe
{
    /// <summary>Open a connection and close it.</summary>
    /// <param name="hostPort">The published host port.</param>
    /// <param name="containerPort">What it maps to inside.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <returns>What happened.</returns>
    PortAnswer Connect(int hostPort, string containerPort, TimeSpan timeout);

    /// <summary>Ask for one path and read the status line.</summary>
    /// <param name="hostPort">The published host port.</param>
    /// <param name="path">The path, beginning with a slash.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <returns>What came back.</returns>
    RequestAnswer Get(int hostPort, string path, TimeSpan timeout);
}

/// <summary>
/// The probe, and the one place on this surface that reaches something other than the daemon.
/// </summary>
/// <remarks>
/// DD30. <c>running</c> and <c>answering</c> are different facts and the gap between them is where an
/// agent stops being able to make progress: the process died inside the container, the app bound to
/// <c>127.0.0.1</c> rather than <c>0.0.0.0</c>, the bind mount resolved to an empty directory because a
/// Windows path did not survive the hop into WSL. None of it is visible from the Engine API, and today
/// it is closed by a person opening a browser and reporting back — the most expensive cycle there is.
///
/// <para><b>Why this is still a read.</b> DD28 deliberately chose the socket table over a connect,
/// because a connect reaches somebody's service. That reasoning holds and this is the narrow exception
/// it argued against: <c>read</c> promises not to mutate <b>the engine</b> — the same promise that lets
/// <c>read logs --out</c> write a file — and a connect that opens and closes reaches a socket the caller
/// named in the same breath. A request is a further step, appears in somebody's access log, and is
/// therefore opt-in; it is a GET and nothing else, because a verify that could POST is not a verify.</para>
///
/// <para>Loopback rather than the machine's address. A published port binds <c>0.0.0.0</c> and the
/// question is whether this machine can reach it; going out to the LAN address would add a firewall to
/// the list of things a failure could mean.</para>
/// </remarks>
public sealed class ServiceProbe : IServiceProbe
{
    /// <summary>Where a published port is reached.</summary>
    public const string Loopback = "127.0.0.1";

    /// <inheritdoc/>
    public PortAnswer Connect(int hostPort, string containerPort, TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hostPort);

        var clock = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            using var deadline = new CancellationTokenSource(timeout);
            client.ConnectAsync(Loopback, hostPort, deadline.Token).AsTask().GetAwaiter().GetResult();
            return new PortAnswer(hostPort, containerPort, true, (int)clock.ElapsedMilliseconds, null);
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException
            or AggregateException)
        {
            return new PortAnswer(
                hostPort,
                containerPort,
                false,
                (int)clock.ElapsedMilliseconds,
                exception is SocketException socket
                    ? socket.SocketErrorCode.ToString()
                    : "timed out after " + Brief(timeout));
        }
    }

    /// <inheritdoc/>
    public RequestAnswer Get(int hostPort, string path, TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hostPort);
        ArgumentNullException.ThrowIfNull(path);

        var target = $"http://{Loopback}:{hostPort.ToString(CultureInfo.InvariantCulture)}{path}";
        var clock = Stopwatch.StartNew();
        try
        {
            // Redirects are not followed: a 301 is an answer, and following it would report the status
            // of somewhere else as though it were this service's.
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { Timeout = timeout };
            using var response = http
                .Send(new HttpRequestMessage(HttpMethod.Get, target), HttpCompletionOption.ResponseHeadersRead);
            return new RequestAnswer(
                target, (int)response.StatusCode, (int)clock.ElapsedMilliseconds, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
            or OperationCanceledException or InvalidOperationException or UriFormatException)
        {
            return new RequestAnswer(
                target,
                null,
                (int)clock.ElapsedMilliseconds,
                exception is TaskCanceledException or OperationCanceledException
                    ? "timed out after " + Brief(timeout)
                    : exception.Message);
        }
    }

    private static string Brief(TimeSpan span) =>
        ((int)span.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s";
}
