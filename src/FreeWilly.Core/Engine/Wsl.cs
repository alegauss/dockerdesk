using FreeWilly.Core.Preflight.Windows;

namespace FreeWilly.Core.Engine;

/// <summary>What one <c>wsl.exe</c> invocation produced.</summary>
/// <param name="ExitCode">The exit code, or <see langword="null"/> if it never ran.</param>
/// <param name="Output">What it wrote, decoded.</param>
/// <param name="Failure">Why it never ran or never finished.</param>
public sealed record WslResult(int? ExitCode, string Output, string? Failure)
{
    /// <summary>Whether it ran and reported success.</summary>
    public bool Succeeded => Failure is null && ExitCode == 0;
}

/// <summary>
/// <c>wsl.exe</c>, behind a seam. Importing a distribution and unpacking an engine into it cannot
/// be done to a test machine on demand, so what the provisioner is tested on is the invocations it
/// builds rather than their effect.
/// </summary>
public interface IWsl
{
    /// <summary>Run <c>wsl.exe</c> with these arguments.</summary>
    /// <param name="arguments">The arguments, already split — never a command line to be parsed.</param>
    /// <returns>What it produced.</returns>
    WslResult Run(params string[] arguments);
}

/// <summary>The real <c>wsl.exe</c>.</summary>
public sealed class Wsl : IWsl
{
    /// <summary>Where the launcher lives.</summary>
    public static string LauncherPath =>
        Path.Combine(Environment.SystemDirectory, "wsl.exe");

    /// <inheritdoc/>
    public WslResult Run(params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var output = ConsoleTool.Run(LauncherPath, arguments);
        return new WslResult(output.ExitCode, output.Output, output.Failure);
    }

    /// <summary>
    /// The path a Windows file has inside a distribution, through the automatic drive mounts.
    /// </summary>
    /// <param name="windowsPath">A rooted Windows path, e.g. <c>C:\Users\x\y.tgz</c>.</param>
    /// <returns>The same file as <c>/mnt/c/Users/x/y.tgz</c>.</returns>
    /// <exception cref="ArgumentException">The path has no drive letter.</exception>
    public static string ToDistributionPath(string windowsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowsPath);
        var full = Path.GetFullPath(windowsPath);

        if (full.Length < 3 || full[1] != ':' || !char.IsAsciiLetter(full[0]))
        {
            throw new ArgumentException(
                $"a distribution reaches Windows files by drive letter, and '{windowsPath}' has none",
                nameof(windowsPath));
        }

        var drive = char.ToLowerInvariant(full[0]);
        var rest = full[2..].Replace('\\', '/').TrimStart('/');
        return $"/mnt/{drive}/{rest}";
    }

    /// <summary>
    /// The Windows path a distribution path came from, where it came from one.
    /// </summary>
    /// <param name="distributionPath">A path as the distribution spells it.</param>
    /// <returns>The Windows path, or <see langword="null"/> where this is not a mapped drive at all.</returns>
    /// <remarks>
    /// The reverse of <see cref="ToDistributionPath"/>, and it answers null rather than guessing. A path
    /// inside the distribution's own filesystem — <c>/var/lib/docker/volumes/…</c> — has no Windows
    /// equivalent, and neither does another engine's convention: Docker Desktop mounts the host under
    /// <c>/run/desktop/mnt/host/c</c>, which this deliberately does not recognise. Reporting "does not
    /// resolve" about a path this tool never mapped would be a false diagnosis, which is worse than no
    /// diagnosis (DD26).
    /// </remarks>
    public static string? ToWindowsPath(string? distributionPath)
    {
        if (string.IsNullOrWhiteSpace(distributionPath))
        {
            return null;
        }

        const string prefix = "/mnt/";
        if (!distributionPath.StartsWith(prefix, StringComparison.Ordinal)
            || distributionPath.Length < prefix.Length + 2
            || !char.IsAsciiLetter(distributionPath[prefix.Length]))
        {
            return null;
        }

        var after = distributionPath[(prefix.Length + 1)..];
        if (after.Length > 0 && after[0] != '/')
        {
            // /mnt/certificates is a directory inside the distribution, not drive C with a long name.
            return null;
        }

        var drive = char.ToUpperInvariant(distributionPath[prefix.Length]);
        return drive + ":" + (after.Length == 0 ? "\\" : after.Replace('/', '\\'));
    }
}
