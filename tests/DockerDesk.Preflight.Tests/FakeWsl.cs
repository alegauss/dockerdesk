using System.Text;
using DockerDesk.Core.Engine;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// A <c>wsl.exe</c> that records what it was asked and answers what a test says. Importing a
/// distribution cannot be undone inside a test run, so what is asserted is the invocation.
/// </summary>
internal sealed class FakeWsl : IWsl
{
    private readonly Queue<WslResult> _answers = new();

    /// <summary>Every argument list this was called with, in order.</summary>
    internal List<string[]> Invocations { get; } = [];

    /// <summary>Whatever is left unconsumed answers success with no output.</summary>
    internal WslResult Default { get; set; } = new(0, "", null);

    /// <summary>Queue the next answer.</summary>
    internal FakeWsl Answer(int? exitCode, string output = "", string? failure = null)
    {
        _answers.Enqueue(new WslResult(exitCode, output, failure));
        return this;
    }

    /// <inheritdoc/>
    public WslResult Run(params string[] arguments)
    {
        Invocations.Add(arguments);
        return _answers.Count > 0 ? _answers.Dequeue() : Default;
    }

    /// <summary>The invocation whose first argument is <paramref name="verb"/>, or null.</summary>
    internal string[]? WithVerb(string verb) =>
        Invocations.FirstOrDefault(argv => argv.Length > 0 && argv[0] == verb);
}

/// <summary>An <see cref="IArtefactFetcher"/> that writes bytes a test chose.</summary>
internal sealed class FakeFetcher : IArtefactFetcher
{
    private readonly Func<string, byte[]?> _bytesFor;

    /// <summary>Construct a fetcher.</summary>
    /// <param name="bytesFor">
    /// What to write for a URL. Returning null throws instead, which is how a download that failed
    /// rather than one that arrived wrong is injected.
    /// </param>
    internal FakeFetcher(Func<string, byte[]?> bytesFor) => _bytesFor = bytesFor;

    /// <summary>A fetcher that always writes this text.</summary>
    internal static FakeFetcher Writing(string content) =>
        new(_ => Encoding.UTF8.GetBytes(content));

    /// <summary>Every URL it was asked for.</summary>
    internal List<string> Requested { get; } = [];

    /// <inheritdoc/>
    public async Task FetchAsync(string url, string destination, CancellationToken cancellation)
    {
        Requested.Add(url);
        var bytes = _bytesFor(url)
            ?? throw new HttpRequestException($"pretend the network refused {url}");
        await File.WriteAllBytesAsync(destination, bytes, cancellation);
    }
}
