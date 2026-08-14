using FreeWilly.Core.Engine;

namespace FreeWilly.Tray.Cli;

/// <summary>
/// Hold the tray's context menu open so something can photograph it (DD67).
/// </summary>
/// <remarks>
/// No popup this product draws had ever been photographed. DD61 settled that
/// <c>--capture-window</c> renders the window's own visual tree and that
/// <c>scripts\Capture-Window.ps1</c> is the screen copy for a popup, because a popup is its own
/// top-level window and is not in that tree — and then made the script refuse the Fluent shell,
/// which is right and left it with nothing it could find. A menu exists only while it is open, and
/// nothing opened one.
///
/// <para><b>The driving is inside the process that owns the menu.</b> The alternative was a Win32
/// click against the notification area — reaching into another process's UI, and the Windows 11
/// overflow makes finding the icon its own problem (DD21). This costs one verb and no cross-process
/// input at all.</para>
///
/// <para><b>AutoClose is off, and that is the whole reason a verb beats a click.</b> A dropdown
/// dismisses itself the moment anything else takes focus, and a screen copy is something else taking
/// focus. Only the process that owns the menu can say otherwise.</para>
///
/// <para><b>No icon and no window</b> (L6). This shows a menu on a machine with no engine, nothing
/// installed and nothing in the notification area — the same law that lets every window here draw
/// without the thing it is about. It also leaves no tray icon behind when it exits.</para>
///
/// <para><b>It exits on its own.</b> A verb that holds a menu open forever is one a script can leave
/// running on somebody's desktop, so the deadline is the default rather than the flag.</para>
/// </remarks>
internal static class MenuPreview
{
    /// <summary>How long the menu stays up when nothing says otherwise.</summary>
    internal const int DefaultSeconds = 20;

    /// <summary>What the menu says about the engine when nothing names a state.</summary>
    /// <remarks>
    /// Stopped, which is what <c>--capture-window</c> defaults to and the state a machine with
    /// nothing installed is in — so the default run needs no engine to be truthful.
    /// </remarks>
    internal const EngineState DefaultState = EngineState.Stopped;

    /// <summary>Read this verb's arguments.</summary>
    /// <param name="arguments">Everything after the verb.</param>
    /// <param name="state">The state the menu should reflect.</param>
    /// <param name="seconds">How long to hold it open.</param>
    /// <param name="refusal">Why the arguments were refused, or null.</param>
    /// <returns><see langword="true"/> where they were understood.</returns>
    internal static bool TryRead(
        string[] arguments, out EngineState state, out int seconds, out string? refusal)
    {
        state = DefaultState;
        seconds = DefaultSeconds;
        refusal = null;

        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (string.Equals(argument, "--seconds", StringComparison.Ordinal))
            {
                if (i + 1 >= arguments.Length
                    || !int.TryParse(
                        arguments[++i],
                        System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out seconds)
                    || seconds is < 1 or > 600)
                {
                    refusal = "--seconds needs a whole number of seconds from 1 to 600";
                    return false;
                }

                continue;
            }

            if (Enum.TryParse(argument, ignoreCase: true, out EngineState named))
            {
                state = named;
                continue;
            }

            refusal = $"unexpected argument {argument}: "
                + $"{CommandLine.ShowMenuVerb} takes stopped, starting or running, and --seconds";
            return false;
        }

        return true;
    }

    /// <summary>Show the menu and hold it.</summary>
    /// <param name="arguments">Everything after the verb.</param>
    /// <returns>The process exit code.</returns>
    internal static int Run(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TryRead(arguments, out var state, out var seconds, out var refusal))
        {
            Console.Error.WriteLine($"{CommandLine.ExecutableName}: {refusal}");
            return 2;
        }

        ApplicationConfiguration.Initialize();

        // The menu the tray ships, built with the same class and handed nothing to do. A click here
        // would be a click on a preview, and every item's action is already somewhere a user can
        // reach it.
        var menu = new TrayMenu(Nothing, Nothing, Nothing, Nothing);
        menu.Reflect(state);
        menu.Strip.AutoClose = false;

        // Where Windows would put it: the bottom-right of the working area, which is where the
        // notification area is. The rectangle a copy reads comes from the window, so the position
        // decides what is behind the menu and nothing else — and the working area keeps it off the
        // taskbar, which is the one thing that would be behind it there.
        var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
        var size = menu.Strip.GetPreferredSize(Size.Empty);
        var at = new Point(
            Math.Max(work.Left, work.Right - size.Width - 8),
            Math.Max(work.Top, work.Bottom - size.Height - 8));

        using var deadline = new System.Windows.Forms.Timer { Interval = seconds * 1000 };
        deadline.Tick += (_, _) =>
        {
            deadline.Stop();
            menu.Strip.AutoClose = true;
            menu.Strip.Close();
            Application.ExitThread();
        };

        menu.Strip.Show(at);
        deadline.Start();

        Console.Out.WriteLine(
            $"menu open at ({at.X},{at.Y}), {size.Width}x{size.Height}, engine {state}, "
            + $"closing in {seconds}s");

        Application.Run();
        return 0;
    }

    private static void Nothing()
    {
        // A preview's items do nothing on purpose: see the class remarks.
    }
}
