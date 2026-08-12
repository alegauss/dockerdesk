using System.Text;
using DockerDesk.Core.Api;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// A stream a test writes to line by line, and which can be ended on demand — because "the engine
/// went away mid-stream" is the case this whole class exists for.
/// </summary>
internal sealed class LiveStream : Stream
{
    private readonly System.Threading.Channels.Channel<byte[]> _chunks =
        System.Threading.Channels.Channel.CreateUnbounded<byte[]>();

    private ReadOnlyMemory<byte> _left;

    internal void WriteLine(string line) =>
        _chunks.Writer.TryWrite(Encoding.UTF8.GetBytes(line + "\n"));

    /// <summary>End the stream, as a daemon that stopped does.</summary>
    internal void End() => _chunks.Writer.TryComplete();

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => 0; set { } }

    public override void Flush()
    {
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_left.IsEmpty)
        {
            try
            {
                if (!await _chunks.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }
            }
            catch (System.Threading.Channels.ChannelClosedException)
            {
                return 0;
            }

            if (!_chunks.Reader.TryRead(out var chunk))
            {
                return 0;
            }

            _left = chunk;
        }

        var take = Math.Min(_left.Length, buffer.Length);
        _left[..take].CopyTo(buffer);
        _left = _left[take..];
        return take;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}

/// <summary>A source that hands out prepared streams, or refuses.</summary>
internal sealed class FakeEventSource : IEventStreamSource
{
    private readonly Queue<Func<Stream>> _streams = new();

    internal int Opened { get; private set; }

    internal FakeEventSource Serves(Stream stream)
    {
        _streams.Enqueue(() => stream);
        return this;
    }

    internal FakeEventSource Refuses()
    {
        _streams.Enqueue(() => throw new DockerApiException("the engine did not answer /events"));
        return this;
    }

    public Task<Stream> OpenAsync(CancellationToken cancellation)
    {
        Opened++;
        if (_streams.Count == 0)
        {
            throw new DockerApiException("nothing left to serve");
        }

        return Task.FromResult(_streams.Dequeue()());
    }
}

/// <summary>
/// The parsing, and the reconnecting. The second is the point: every break here is something that
/// happens in normal use, so the tests are mostly about the engine going away.
/// </summary>
public sealed class EngineEventsTests
{
    private static readonly string StartEvent = """
        {"status":"start","id":"d7c06c1092c59ff199799c6e94da3f4273f51cf4d9ac6ef44a8c496df175b562",
         "Type":"container","Action":"start",
         "Actor":{"ID":"d7c06c1092c59ff199799c6e94da3f4273f51cf4d9ac6ef44a8c496df175b562",
                  "Attributes":{"image":"nginx:alpine","name":"dd4-nginx"}},
         "scope":"local","time":1786000000,"timeNano":1786000000000000000}
        """.ReplaceLineEndings("");

    private static readonly string ExecEvent = """
        {"Type":"container","Action":"exec_start: /bin/sh",
         "Actor":{"ID":"abc","Attributes":{"name":"x"}},"time":1786000001}
        """.ReplaceLineEndings("");

    /// <summary>No real seconds: the backoff schedule is asserted on its own.</summary>
    private static Task NoWait(TimeSpan wait, CancellationToken cancellation) => Task.CompletedTask;

    private static async Task Until(Func<bool> done, string what)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            await Task.Delay(25);
        }

        Assert.True(done(), $"timed out waiting for {what}");
    }

    // ---- reading one line -------------------------------------------------------------------

    [Fact]
    public void An_event_is_read_down_to_its_container_name()
    {
        var parsed = System.Text.Json.JsonSerializer.Deserialize<DockerEvent>(StartEvent)!;

        Assert.Equal("container", parsed.Type);
        Assert.Equal("start", parsed.Action);
        Assert.Equal("dd4-nginx", parsed.Name);
        Assert.Equal("d7c06c1092c5", parsed.ShortId);
        Assert.True(parsed.ChangesTheContainerList);
    }

    [Theory]
    [InlineData("container", "create", true)]
    [InlineData("container", "die", true)]
    [InlineData("container", "destroy", true)]
    [InlineData("container", "exec_start: /bin/sh", false)]
    [InlineData("container", "health_status: healthy", false)]
    [InlineData("image", "pull", false)]
    [InlineData("network", "connect", false)]
    public void Only_what_changes_a_list_asks_for_a_refresh(string type, string action, bool refresh)
    {
        // Refreshing on everything turns an event stream back into a poll with extra steps.
        var parsed = new DockerEvent { Type = type, Action = action };

        Assert.Equal(refresh, parsed.ChangesTheContainerList);
    }

    [Fact]
    public void An_event_with_no_name_attribute_has_none_rather_than_an_empty_string()
    {
        var parsed = new DockerEvent { Actor = new EventActor { Id = "abc" } };

        Assert.Null(parsed.Name);
    }

    // ---- reading a live stream ---------------------------------------------------------------

    [Fact]
    public async Task Events_arrive_as_they_are_written_and_not_when_the_stream_ends()
    {
        var stream = new LiveStream();
        var seen = new List<DockerEvent>();
        await using var events = new EngineEvents(new FakeEventSource().Serves(stream), NoWait);
        events.Received += e => { lock (seen) { seen.Add(e); } };
        events.Start();

        await Until(() => events.State == EventStreamState.Watching, "the stream to open");
        stream.WriteLine(StartEvent);

        // The stream is still open here. If this only arrived at the end, a UI would be a poll.
        await Until(() => seen.Count == 1, "the first event");
        Assert.Equal("dd4-nginx", seen[0].Name);
        Assert.Equal(EventStreamState.Watching, events.State);
    }

    [Fact]
    public async Task A_line_this_build_cannot_read_is_counted_and_the_stream_survives()
    {
        // A daemon newer than the pinned API version saying something new must not be an outage.
        var stream = new LiveStream();
        var seen = 0;
        await using var events = new EngineEvents(new FakeEventSource().Serves(stream), NoWait);
        events.Received += _ => Interlocked.Increment(ref seen);
        events.Start();
        await Until(() => events.State == EventStreamState.Watching, "the stream to open");

        stream.WriteLine("{ this is not json");
        stream.WriteLine(StartEvent);

        await Until(() => Volatile.Read(ref seen) == 1, "the readable event");
        Assert.Equal(1, events.Unreadable);
        Assert.Equal(EventStreamState.Watching, events.State);
    }

    [Fact]
    public async Task An_exec_event_is_delivered_and_simply_does_not_ask_for_a_refresh()
    {
        var stream = new LiveStream();
        var seen = new List<DockerEvent>();
        await using var events = new EngineEvents(new FakeEventSource().Serves(stream), NoWait);
        events.Received += e => { lock (seen) { seen.Add(e); } };
        events.Start();
        await Until(() => events.State == EventStreamState.Watching, "the stream to open");

        stream.WriteLine(ExecEvent);

        await Until(() => seen.Count == 1, "the exec event");
        Assert.False(seen[0].ChangesTheContainerList);
    }

    // ---- the engine going away ---------------------------------------------------------------

    [Fact]
    public async Task A_stream_that_ends_is_re_opened_rather_than_reported_as_an_error()
    {
        var first = new LiveStream();
        var second = new LiveStream();
        var source = new FakeEventSource().Serves(first).Serves(second);
        var states = new List<EventStreamState>();
        await using var events = new EngineEvents(source, NoWait);
        events.StateChanged += s => { lock (states) { states.Add(s); } };
        events.Start();
        await Until(() => events.State == EventStreamState.Watching, "the first stream");

        first.End();

        await Until(() => source.Opened == 2, "the reconnect");
        Assert.Equal(1, events.Reconnects);
        lock (states)
        {
            Assert.Contains(EventStreamState.Reconnecting, states);
        }
    }

    [Fact]
    public async Task An_engine_that_is_not_there_is_retried_and_never_throws()
    {
        // The engine being stopped is the normal state of this tool between tasks.
        var stream = new LiveStream();
        var source = new FakeEventSource().Refuses().Refuses().Serves(stream);
        await using var events = new EngineEvents(source, NoWait);
        events.Start();

        await Until(() => events.State == EventStreamState.Watching, "the third attempt to connect");
        Assert.Equal(3, source.Opened);
        Assert.Equal(2, events.Reconnects);
    }

    [Fact]
    public async Task Events_keep_arriving_after_a_reconnect()
    {
        var first = new LiveStream();
        var second = new LiveStream();
        var seen = 0;
        var source = new FakeEventSource().Serves(first).Serves(second);
        await using var events = new EngineEvents(source, NoWait);
        events.Received += _ => Interlocked.Increment(ref seen);
        events.Start();
        await Until(() => events.State == EventStreamState.Watching, "the first stream");

        first.WriteLine(StartEvent);
        await Until(() => Volatile.Read(ref seen) == 1, "the event before the break");
        first.End();
        await Until(() => source.Opened == 2, "the reconnect");
        second.WriteLine(StartEvent);

        await Until(() => Volatile.Read(ref seen) == 2, "the event after the break");
    }

    [Fact]
    public async Task Disposing_stops_the_loop_and_leaves_it_Stopped()
    {
        var stream = new LiveStream();
        var source = new FakeEventSource().Serves(stream);
        var events = new EngineEvents(source, NoWait);
        events.Start();
        await Until(() => events.State == EventStreamState.Watching, "the stream to open");

        await events.DisposeAsync();

        Assert.Equal(EventStreamState.Stopped, events.State);
    }

    [Fact]
    public async Task Starting_twice_is_refused()
    {
        await using var events = new EngineEvents(
            new FakeEventSource().Serves(new LiveStream()), NoWait);
        events.Start();

        Assert.Throws<InvalidOperationException>(events.Start);
    }

    // ---- the backoff ------------------------------------------------------------------------

    [Fact]
    public void The_first_retry_is_quick_because_a_restarting_engine_is_back_in_a_moment() =>
        Assert.Equal(TimeSpan.FromSeconds(1), EngineEvents.BackoffFor(1));

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void The_backoff_doubles(int attempt, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), EngineEvents.BackoffFor(attempt));

    [Theory]
    [InlineData(6)]
    [InlineData(20)]
    [InlineData(10_000)]
    public void The_backoff_is_capped_so_an_engine_stopped_for_the_afternoon_costs_nothing(
        int attempt) =>
        Assert.Equal(EngineEvents.MaximumBackoff, EngineEvents.BackoffFor(attempt));
}
