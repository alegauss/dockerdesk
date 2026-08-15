using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FreeWilly.Core.Engine;

/// <summary>
/// Serves the Windows named pipe every Docker client already looks for, and forwards each
/// connection to the daemon inside the distribution.
/// </summary>
/// <remarks>
/// A Linux <c>dockerd</c> cannot create a Windows named pipe — that is a Win32 object — so
/// something on this side has to, or `docker` needs a DOCKER_HOST in every shell and every script
/// the user already has. This is that something.
///
/// The pipe's ACL is the reason this exists rather than a forwarded port: only the account that
/// started the relay can connect, and the Engine API is equivalent to root on the machine.
/// </remarks>
public sealed class EnginePipeRelay : IAsyncDisposable
{
    /// <summary>The pipe name Docker clients use on Windows.</summary>
    public const string DefaultPipeName = "docker_engine";

    private readonly IEngineBackend _backend;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _accepting;

    /// <summary>Construct a relay.</summary>
    /// <param name="backend">How a channel to the daemon is opened.</param>
    /// <param name="pipeName">The pipe to serve. Overridden in tests so a run is isolated.</param>
    public EnginePipeRelay(IEngineBackend backend, string pipeName = DefaultPipeName)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        _backend = backend;
        _pipeName = pipeName;
    }

    /// <summary>How many connections have been accepted. For a test, and for a status line.</summary>
    public int Accepted { get; private set; }

    /// <summary>Start accepting. Returns as soon as the first listener is up.</summary>
    public void Start()
    {
        if (_accepting is not null)
        {
            throw new InvalidOperationException("this relay is already started");
        }

        // The first server instance is created synchronously, so a caller that polls the pipe
        // immediately afterwards cannot observe "not there yet" and conclude the engine is down.
        var first = CreateServer();
        _accepting = Task.Run(() => AcceptLoopAsync(first, _stopping.Token));
    }

    private NamedPipeServerStream CreateServer()
    {
        // Only the current user. A forwarded TCP port cannot express this, and the Engine API is
        // not something to leave open to every process on the machine.
        var security = new PipeSecurity();
        var self = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("this process has no user SID");
        security.AddAccessRule(new PipeAccessRule(self, PipeAccessRights.FullControl, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity: security);
    }

    private async Task AcceptLoopAsync(NamedPipeServerStream first, CancellationToken cancellation)
    {
        var server = first;
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                await server.WaitForConnectionAsync(cancellation).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
            {
                await server.DisposeAsync().ConfigureAwait(false);
                return;
            }

            Accepted++;
            var connected = server;

            // The next listener goes up before this connection is served, so a client is never
            // refused because the relay is busy with the one before it.
            server = CreateServer();
            _ = ServeAsync(connected, cancellation);
        }

        await server.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ServeAsync(NamedPipeServerStream client, CancellationToken cancellation)
    {
        IEngineChannel? channel = null;
        try
        {
            channel = _backend.Open();

            // Both directions, and the first one to end ends the pair: a response that completes
            // must close the client's read, and a client that hangs up must not leave the channel
            // holding a process.
            using var pair = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

            // The client's direction reads its HTTP on the way past, so a bind source spelled the
            // Windows way is respelled the distribution's (DD125). Everything it does not understand
            // it forwards byte for byte, so this stays a pipe. The daemon's direction is the plain
            // copy it always was: nothing in a response names a source this had to change.
            var toEngine = EngineRequestFilter.PumpAsync(client, channel.ToEngine, pair.Token);
            var toClient = Pump(channel.FromEngine, client, pair.Token);
            await Task.WhenAny(toEngine, toClient).ConfigureAwait(false);
            await pair.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(
                Swallow(toEngine), Swallow(toClient)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException
            or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            // One client's connection failing is not the relay failing.
        }
        finally
        {
            channel?.Dispose();
            try
            {
                if (client.IsConnected)
                {
                    client.Disconnect();
                }
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException
                or InvalidOperationException)
            {
                // Already gone.
            }

            await client.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task Pump(Stream from, Stream to, CancellationToken cancellation)
    {
        var buffer = new byte[16 * 1024];
        while (!cancellation.IsCancellationRequested)
        {
            var read = await from.ReadAsync(buffer, cancellation).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            await to.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);
            await to.FlushAsync(cancellation).ConfigureAwait(false);
        }
    }

    private static async Task Swallow(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException
            or ObjectDisposedException or OperationCanceledException)
        {
            // Expected: cancelling a pump is how a finished connection is torn down.
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_accepting is not null)
        {
            await Swallow(_accepting).ConfigureAwait(false);
        }

        _stopping.Dispose();
    }
}
