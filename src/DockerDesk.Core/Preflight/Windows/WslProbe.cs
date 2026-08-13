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

    /// <summary>
    /// What <c>wsl --status</c> exits with when the feature is not installed.
    /// </summary>
    /// <remarks>
    /// Measured on a fresh Windows 11 guest, build 26200, that never had WSL: <c>--status</c>
    /// returns 50 immediately, along with a localized sentence naming <c>wsl.exe --install</c>. The
    /// sentence is not read — it is translated — and the code is.
    ///
    /// Only this exact code means it. Any other non-zero answer falls through to <c>--version</c>
    /// and the behaviour this row always had, because a wrong "not installed" would be a worse
    /// defect than the slow row it replaced.
    /// </remarks>
    private const int FeatureNotInstalled = 50;

    /// <summary>Read the installation.</summary>
    /// <returns>What is there, and why anything missing could not be read.</returns>
    internal static WslInstallation Read()
    {
        if (!File.Exists(LauncherPath))
        {
            return new WslInstallation { CommandPresent = false };
        }

        return Interpret(
            ConsoleTool.Run(LauncherPath, "--status"),
            () => ConsoleTool.Run(LauncherPath, "--version"),
            () => File.Exists(InboxKernelPath),
            ReadDefaultVersion());
    }

    /// <summary>
    /// Turn what the commands said into facts, asking the second one only if the first left
    /// anything to ask.
    /// </summary>
    /// <param name="status">What <c>wsl --status</c> answered.</param>
    /// <param name="readVersion">
    /// How to run <c>wsl --version</c>. Deferred rather than passed as a value, because not running
    /// it is the point: on a machine that never had WSL that command hangs, and the fifteen seconds
    /// this row used to cost were its timeout.
    /// </param>
    /// <param name="inboxKernelPresent">Whether the kernel Windows ships in the box is on disk.</param>
    /// <param name="defaultVersion">What the registry says a new distro is created at.</param>
    /// <returns>The installation.</returns>
    internal static WslInstallation Interpret(
        ProcessOutput status,
        Func<ProcessOutput> readVersion,
        Func<bool> inboxKernelPresent,
        int? defaultVersion)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(readVersion);
        ArgumentNullException.ThrowIfNull(inboxKernelPresent);

        // `--status` itself not finishing is the one honest Unknown. Reaching for `--version` after
        // it would spend the timeout twice to learn the same nothing.
        if (status.Failure is not null)
        {
            return new WslInstallation
            {
                CommandPresent = true,
                DefaultVersion = defaultVersion,
                Unreadable = status.Failure,
            };
        }

        if (status.ExitCode == FeatureNotInstalled)
        {
            return new WslInstallation
            {
                CommandPresent = true,
                FeatureInstalled = false,
                DefaultVersion = defaultVersion,
            };
        }

        var reported = readVersion();

        if (reported.Failure is not null)
        {
            return new WslInstallation
            {
                CommandPresent = true,
                FeatureInstalled = true,
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
                    FeatureInstalled = true,
                    Version = versions[0].Value,
                    KernelVersion = versions[1].Value,
                    DefaultVersion = defaultVersion,
                };
            }
        }

        // An installation that predates `wsl --version` still has a kernel, and its version is
        // not reported anywhere a locale-independent read can reach. Say so rather than guess.
        // `--status` is the older command, so such a machine answered it and got here.
        if (inboxKernelPresent())
        {
            return new WslInstallation
            {
                CommandPresent = true,
                FeatureInstalled = true,
                KernelVersion = "bundled with Windows, version not reported",
                DefaultVersion = defaultVersion,
            };
        }

        return new WslInstallation
        {
            CommandPresent = true,
            FeatureInstalled = true,
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
