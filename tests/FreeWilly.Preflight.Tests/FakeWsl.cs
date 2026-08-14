using System.Text;
using FreeWilly.Core.Engine;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// A <c>wsl.exe</c> that records what it was asked and answers what a test says. Importing a
/// distribution cannot be undone inside a test run, so what is asserted is the invocation.
/// </summary>
internal sealed class FakeWsl : IWsl
{
    private readonly Queue<WslResult> _answers = new();

    /// <summary>Every argument list this was called with, in order.</summary>
    internal List<string[]> Invocations { get; } = [];

    /// <summary>The budget each of those calls named, in the same order (DD122).</summary>
    internal List<TimeSpan> Budgets { get; } = [];

    /// <summary>Whatever is left unconsumed answers success with no output.</summary>
    internal WslResult Default { get; set; } = new(0, "", null);

    /// <summary>Queue the next answer.</summary>
    internal FakeWsl Answer(int? exitCode, string output = "", string? failure = null)
    {
        _answers.Enqueue(new WslResult(exitCode, output, failure));
        return this;
    }

    /// <inheritdoc/>
    public WslResult Run(TimeSpan budget, params string[] arguments)
    {
        Invocations.Add(arguments);
        Budgets.Add(budget);
        return _answers.Count > 0 ? _answers.Dequeue() : Default;
    }

    /// <summary>The invocation whose first argument is <paramref name="verb"/>, or null.</summary>
    internal string[]? WithVerb(string verb) =>
        Invocations.FirstOrDefault(argv => argv.Length > 0 && argv[0] == verb);

    /// <summary>The budget the call whose first argument is <paramref name="verb"/> named.</summary>
    /// <param name="verb">The first argument, e.g. <c>--import</c>.</param>
    /// <returns>Its budget, or null where no call opened with that argument.</returns>
    internal TimeSpan? BudgetForVerb(string verb)
    {
        var at = Invocations.FindIndex(argv => argv.Length > 0 && argv[0] == verb);
        return at < 0 ? null : Budgets[at];
    }
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
