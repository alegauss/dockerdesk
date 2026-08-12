using System.IO.Pipes;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DockerDesk.Core.Api;

/// <summary>The engine refused a call, and said why.</summary>
/// <param name="message">What went wrong, as a sentence naming the endpoint.</param>
/// <param name="status">The HTTP status, when there was one.</param>
public sealed class DockerApiException(string message, HttpStatusCode? status = null)
    : Exception(message)
{
    /// <summary>The status the daemon returned, or <see langword="null"/> if it never answered.</summary>
    public HttpStatusCode? Status { get; } = status;
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
            response.StatusCode);
    }

    private static string Shorten(string text) =>
        text.Length <= 300 ? text : text[..300] + "…";

    private static string Path(string endpoint) => $"{ApiVersion}/{endpoint}";

    /// <inheritdoc/>
    public void Dispose() => _http.Dispose();
}
