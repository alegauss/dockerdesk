using FreeWilly.Core.Engine;

namespace FreeWilly.Core.Agent;

/// <summary>What the distribution says about one bind source (DD101).</summary>
/// <remarks>
/// Deliberately four answers and not two. <see cref="Unasked"/> is not a hedge — it is the state a
/// stopped distribution, a missing <c>wsl.exe</c> and a command that timed out all land in, and
/// folding it into <see cref="Missing"/> would report "this path is not there" about a question
/// nobody managed to ask. And <see cref="Empty"/> is kept apart from <see cref="Missing"/> because
/// the daemon erases the difference: it creates a source it cannot find rather than refusing, so
/// after one run the two look identical from Windows and only the distribution still knows.
/// </remarks>
public enum BindSource
{
    /// <summary>Nothing reached the distribution, so nothing is known.</summary>
    Unasked,

    /// <summary>The distribution does not have that path.</summary>
    Missing,

    /// <summary>It is there, and it holds nothing.</summary>
    Empty,

    /// <summary>It is there, and it holds something.</summary>
    Holds,
}

/// <summary>
/// Whether a bind source exists inside the distribution the engine runs in (DD101).
/// </summary>
/// <remarks>
/// The seam exists for the reason every other member of <see cref="MachineReads"/> does: this runs a
/// subprocess, and <c>read doctor</c> is measured to the token by <c>agent-budget.json</c>. A verb
/// that reached <c>wsl.exe</c> from inside its own body would make the benchmark's figure this
/// machine's again — which is DD78's whole finding, and DD98's warning that the seam was held by
/// memory alone. This is the first read that tests it.
/// </remarks>
public interface IBindSources
{
    /// <summary>Ask the distribution about one source.</summary>
    /// <param name="source">The source exactly as the daemon holds it.</param>
    /// <returns>What is there, or <see cref="BindSource.Unasked"/> where nothing could be asked.</returns>
    BindSource Look(string source);
}

/// <summary>The real read, which runs one shell in the distribution.</summary>
/// <remarks>
/// <b>The path is an argument, never text in the script.</b> It arrives from a container's own
/// configuration, so a source with a space, a quote or a <c>$</c> in it must not be able to change
/// what runs. <c>--exec</c> hands the arguments straight to the program rather than through the
/// distribution's login shell, and the script reads the source as <c>$1</c>.
///
/// <para><b>Builtins only.</b> The engine's rootfs is not a general-purpose Linux install and
/// nothing here should depend on what is in its <c>PATH</c> — so emptiness is decided by a glob and
/// <c>test</c> rather than by <c>ls</c>, and the dotfile pattern is included because a directory
/// holding only <c>.git</c> is not an empty one.</para>
///
/// <para><b>One shell per source, and that is the ceiling.</b> Only the sources nothing else could
/// settle are asked at all — a mapped drive is answered from Windows and another engine's spelling
/// is not ours to judge — so the common container asks none, and the container this task exists for
/// asks one.</para>
/// </remarks>
public sealed class BindSources : IBindSources
{
    private readonly IWsl _wsl;
    private readonly string _distribution;

    /// <summary>Construct the read against this machine's own engine distribution.</summary>
    public BindSources()
        : this(new Wsl(), EnginePaths.CurrentDistribution)
    {
    }

    /// <summary>Construct the read against a named distribution.</summary>
    /// <param name="wsl">The launcher.</param>
    /// <param name="distribution">The distribution the engine runs in.</param>
    public BindSources(IWsl wsl, string distribution)
    {
        ArgumentNullException.ThrowIfNull(wsl);
        ArgumentException.ThrowIfNullOrWhiteSpace(distribution);
        _wsl = wsl;
        _distribution = distribution;
    }

    /// <summary>The three words the script can print, and nothing else is read as an answer.</summary>
    internal const string Script =
        """
        [ -e "$1" ] || { echo missing; exit 0; }
        [ -d "$1" ] || { echo holds; exit 0; }
        for entry in "$1"/* "$1"/.[!.]* "$1"/..?*; do
          [ -e "$entry" ] && { echo holds; exit 0; }
        done
        echo empty
        """;

    /// <inheritdoc/>
    public BindSource Look(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return BindSource.Unasked;
        }

        var result = _wsl.Run("-d", _distribution, "--exec", "/bin/sh", "-c", Script, "sh", source);
        return result.Succeeded ? Read(result.Output) : BindSource.Unasked;
    }

    /// <summary>Turn what the shell printed into an answer.</summary>
    /// <param name="output">Everything the run wrote.</param>
    /// <returns>The answer, or <see cref="BindSource.Unasked"/> for anything unrecognised.</returns>
    /// <remarks>
    /// Trimmed and matched whole rather than searched for, because <c>wsl.exe</c> is entitled to
    /// print a warning of its own alongside — and a substring search for "missing" in a line that
    /// says a feature is missing would answer the wrong question about the user's code.
    /// </remarks>
    internal static BindSource Read(string? output) => (output ?? "").Trim() switch
    {
        "missing" => BindSource.Missing,
        "empty" => BindSource.Empty,
        "holds" => BindSource.Holds,
        _ => BindSource.Unasked,
    };
}
