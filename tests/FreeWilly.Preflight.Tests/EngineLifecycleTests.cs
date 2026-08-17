using System.IO.Pipes;
using System.Text;
using FreeWilly.Core.Engine;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// A backend that answers like a daemon, or refuses like a dead one.
/// </summary>
/// <remarks>
/// The reply is served only after a request arrives, which is not decoration: a channel that hands
/// back the reply regardless lets the ping succeed before the relay has finished forwarding, and an
/// assertion about what the backend received then passes or fails depending on scheduling. That is
/// how this test first went green alone and red in the suite.
/// </remarks>
internal sealed class FakeBackend(string? reply) : IEngineBackend
{
    private int _opened;

    internal int Opened => Volatile.Read(ref _opened);

    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _requests = new();

    internal string Received => string.Concat(_requests);

    public IEngineChannel Open()
    {
        Interlocked.Increment(ref _opened);
        if (reply is null)
        {
            throw new IOException("pretend the daemon socket is not there");
        }

        return new Channel(_requests, reply);
    }

    private sealed class Channel : IEngineChannel
    {
        private readonly SemaphoreSlim _requestArrived = new(0, 1);

        internal Channel(
            System.Collections.Concurrent.ConcurrentQueue<string> requests, string reply)
        {
            ToEngine = new RecordingStream(requests, _requestArrived);
            FromEngine = new ReplyStream(Encoding.ASCII.GetBytes(reply), _requestArrived);
        }

        public Stream ToEngine { get; }

        public Stream FromEngine { get; }

        public void Dispose()
        {
            ToEngine.Dispose();
            FromEngine.Dispose();
            _requestArrived.Dispose();
        }
    }

    /// <summary>Keeps what the relay forwarded, and releases the reply once something arrives.</summary>
    private sealed class RecordingStream(
        System.Collections.Concurrent.ConcurrentQueue<string> requests, SemaphoreSlim arrived)
        : WriteOnlyStream
    {
        private bool _released;

        public override void Write(byte[] buffer, int offset, int count)
        {
            requests.Enqueue(Encoding.ASCII.GetString(buffer, offset, count));
            if (!_released)
            {
                _released = true;
                arrived.Release();
            }
        }
    }

    /// <summary>Serves the reply, but not before a request has been forwarded.</summary>
    private sealed class ReplyStream(byte[] reply, SemaphoreSlim arrived) : Stream
    {
        private int _position;
        private bool _waited;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => reply.Length;

        public override long Position { get => _position; set { } }

        public override void Flush()
        {
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_waited)
            {
                await arrived.WaitAsync(cancellationToken).ConfigureAwait(false);
                _waited = true;
            }

            var left = reply.Length - _position;
            if (left <= 0)
            {
                return 0;
            }

            var take = Math.Min(left, buffer.Length);
            reply.AsMemory(_position, take).CopyTo(buffer);
            _position += take;
            return take;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}

/// <summary>The uninteresting half of a one-way stream.</summary>
internal abstract class WriteOnlyStream : Stream
{
    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => 0;

    public override long Position { get => 0; set { } }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}

/// <summary>A daemon that can be made to be dead.</summary>
internal sealed class FakeDaemon(bool aliveWhenLaunched = true) : IDaemonProcess
{
    internal int Launches { get; private set; }

    internal int Stops { get; private set; }

    public bool Alive { get; private set; }

    public void Launch()
    {
        Launches++;
        Alive = aliveWhenLaunched;
    }

    public void Stop()
    {
        Stops++;
        Alive = false;
    }

    public void Dispose() => Alive = false;
}

/// <summary>
/// The state machine and the relay. "Running" has to mean the engine answered, so the tests that
/// matter are the ones where something is up and the engine still is not.
/// </summary>
public sealed class EngineLifecycleTests
{
    private const string Ok200 =
        "HTTP/1.1 200 OK\r\nApi-Version: 1.55\r\nContent-Length: 2\r\n\r\nOK";

    private static string Pipe() => $"freewilly-test-{Guid.NewGuid():N}";

    /// <summary>The distribution these tests pretend this install owns.</summary>
    /// <remarks>
    /// Passed rather than resolved (DD55). The lifecycle otherwise asks the machine which name it
    /// owns, and a developer whose laptop carries an install made before the rename would run a
    /// suite that answers <c>dockerdesk</c> where every fixture here says <c>freewilly</c> — a test
    /// that passes or fails on what is registered outside the repository.
    /// </remarks>
    private const string Owned = "freewilly";

    // ---- reading a reply --------------------------------------------------------------------

    [Fact]
    public void A_200_with_an_api_version_is_the_engine_answering()
    {
        var result = EnginePing.Interpret(Ok200);

        Assert.True(result.Answered);
        Assert.Equal("1.55", result.ApiVersion);
    }

    [Fact]
    public void A_400_is_something_being_there_and_is_not_running()
    {
        // The daemon replying 400 proves it exists, and proves this code sent it nonsense. Calling
        // that Running would hide the defect — a BOM in front of the request line did exactly this.
        var result = EnginePing.Interpret("HTTP/1.1 400 Bad Request\r\n\r\n");

        Assert.False(result.Answered);
        Assert.Contains("400", result.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("HTTP/1.1 500 Internal Server Error\r\n\r\n")]
    [InlineData("HTTP/1.1 404 Not Found\r\n\r\n")]
    [InlineData("garbage that is not http at all")]
    [InlineData("")]
    public void Anything_that_is_not_a_success_is_not_running(string reply) =>
        Assert.False(EnginePing.Interpret(reply).Answered);

    // ---- the relay, over a real named pipe --------------------------------------------------

    [Fact]
    public async Task The_relay_carries_a_request_to_the_backend_and_the_reply_back()
    {
        var backend = new FakeBackend(Ok200);
        var pipe = Pipe();
        await using var relay = new EnginePipeRelay(backend, pipe);
        relay.Start();

        var ping = await EnginePing.AskAsync(pipe, TimeSpan.FromSeconds(10));

        Assert.True(ping.Answered, ping.Detail);
        Assert.Equal("1.55", ping.ApiVersion);
        Assert.Equal(1, backend.Opened);
        Assert.Contains("GET /_ping", backend.Received, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_request_reaches_the_backend_with_no_byte_order_mark_in_front_of_it()
    {
        // The defect this asserts against cost an afternoon: a UTF-8 writer puts EF BB BF before
        // the request line, and the daemon answers 400 to a request that looks perfect on screen.
        var backend = new FakeBackend(Ok200);
        var pipe = Pipe();
        await using var relay = new EnginePipeRelay(backend, pipe);
        relay.Start();

        await EnginePing.AskAsync(pipe, TimeSpan.FromSeconds(10));

        Assert.StartsWith("GET /_ping", backend.Received, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_relay_serves_more_than_one_connection()
    {
        var backend = new FakeBackend(Ok200);
        var pipe = Pipe();
        await using var relay = new EnginePipeRelay(backend, pipe);
        relay.Start();

        for (var i = 0; i < 3; i++)
        {
            Assert.True((await EnginePing.AskAsync(pipe, TimeSpan.FromSeconds(10))).Answered);
        }

        Assert.Equal(3, relay.Accepted);
    }

    [Fact]
    public async Task A_relay_whose_backend_refuses_leaves_the_pipe_answering_nothing()
    {
        // The pipe exists — the relay created it — and the engine is not there. This is the exact
        // gap that makes "does the pipe exist" the wrong question.
        var backend = new FakeBackend(null);
        var pipe = Pipe();
        await using var relay = new EnginePipeRelay(backend, pipe);
        relay.Start();

        var ping = await EnginePing.AskAsync(pipe, TimeSpan.FromSeconds(5));

        Assert.False(ping.Answered);
        Assert.Equal(1, backend.Opened);
    }

    [Fact]
    public async Task Starting_a_relay_twice_is_refused()
    {
        await using var relay = new EnginePipeRelay(new FakeBackend(Ok200), Pipe());
        relay.Start();

        Assert.Throws<InvalidOperationException>(relay.Start);
    }

    [Fact]
    public async Task Nothing_answers_a_pipe_no_relay_is_serving()
    {
        var ping = await EnginePing.AskAsync(Pipe(), TimeSpan.FromSeconds(2));

        Assert.False(ping.Answered);
    }

    // ---- the state machine -----------------------------------------------------------------

    [Fact]
    public async Task An_unregistered_distribution_is_stopped_and_says_to_install()
    {
        var wsl = new FakeWsl();
        wsl.Default = new WslResult(0, "Ubuntu\r\n", null);
        await using var engine = new EngineLifecycle(
            wsl, new FakeDaemon(), new FakeBackend(Ok200), Pipe(), Owned);

        var status = await engine.StartAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(EngineState.Stopped, status.State);
        Assert.Contains("not registered", status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_daemon_that_dies_while_starting_names_its_log_rather_than_timing_out()
    {
        var wsl = new FakeWsl();
        wsl.Default = new WslResult(0, "freewilly\r\n", null);
        await using var engine = new EngineLifecycle(
            wsl, new FakeDaemon(aliveWhenLaunched: false), new FakeBackend(null), Pipe(), Owned);

        var status = await engine.StartAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(EngineState.Stopped, status.State);
        Assert.Contains(EngineLifecycle.LogPath, status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_daemon_that_lives_and_never_answers_times_out_as_Starting()
    {
        // Not Running and not Stopped: something is up and the engine is not usable, which is the
        // state the whole enum exists to be able to say.
        var wsl = new FakeWsl();
        wsl.Default = new WslResult(0, "freewilly\r\n", null);
        await using var engine = new EngineLifecycle(
            wsl, new FakeDaemon(), new FakeBackend(null), Pipe(), Owned);

        var status = await engine.StartAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(EngineState.Starting, status.State);
        Assert.False(status.Usable);
        Assert.Contains("did not answer", status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_start_that_reaches_an_answering_engine_is_Running_with_its_api_version()
    {
        var wsl = new FakeWsl();
        wsl.Default = new WslResult(0, "freewilly\r\n", null);
        await using var engine = new EngineLifecycle(
            wsl, new FakeDaemon(), new FakeBackend(Ok200), Pipe(), Owned);

        var status = await engine.StartAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(EngineState.Running, status.State);
        Assert.True(status.Usable);
        Assert.Equal("1.55", status.ApiVersion);
    }

    [Fact]
    public async Task Stopping_stops_the_daemon_and_terminates_the_distribution()
    {
        var wsl = new FakeWsl();
        wsl.Default = new WslResult(0, "freewilly\r\n", null);
        var daemon = new FakeDaemon();
        await using var engine = new EngineLifecycle(
            wsl, daemon, new FakeBackend(Ok200), Pipe(), Owned);
        await engine.StartAsync(TimeSpan.FromSeconds(20));

        var status = await engine.StopAsync();

        Assert.Equal(EngineState.Stopped, status.State);
        Assert.Equal(1, daemon.Stops);
        Assert.Contains(
            wsl.Invocations,
            argv => argv.Length > 1 && argv[0] == "--terminate" && argv[1] == "freewilly");
    }

    [Fact]
    public async Task Stopping_what_was_never_started_says_so_and_does_nothing()
    {
        var wsl = new FakeWsl();
        wsl.Default = new WslResult(0, "Ubuntu\r\n", null);
        var daemon = new FakeDaemon();
        await using var engine = new EngineLifecycle(wsl, daemon, new FakeBackend(Ok200), Pipe(), Owned);

        var status = await engine.StopAsync();

        Assert.Equal(EngineState.Stopped, status.State);
        Assert.Contains("nothing was running", status.Detail, StringComparison.Ordinal);
        Assert.Equal(0, daemon.Stops);
    }

    [Fact]
    public async Task Status_reports_Running_only_when_the_engine_answers()
    {
        var wsl = new FakeWsl();
        wsl.Default = new WslResult(0, "freewilly\r\n", null);
        var pipe = Pipe();
        await using var relay = new EnginePipeRelay(new FakeBackend(Ok200), pipe);
        await using var engine = new EngineLifecycle(
            wsl, new FakeDaemon(), new FakeBackend(Ok200), pipe, Owned);

        var before = await engine.StatusAsync();
        relay.Start();
        var after = await engine.StatusAsync();

        Assert.Equal(EngineState.Stopped, before.State);
        Assert.Equal(EngineState.Running, after.State);
    }

    // ---- what a status is entitled to claim (DD134) ------------------------------------------

    [Fact]
    public async Task A_wsl_list_that_never_answered_is_not_reported_as_an_engine_that_is_gone()
    {
        // The half of DD134 that manufactured evidence out of load. `wsl --list` timing out used to
        // be folded into "not registered", which is the one Stopped the watch was entitled to act
        // on — so a busy machine could produce the verdict that terminates its own distribution.
        var wsl = new FakeWsl();
        wsl.Default = new WslResult(null, "", "wsl.exe did not answer within 15s");
        await using var engine = new EngineLifecycle(
            wsl, new FakeDaemon(), new FakeBackend(null), Pipe(), Owned);

        var status = await engine.StatusAsync();

        Assert.Equal(EngineState.Stopped, status.State);
        Assert.False(status.Conclusive);
        Assert.True(engine.DistributionRegistered, "a probe that did not answer is not a denial");
    }

    [Fact]
    public async Task A_daemon_this_host_launched_and_lost_is_conclusive()
    {
        // The handle is the witness that load cannot slow down or lie with, so once this lifecycle
        // owns a daemon it stops asking wsl anything on the poll path at all.
        var wsl = new FakeWsl();
        wsl.Default = new WslResult(0, "freewilly\r\n", null);
        var daemon = new FakeDaemon();
        await using var engine = new EngineLifecycle(
            wsl, daemon, new FakeBackend(null), Pipe(), Owned);
        await engine.StartAsync(TimeSpan.FromSeconds(2));

        daemon.Stop();
        var status = await engine.StatusAsync();

        Assert.Equal(EngineState.Stopped, status.State);
        Assert.True(status.Conclusive);
    }

    [Fact]
    public async Task A_launched_daemon_that_is_merely_quiet_is_never_conclusive()
    {
        // The daemon is up and the pipe said nothing, which is the exact reading the failure of
        // 17 August 2026 acted on. It has to stay an open question however many times it repeats.
        var wsl = new FakeWsl();
        wsl.Default = new WslResult(0, "freewilly\r\n", null);
        await using var engine = new EngineLifecycle(
            wsl, new FakeDaemon(), new FakeBackend(null), Pipe(), Owned);
        await engine.StartAsync(TimeSpan.FromSeconds(2));

        var status = await engine.StatusAsync();

        Assert.Equal(EngineState.Starting, status.State);
        Assert.False(status.Conclusive);
    }

    [Fact]
    public async Task An_unregistered_distribution_is_conclusive_because_the_probe_answered()
    {
        // The other side of the first test here: `wsl --list` ran, succeeded, and did not name the
        // distribution. That is a fact about the machine rather than about its load, and a host
        // that keeps serving a pipe for an engine which is not installed helps nobody.
        var wsl = new FakeWsl();
        wsl.Default = new WslResult(0, "Ubuntu\r\n", null);
        await using var engine = new EngineLifecycle(
            wsl, new FakeDaemon(), new FakeBackend(null), Pipe(), Owned);

        var status = await engine.StatusAsync();

        Assert.Equal(EngineState.Stopped, status.State);
        Assert.True(status.Conclusive);
        Assert.Contains("not registered", status.Detail, StringComparison.Ordinal);
    }
}
