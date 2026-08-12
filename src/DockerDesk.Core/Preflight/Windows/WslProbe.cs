using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DockerDesk.Core.Preflight.Windows;

/// <summary>Reads what is installed of WSL, without judging any of it.</summary>
internal static partial class WslProbe
{
    /// <summary>
    /// Where the launcher lives. Present on a modern Windows whether or not the feature is
    /// installed, so its absence is proof and its presence is not.
    /// </summary>
    private static string LauncherPath =>
        Path.Combine(Environment.SystemDirectory, "wsl.exe");

    /// <summary>
    /// The kernel Windows ships in the box, for an installation too old for <c>wsl --version</c>.
    /// </summary>
    private static string InboxKernelPath =>
        Path.Combine(Environment.SystemDirectory, "lxss", "tools", "kernel");

    /// <summary>Where WSL records the version a new distro is created at.</summary>
    private const string LxssKey = @"Software\Microsoft\Windows\CurrentVersion\Lxss";

    [GeneratedRegex(@"\d+(?:\.\d+){2,3}", RegexOptions.CultureInvariant)]
    private static partial Regex VersionToken { get; }

    /// <summary>Read the installation.</summary>
    /// <returns>What is there, and why anything missing could not be read.</returns>
    internal static WslInstallation Read()
    {
        if (!File.Exists(LauncherPath))
        {
            return new WslInstallation { CommandPresent = false };
        }

        var defaultVersion = ReadDefaultVersion();
        var reported = ConsoleTool.Run(LauncherPath, "--version");

        if (reported.Failure is not null)
        {
            return new WslInstallation
            {
                CommandPresent = true,
                DefaultVersion = defaultVersion,
                Unreadable = reported.Failure,
            };
        }

        if (reported.Succeeded)
        {
            // `wsl --version` prints one labelled version per line, and the labels are localized
            // while the order is not: WSL first, then the kernel. So the tokens are read by
            // position, which is the only part of that output every locale agrees on.
            var versions = VersionToken.Matches(reported.Output);
            if (versions.Count >= 2)
            {
                return new WslInstallation
                {
                    CommandPresent = true,
                    Version = versions[0].Value,
                    KernelVersion = versions[1].Value,
                    DefaultVersion = defaultVersion,
                };
            }
        }

        // An installation that predates `wsl --version` still has a kernel, and its version is
        // not reported anywhere a locale-independent read can reach. Say so rather than guess.
        if (File.Exists(InboxKernelPath))
        {
            return new WslInstallation
            {
                CommandPresent = true,
                KernelVersion = "bundled with Windows, version not reported",
                DefaultVersion = defaultVersion,
            };
        }

        return new WslInstallation
        {
            CommandPresent = true,
            DefaultVersion = defaultVersion,
        };
    }

    private static int? ReadDefaultVersion()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(LxssKey);
            return key?.GetValue("DefaultVersion") as int?;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException
            or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
