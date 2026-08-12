using System.IO.Pipes;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DockerDesk.Core.Api;

/// <summary>The engine refused a call, and said why.</summary>
/// <param name="message">What went wrong, as a sentence naming the endpoint.</param>
/// <param name="status">The HTTP status, when there was one.</param>
/// <param name="detail">The sentence the daemon itself put in the body, if it put one there.</param>
public sealed class DockerApiException(
    string message, HttpStatusCode? status = null, string? detail = null)
    : Exception(message)
{
    /// <summary>The status the daemon returned, or <see langword="null"/> if it never answered.</summary>
    public HttpStatusCode? Status { get; } = status;

    /// <summary>
    /// What the daemon said, with none of this client's framing around it.
    /// </summary>
    /// <remarks>
    /// <see cref="Exception.Message"/> names the endpoint because a log needs to know which call
    /// this was. A row does not: the user pressed the button, so sixty characters of URL before
    /// "port is already allocated" is this tool talking over the engine.
    /// </remarks>
    public string? Detail { get; } = string.IsNullOrWhiteSpace(detail) ? null : detail;
}

/// <summary>
/// The Engine API over <c>\\.\pipe\docker_engine</c>. HTTP, JSON, and nothing from NuGet.
/// </summary>
/// <remarks>
/// The whole transport is a <see cref="NamedPipeClientStream"/> handed to
/// <see cref="SocketsHttpHandler.ConnectCallback"/>, which is why this needs no dependency: .NET
/// already speaks HTTP over any stream somebody can open.
///
/// Shelling out to <c>docker.exe</c> is the alternative and it is worse in ways that show up on the
/// first refresh — a process per call, text output that changes between versions, and no way to read
/// a streaming endpoint without owning a child's stdout. <see cref="StreamAsync"/> exists because of
/// that last one.
/// </remarks>
public sealed class DockerApi : IDisposable
{
    /// <summary>The pipe the engine serves on Windows.</summary>
    public const string DefaultPipeName = "docker_engine";

    /// <summary>
    /// The API version every request is made against.
    /// </summary>
    /// <remarks>
    /// Pinned rather than omitted. An unversioned path is answered with the daemon's newest version,
    /// so a daemon upgrade can change a response shape under a client that asked for nothing. This
    /// floor is old enough that any engine this project installs answers it, and new enough for
    /// every field read here.
    /// </remarks>
    public const string ApiVersion = "v1.43";

    /// <summary>
    /// How long opening the pipe may take before the engine counts as absent.
    /// </summary>
    private const int ConnectTimeoutMs = 2000;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly string _pipeName;

    /// <summary>Construct a client.</summary>
    /// <param name="pipeName">The pipe to talk to; overridden in tests.</param>
    /// <param name="timeout">How long any one call may take. Streaming calls ignore it.</param>
    public DockerApi(string pipeName = DefaultPipeName, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        _pipeName = pipeName;

        var handler = new SocketsHttpHandler
        {
            ConnectCallback = ConnectAsync,

            // The Engine API is a local socket; a pool that keeps connections for two minutes holds
            // pipe handles nothing is going to use again.
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(20),
        };

        // A host is required to build a request URI and is never resolved: the callback above is
        // what decides where the bytes go.
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(20),
        };
    }

    private async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context, CancellationToken cancellation)
    {
        var pipe = new NamedPipeClientStream(
            ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            // Bounded, because ConnectAsync with no timeout waits for the pipe to *appear* rather
            // than failing when it is absent. Unbounded, a call against a stopped engine burns the
            // whole request budget and then surfaces as a timeout, which reads like a slow daemon
            // instead of no daemon.
            await pipe.ConnectAsync(ConnectTimeoutMs, cancellation).ConfigureAwait(false);
            return pipe;
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Whether the engine answers at all.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns><see langword="true"/> when it replied 200.</returns>
    public async Task<bool> PingAsync(CancellationToken cancellation = default)
    {
        try
        {
            using var response = await _http.GetAsync(Path("_ping"), cancellation)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException or TaskCanceledException or TimeoutException)
        {
            return false;
        }
    }

    /// <summary>What the daemon says about itself.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>Its versions and platform.</returns>
    public Task<EngineVersion> VersionAsync(CancellationToken cancellation = default) =>
        GetAsync<EngineVersion>("version", cancellation);

    /// <summary>Every container the daemon knows about.</summary>
    /// <param name="all">
    /// <see langword="true"/> for stopped ones too. The list a user opens this tool for includes
    /// the container that exited immediately, so this is normally true.
    /// </param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The containers, in the order the daemon returned them.</returns>
    public async Task<IReadOnlyList<ContainerSummary>> ContainersAsync(
        bool all = true, CancellationToken cancellation = default)
    {
        var query = all ? "containers/json?all=1" : "containers/json";
        return await GetAsync<List<ContainerSummary>>(query, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// Everything the daemon knows about one container.
    /// </summary>
    /// <remarks>
    /// Read for one field: whether a TTY was allocated. That is not on the list endpoint, and it is
    /// what decides whether the log stream carries frame headers — so a log window asks this once
    /// before it opens the stream, rather than guessing from the bytes that arrive.
    /// </remarks>
    /// <param name="id">The container's id.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>What the daemon said.</returns>
    public Task<ContainerInspect> InspectAsync(string id, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return GetAsync<ContainerInspect>(
            $"containers/{Uri.EscapeDataString(id)}/json", cancellation);
    }

    /// <summary>
    /// Open a container's log and keep it open.
    /// </summary>
    /// <remarks>
    /// The same streaming shape as <c>/events</c>, so it goes through <see cref="StreamAsync"/>.
    /// <paramref name="tail"/> is what makes opening a window on a container that has been running
    /// for a week finish at all: without it the daemon replays the entire log first.
    /// </remarks>
    /// <param name="id">The container's id.</param>
    /// <param name="tail">How many lines of history to open with.</param>
    /// <param name="follow">Whether to keep the stream open for new output.</param>
    /// <param name="cancellation">Cancellation. Closing it is how the stream ends.</param>
    /// <returns>The response body, framed unless the container has a TTY.</returns>
    public Task<Stream> LogsAsync(
        string id, int tail = 2000, bool follow = true, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegative(tail);
        return StreamAsync(
            $"containers/{Uri.EscapeDataString(id)}/logs"
            + $"?stdout=1&stderr=1&tail={tail}&follow={(follow ? 1 : 0)}",
            cancellation);
    }

    /// <summary>Start a container.</summary>
    /// <param name="id">The container's id.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    public Task StartContainerAsync(string id, CancellationToken cancellation = default) =>
        ActOnAsync(HttpMethod.Post, id, "start", cancellation);

    /// <summary>
    /// Stop a container, giving it the daemon's default grace period before the kill.
    /// </summary>
    /// <remarks>
    /// No <c>t</c> parameter, so the daemon's own ten seconds apply. Shortening it here would make
    /// this tool stop containers differently from every other Docker client on the machine, and the
    /// wait is the reason the row goes to a pending state rather than the reason to cut it short.
    /// </remarks>
    /// <param name="id">The container's id.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    public Task StopContainerAsync(string id, CancellationToken cancellation = default) =>
        ActOnAsync(HttpMethod.Post, id, "stop", cancellation);

    /// <summary>Restart a container.</summary>
    /// <param name="id">The container's id.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    public Task RestartContainerAsync(string id, CancellationToken cancellation = default) =>
        ActOnAsync(HttpMethod.Post, id, "restart", cancellation);

    /// <summary>Remove a container.</summary>
    /// <remarks>
    /// <paramref name="force"/> is what makes removing a running container possible at all: without
    /// it the daemon answers 409 and the container stays. The caller is the one that knows whether
    /// the user was asked, so this does not decide it.
    /// </remarks>
    /// <param name="id">The container's id.</param>
    /// <param name="force">Whether to kill it first.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task that completes when the daemon accepted the call.</returns>
    public Task RemoveContainerAsync(
        string id, bool force = false, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return SendAsync(
            HttpMethod.Delete, $"containers/{Uri.EscapeDataString(id)}?force={(force ? 1 : 0)}",
            cancellation);
    }

    private Task ActOnAsync(
        HttpMethod method, string id, string verb, CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return SendAsync(method, $"containers/{Uri.EscapeDataString(id)}/{verb}", cancellation);
    }

    private async Task SendAsync(
        HttpMethod method, string path, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, Path(path));
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellation).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            (exception is HttpRequestException or IOException or TimeoutException
             || (exception is TaskCanceledException && !cancellation.IsCancellationRequested)))
        {
            throw new DockerApiException(
                $"the engine did not answer {Path(path)}: {exception.Message}");
        }

        using (response)
        {
            // 304 is the daemon saying the container is already in the state that was asked for.
            // Nothing happened, and nothing needed to: surfacing that as a failure would put "not
            // modified" on a row that already reads the way the user wanted it to.
            if (response.StatusCode is HttpStatusCode.NotModified)
            {
                return;
            }

            await ThrowIfRefusedAsync(response, path, cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Open a streaming endpoint and hand back its body, unread.
    /// </summary>
    /// <remarks>
    /// For <c>/events</c> and container logs: endpoints that never end. The response is not buffered,
    /// so the caller reads frames as the daemon writes them — which is the thing shelling out to a
    /// CLI cannot do without owning a child process's stdout.
    /// </remarks>
    /// <param name="path">The endpoint, without a leading slash or version.</param>
    /// <param name="cancellation">Cancellation. Closing it is how the stream ends.</param>
    /// <returns>The response body.</returns>
    public async Task<Stream> StreamAsync(string path, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var request = new HttpRequestMessage(HttpMethod.Get, Path(path));
        HttpResponseMessage response;
        try
        {
            response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            (exception is HttpRequestException or IOException or TimeoutException
             || (exception is TaskCanceledException && !cancellation.IsCancellationRequested)))
        {
            throw new DockerApiException(
                $"the engine did not answer {Path(path)}: {exception.Message}");
        }

        await ThrowIfRefusedAsync(response, path, cancellation).ConfigureAwait(false);
        return await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellation)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(Path(path), cancellation).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            (exception is HttpRequestException or IOException or TimeoutException
             || (exception is TaskCanceledException && !cancellation.IsCancellationRequested)))
        {
            // Naming the endpoint matters: "the pipe is not there" is one failure and "this call was
            // refused" is another, and a UI that shows the same message for both teaches nothing.
            // TaskCanceledException is in the list because that is what HttpClient raises when its
            // own timeout elapses, and letting it out raw is the same failure with no endpoint in it.
            // A cancellation the caller asked for is not caught: that one belongs to them.
            throw new DockerApiException(
                $"the engine did not answer {Path(path)}: {exception.Message}");
        }

        using (response)
        {
            await ThrowIfRefusedAsync(response, path, cancellation).ConfigureAwait(false);
            try
            {
                return await response.Content
                    .ReadFromJsonAsync<T>(Json, cancellation).ConfigureAwait(false)
                    ?? throw new DockerApiException($"{Path(path)} returned null");
            }
            catch (JsonException exception)
            {
                throw new DockerApiException(
                    $"{Path(path)} returned something that is not the JSON expected: "
                    + exception.Message,
                    response.StatusCode);
            }
        }
    }

    private static async Task ThrowIfRefusedAsync(
        HttpResponseMessage response, string path, CancellationToken cancellation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // The daemon puts a sentence in the body, and it is almost always the useful part.
        var body = "";
        try
        {
            body = (await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false))
                .Trim();
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException)
        {
            // Nothing readable; the status is what there is.
        }

        var said = body.Length == 0 ? "" : $": {Shorten(body)}";
        throw new DockerApiException(
            $"the engine answered {(int)response.StatusCode} {response.ReasonPhrase} "
            + $"for {ApiVersion}/{path}{said}",
            response.StatusCode,
            Shorten(body));
    }

    private static string Shorten(string text) =>
        text.Length <= 300 ? text : text[..300] + "…";

    private static string Path(string endpoint) => $"{ApiVersion}/{endpoint}";

    /// <inheritdoc/>
    public void Dispose() => _http.Dispose();
}
