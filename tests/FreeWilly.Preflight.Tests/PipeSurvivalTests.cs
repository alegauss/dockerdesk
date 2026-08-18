using System.IO.Pipes;
using FreeWilly.Core.Engine;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The relay outliving a machine that momentarily will not hand out a pipe instance (DD142).
/// </summary>
/// <remarks>
/// The failure this is about arrives in bursts: every docker client on the machine fails together
/// with "cannot find the file", nothing is done, and moments later all of them work again. That
/// wording is not an engine that is down — it is a pipe name that does not exist at all, which is a
/// different fact and has exactly one cause on this side.
///
/// <para>The accept loop replaces its listener the instant one is taken, and used to do it with a
/// bare call. A throw from that call ended the loop; the connection in hand was disposed when it
/// finished; and nothing created another. The pipe stopped existing, and because the loop's task is
/// awaited only in <c>DisposeAsync</c>, the exception that did it was never observed by anything.
/// </para>
/// </remarks>
public sealed class PipeSurvivalTests
{
    private static string Pipe() => $"freewilly-survival-{Guid.NewGuid():N}";

    /// <summary>A backend that answers anything with one small response.</summary>
    private sealed class Answering : IEngineBackend
    {
        public IEngineChannel Open() => new Channel();

        private sealed class Channel : IEngineChannel
        {
            private readonly MemoryStream _in = new();

            public Stream ToEngine => _in;

            public Stream FromEngine { get; } = new MemoryStream(
                System.Text.Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"));

            public void Dispose()
            {
                _in.Dispose();
                FromEngine.Dispose();
            }
        }
    }

    /// <summary>Connect once, and say whether the pipe was there to connect to.</summary>
    private static async Task<bool> ReachableAsync(string pipe)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);

            // Short, because the question is whether the pipe exists rather than whether the
            // machine is busy. A generous timeout would hide the very state being asserted.
            await client.ConnectAsync(2000);
            return true;
        }
        catch (Exception exception) when (exception is TimeoutException or IOException)
        {
            return false;
        }
    }

    [Fact]
    public async Task A_machine_that_refuses_one_pipe_instance_does_not_take_docker_down_with_it()
    {
        // The defect, driven. Creation fails twice — which is what an operating system under load
        // does, and what no test can ask it for — and before DD142 the second connection below
        // found no pipe at all, for good, on a relay whose engine was perfectly healthy.
        var pipe = Pipe();
        var attempt = 0;

        await using var relay = new EnginePipeRelay(new Answering(), pipe);
        relay.Listener = () =>
        {
            // The first one is the listener Start creates, and it has to succeed — a relay that
            // cannot come up at all is a different failure with a caller to report it to. What this
            // test is about is the replacement, which happens where nobody is watching.
            attempt++;
            if (attempt is 2 or 3)
            {
                throw new IOException("all pipe instances are busy");
            }

            return NamedPipeServerStreamAcl.Create(
                pipe,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity: OnlyThisUser());
        };

        relay.Start();

        // The first connection takes the listener the start created, which is what makes the loop
        // reach for another one — and that is the call this test makes fail.
        Assert.True(await ReachableAsync(pipe), "the relay never served its first connection");

        // The one that used to find nothing. Retried past the refusals rather than abandoned, so
        // the pipe is still there a moment later.
        Assert.True(
            await ReachableAsync(pipe),
            "the pipe stopped existing after one failed listener, so every docker client on this "
            + "machine would fail together with \"cannot find the file\"");

        // And it is not silent any more. The count is the whole difference between a burst nobody
        // can explain and one that names itself.
        Assert.Equal(2, relay.Stumbles);
    }

    [Fact]
    public async Task The_host_can_read_the_count_without_reaching_inside_the_relay()
    {
        // The counter is only worth keeping if something says it out loud, and the host is the one
        // thing in a position to: it owns DD137's journal, and this is the event that leaves the
        // engine reading perfectly healthy while every docker client on the machine fails.
        //
        // A lifecycle that never started one answers zero rather than throwing — the supervisor
        // reads this on every turn of its loop, including the turns before there is a relay at all.
        await using var lifecycle = new EngineLifecycle(
            new FakeWsl(), new FakeDaemon(), new Answering());

        Assert.Equal(0, lifecycle.Stumbles);
    }

    [Fact]
    public async Task A_healthy_relay_stumbles_over_nothing()
    {
        // The other half, so the counter above cannot quietly become noise: a run with nothing
        // wrong reports nothing wrong, which is what makes a non-zero reading worth acting on.
        var pipe = Pipe();

        await using var relay = new EnginePipeRelay(new Answering(), pipe);
        relay.Start();

        Assert.True(await ReachableAsync(pipe));
        Assert.True(await ReachableAsync(pipe));
        Assert.Equal(0, relay.Stumbles);
    }

    private static System.IO.Pipes.PipeSecurity OnlyThisUser()
    {
        var security = new System.IO.Pipes.PipeSecurity();
        var self = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        security.AddAccessRule(new PipeAccessRule(
            self,
            PipeAccessRights.FullControl,
            System.Security.AccessControl.AccessControlType.Allow));
        return security;
    }
}
