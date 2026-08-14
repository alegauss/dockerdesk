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
    /// <summary>Where WSL records every registered distribution.</summary>
    private const string LxssKey = @"Software\Microsoft\Windows\CurrentVersion\Lxss";

    /// <summary>Where the launcher lives.</summary>
    public static string LauncherPath =>
        Path.Combine(Environment.SystemDirectory, "wsl.exe");

    /// <summary>Every distribution registered on this machine.</summary>
    /// <returns>Their names, or empty where the key is absent or unreadable.</returns>
    /// <remarks>
    /// The registry and not <c>wsl --list --quiet</c>. Three reasons, all measured on this project:
    /// the subprocess costs a preflight that is already slow, its output is UTF-16LE and mis-decoding
    /// it reads as "WSL is not installed" on a machine where it is, and the registry answers while WSL
    /// is shut down — which is precisely the machine the rival row used to get wrong.
    ///
    /// <para>Here rather than in the rival probe that first needed it, because DD55 gave it a second
    /// caller: <see cref="EnginePaths"/> asks the same question to decide whether this install owns a
    /// distribution under the old name. Two readers of one registry key would be two chances to
    /// disagree about what is installed.</para>
    /// </remarks>
    public static IReadOnlyList<string> RegisteredDistributions()
    {
        try
        {
            using var lxss = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(LxssKey);
            if (lxss is null)
            {
                return [];
            }

            var names = new List<string>();
            foreach (var child in lxss.GetSubKeyNames())
            {
                using var distribution = lxss.OpenSubKey(child);
                if (distribution?.GetValue("DistributionName") is string name && name.Length > 0)
                {
                    names.Add(name);
                }
            }

            return names;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException
            or UnauthorizedAccessException or IOException)
        {
            // Unreadable is not the same as empty, and every caller here treats it the same way on
            // purpose: the rival row has three other signals, and an install that cannot read this
            // resolves to the current name, which is what a fresh machine would give anyway.
            return [];
        }
    }

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
