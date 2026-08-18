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

    /// <summary>
    /// How many times creating the next listener failed and had to be tried again (DD142).
    /// </summary>
    /// <remarks>
    /// Zero on every healthy run. It is public because the failure this counts used to be invisible:
    /// the pipe simply stopped existing, every client on the machine failed together, and nothing
    /// anywhere said why.
    /// </remarks>
    public int Stumbles { get; private set; }

    /// <summary>
    /// How the next listener is made. Replaced only by a test that needs creation to fail (DD142).
    /// </summary>
    /// <remarks>
    /// A delegate rather than an interface because there is exactly one caller and one thing to
    /// vary. The defect below cannot be reproduced any other way: it needs
    /// <see cref="NamedPipeServerStreamAcl.Create"/> to fail transiently, which is a thing the
    /// operating system does under load and a test cannot ask for.
    /// </remarks>
    internal Func<NamedPipeServerStream>? Listener { get; set; }

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
        if (Listener is { } made)
        {
            return made();
        }

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
            //
            // DD142. This used to be a bare call, and a throw from it ended the loop — which is the
            // one failure in this class that takes the whole machine's docker with it. The instance
            // that just connected is disposed when its connection finishes, and with the loop gone
            // nothing replaces it, so the pipe stops existing altogether: every client, at once,
            // reports "cannot find the file" rather than anything about an engine. Nothing observed
            // the faulted task either — it is awaited only in DisposeAsync — so the account of what
            // happened was a stack trace nobody ever read.
            var next = await NextListenerAsync(cancellation).ConfigureAwait(false);
            if (next is null)
            {
                // Cancelled while retrying. The connection in hand is still served; what stops is
                // the accepting, which is what cancellation asked for.
                _ = ServeAsync(connected, cancellation);
                return;
            }

            server = next;
            _ = ServeAsync(connected, cancellation);
        }

        await server.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Make the next listener, and keep trying while the machine refuses (DD142).</summary>
    /// <param name="cancellation">Stops the retrying, and nothing else.</param>
    /// <returns>The listener, or <see langword="null"/> where cancellation ended the wait.</returns>
    /// <remarks>
    /// Unbounded on purpose, and the alternative is what this replaces. A creation that fails is
    /// the machine being momentarily unable to give out a pipe instance — out of handles, or under
    /// a load that a compose run driving several clients at once is well able to produce — and it
    /// is a state that passes. Giving up after N attempts would restore exactly the defect being
    /// removed, only later and with a number attached to it.
    ///
    /// <para>The wait is short and flat rather than backing off. There is no server to be polite to
    /// here: this is a local object the kernel either can or cannot hand over, and every millisecond
    /// spent waiting is one where the docker command somebody has already typed is failing.</para>
    /// </remarks>
    private async Task<NamedPipeServerStream?> NextListenerAsync(CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                return CreateServer();
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException)
            {
                Stumbles++;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return null;
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
