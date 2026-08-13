using System.Text.Json;

namespace FreeWilly.Core.Api;

/// <summary>Where the event stream comes from. The seam this is testable through.</summary>
public interface IEventStreamSource
{
    /// <summary>Open <c>/events</c> and hand back the body, unread.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The stream, which ends when the engine does.</returns>
    Task<Stream> OpenAsync(CancellationToken cancellation);
}

/// <summary><c>/events</c> from a <see cref="DockerApi"/>.</summary>
public sealed class DockerApiEventSource(DockerApi api) : IEventStreamSource
{
    /// <inheritdoc/>
    public Task<Stream> OpenAsync(CancellationToken cancellation) =>
        api.StreamAsync("events", cancellation);
}

/// <summary>What the watcher is doing.</summary>
public enum EventStreamState
{
    /// <summary>Not started, or stopped on purpose.</summary>
    Stopped,

    /// <summary>Opening the stream.</summary>
    Connecting,

    /// <summary>Connected, and reading.</summary>
    Watching,

    /// <summary>The connection ended and the next attempt is waiting out a backoff.</summary>
    Reconnecting,
}

/// <summary>
/// Reads <c>/events</c> and keeps reading it. One job, and the job is reconnecting.
/// </summary>
/// <remarks>
/// The engine stopping, the distribution being terminated, a laptop suspending — each of these ends
/// the stream, and none of them is an error a user should be shown. So a break is a state and not an
/// exception: the loop backs off and tries again, and the engine indicator reports which state it is
/// in. What a user would rightly complain about is a list that quietly stopped being true.
/// </remarks>
public sealed class EngineEvents : IAsyncDisposable
{
    /// <summary>How long a backoff ever gets.</summary>
    public static readonly TimeSpan MaximumBackoff = TimeSpan.FromSeconds(30);

    private readonly IEventStreamSource _source;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;

    /// <summary>Construct a watcher.</summary>
    /// <param name="source">Where the stream comes from.</param>
    /// <param name="delay">How a backoff waits. Replaced in tests so they need no real seconds.</param>
    public EngineEvents(
        IEventStreamSource source,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _delay = delay ?? ((wait, cancellation) => Task.Delay(wait, cancellation));
    }

    /// <summary>Each event, in the order the daemon wrote it.</summary>
    public event Action<DockerEvent>? Received;

    /// <summary>Every state change, including the ones nobody should be shown as errors.</summary>
    public event Action<EventStreamState>? StateChanged;

    /// <summary>What the loop is doing.</summary>
    public EventStreamState State { get; private set; } = EventStreamState.Stopped;

    /// <summary>How many times the stream has been re-opened after ending.</summary>
    public int Reconnects { get; private set; }

    /// <summary>How many lines were read and could not be parsed.</summary>
    /// <remarks>
    /// Counted rather than thrown. A line this build cannot read is a daemon newer than the pinned
    /// API version saying something new, and dropping the stream over it would turn a forward-
    /// compatible protocol into an outage.
    /// </remarks>
    public int Unreadable { get; private set; }

    /// <summary>
    /// The wait before attempt <paramref name="attempt"/>, doubling from a second and capped.
    /// </summary>
    /// <remarks>
    /// The first retry is quick because the common break is the engine restarting and being back in
    /// a moment. The cap exists because the other common break is the engine being stopped for the
    /// afternoon, and a tight loop against a machine with nothing running is what a laptop's battery
    /// notices.
    /// </remarks>
    /// <param name="attempt">1 for the first retry.</param>
    /// <returns>How long to wait.</returns>
    public static TimeSpan BackoffFor(int attempt)
    {
        if (attempt <= 1)
        {
            return TimeSpan.FromSeconds(1);
        }

        var doubled = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(attempt - 1, 10)));
        return doubled > MaximumBackoff ? MaximumBackoff : doubled;
    }

    /// <summary>Start watching. Returns immediately; the loop runs until disposed.</summary>
    public void Start()
    {
        if (_loop is not null)
        {
            throw new InvalidOperationException("this watcher is already started");
        }

        _loop = Task.Run(() => WatchAsync(_stopping.Token));
    }

    private async Task WatchAsync(CancellationToken cancellation)
    {
        var attempt = 0;
        while (!cancellation.IsCancellationRequested)
        {
            Move(EventStreamState.Connecting);
            try
            {
                await using var stream = await _source.OpenAsync(cancellation).ConfigureAwait(false);
                attempt = 0;
                Move(EventStreamState.Watching);
                await ReadAsync(stream, cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is DockerApiException
                or IOException or ObjectDisposedException or HttpRequestException)
            {
                // The engine is not there, or stopped being there mid-stream. Both are states.
            }

            if (cancellation.IsCancellationRequested)
            {
                break;
            }

            Reconnects++;
            attempt++;
            Move(EventStreamState.Reconnecting);
            try
            {
                await _delay(BackoffFor(attempt), cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Move(EventStreamState.Stopped);
    }

    private async Task ReadAsync(Stream stream, CancellationToken cancellation)
    {
        // A StreamReader over the live body: the daemon writes one JSON object per line and never
        // closes, so every line has to be handed on as it arrives rather than after the response.
        using var reader = new StreamReader(stream);
        while (!cancellation.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellation).ConfigureAwait(false);
            if (line is null)
            {
                return;
            }

            if (line.Length == 0)
            {
                continue;
            }

            DockerEvent? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<DockerEvent>(line);
            }
            catch (JsonException)
            {
                Unreadable++;
                continue;
            }

            if (parsed is not null)
            {
                Received?.Invoke(parsed);
            }
        }
    }

    private void Move(EventStreamState next)
    {
        if (State == next)
        {
            return;
        }

        State = next;
        StateChanged?.Invoke(next);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException
                or ObjectDisposedException or IOException)
            {
                // Stopping.
            }
        }

        _stopping.Dispose();
    }
}
