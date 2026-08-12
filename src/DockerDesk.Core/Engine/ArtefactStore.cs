using System.Security.Cryptography;

namespace DockerDesk.Core.Engine;

/// <summary>Where the bytes of an artefact come from. The network, behind a seam.</summary>
public interface IArtefactFetcher
{
    /// <summary>Download <paramref name="url"/> to <paramref name="destination"/>.</summary>
    /// <param name="url">Where to read from.</param>
    /// <param name="destination">The file to write, overwritten if it exists.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>Nothing, or throws.</returns>
    Task FetchAsync(string url, string destination, CancellationToken cancellation);
}

/// <summary>What acquiring one artefact produced.</summary>
/// <param name="Artefact">Which artefact.</param>
/// <param name="Path">Where it now is, when it is verified.</param>
/// <param name="Cached">Whether it was already on disk and verified without downloading.</param>
/// <param name="Failure">Why there is no verified file, when that is what happened.</param>
public sealed record AcquiredArtefact(
    Artefact Artefact,
    string? Path,
    bool Cached,
    string? Failure)
{
    /// <summary>Whether there is a verified file at <see cref="Path"/>.</summary>
    public bool Verified => Failure is null && Path is not null;
}

/// <summary>
/// Downloads artefacts and refuses any whose digest is not the pinned one.
/// </summary>
/// <remarks>
/// The digest is checked against what is on disk, after the write, every time — including on a
/// cache hit. A file that arrived verified and was later truncated by a full disk is
/// indistinguishable from a correct one until it is read, and the read costs a second on a file
/// this size.
/// </remarks>
public sealed class ArtefactStore
{
    private readonly IArtefactFetcher _fetcher;
    private readonly string _directory;

    /// <summary>Construct a store over a directory.</summary>
    /// <param name="fetcher">How bytes are obtained.</param>
    /// <param name="directory">Where verified artefacts are kept.</param>
    public ArtefactStore(IArtefactFetcher fetcher, string directory)
    {
        ArgumentNullException.ThrowIfNull(fetcher);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _fetcher = fetcher;
        _directory = directory;
    }

    /// <summary>Get <paramref name="artefact"/> onto disk, verified.</summary>
    /// <param name="artefact">What to acquire.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>Where it is, or why it is not there.</returns>
    public async Task<AcquiredArtefact> AcquireAsync(
        Artefact artefact, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(artefact);
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, artefact.FileName);

        if (File.Exists(path))
        {
            var onDisk = await DigestAsync(path, cancellation).ConfigureAwait(false);
            if (Matches(onDisk, artefact.Sha256))
            {
                return new AcquiredArtefact(artefact, path, Cached: true, Failure: null);
            }

            // Not an error yet: a half-written file from an interrupted run is the common case, and
            // the answer to it is to fetch again rather than to stop.
            File.Delete(path);
        }

        try
        {
            await _fetcher.FetchAsync(artefact.Url, path, cancellation).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new AcquiredArtefact(artefact, null, false,
                $"downloading {artefact.Url} failed: {exception.Message}");
        }

        var digest = await DigestAsync(path, cancellation).ConfigureAwait(false);
        if (Matches(digest, artefact.Sha256))
        {
            return new AcquiredArtefact(artefact, path, Cached: false, Failure: null);
        }

        // Deleted, so a retry cannot find it and pass. A file that failed its digest is not
        // evidence of anything except that it must not be unpacked.
        File.Delete(path);
        return new AcquiredArtefact(artefact, null, false,
            $"{artefact.FileName} has digest {digest}, and this build pins {artefact.Sha256}");
    }

    /// <summary>The SHA-256 of a file, lower-case hex.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The digest.</returns>
    public static async Task<string> DigestAsync(
        string path, CancellationToken cancellation = default)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellation).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static bool Matches(string actual, string pinned) =>
        actual.Equals(pinned, StringComparison.OrdinalIgnoreCase);
}

/// <summary>An <see cref="IArtefactFetcher"/> over HTTP.</summary>
public sealed class HttpArtefactFetcher : IArtefactFetcher, IDisposable
{
    private readonly HttpClient _client;

    /// <summary>Construct a fetcher with its own client.</summary>
    public HttpArtefactFetcher()
        : this(new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
    {
    }

    /// <summary>Construct a fetcher over a supplied client.</summary>
    /// <param name="client">The client to use, owned by this instance.</param>
    public HttpArtefactFetcher(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc/>
    public async Task FetchAsync(string url, string destination, CancellationToken cancellation)
    {
        using var response = await _client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var target = File.Create(destination);
        await response.Content.CopyToAsync(target, cancellation).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => _client.Dispose();
}
