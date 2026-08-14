namespace FreeWilly.Tray.Cli;

/// <summary>
/// <c>--quit</c>: ask the tray running on this session to exit, and do not return until it has.
/// </summary>
/// <remarks>
/// The verb the uninstaller was missing (DD121). Windows will not delete an executable a process has
/// open, so an uninstall run while the tray is in the notification area removed the Run value, the
/// PATH entry and the Add/Remove Programs entry — and then failed on <c>FreeWilly.exe</c>, leaving a
/// root with no uninstaller left to take it.
///
/// <para><b>Nothing running is success.</b> The caller asked for a machine with no tray on it, and
/// that is what it has. Exit 1 is reserved for a tray that was asked and did not go, because that is
/// the one answer the uninstaller has to act on.</para>
///
/// <para><b>It does not touch the engine.</b> Quitting the tray leaves the engine exactly as it is —
/// the asymmetry <c>TrayApplication.Quit</c> is built around, and a container someone else is using
/// does not die because an icon closed. Stopping the engine is <c>--stop</c>, and an uninstall wants
/// both.</para>
/// </remarks>
internal static class QuitCommand
{
    /// <summary>
    /// How long a tray gets to go away before this reports that it did not.
    /// </summary>
    /// <remarks>
    /// Generous, and it costs nothing when it is not needed: the wait ends the moment the slot frees,
    /// so this bounds only the failing case. What it has to cover is a tray with the window open on a
    /// list mid-refresh, which unwinds an event stream and a Docker API client on its way out.
    /// </remarks>
    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(20);

    /// <summary>Run the verb.</summary>
    /// <param name="arguments">Everything after the verb, which has to be nothing.</param>
    /// <returns>0 where no tray is left running; 1 where one is.</returns>
    internal static int Run(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!SingleTray.AskTheLiveOneToQuit())
        {
            Console.Out.WriteLine($"  {"Stopped",-8}  no tray was running on this session");
            return 0;
        }

        if (SingleTray.WaitUntilTheTrayIsGone(Budget))
        {
            Console.Out.WriteLine($"  {"Stopped",-8}  the tray closed");
            return 0;
        }

        // Named as a fact rather than a remedy: the one caller that has a remedy is the uninstaller,
        // and forcing a process is its decision to announce, not this verb's to take.
        Console.Error.WriteLine(
            $"{CommandLine.ExecutableName}: the tray was asked to close and was still running after "
            + $"{Budget.TotalSeconds:0}s.");
        return 1;
    }
}
