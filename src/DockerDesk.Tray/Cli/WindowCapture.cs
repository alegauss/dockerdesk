using System.IO;
using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;

namespace DockerDesk.Tray.Cli;

/// <summary>
/// Photographs the window by rendering it, off-screen, where nothing else can be in the frame.
/// </summary>
/// <remarks>
/// DD22. A window used to be verified by copying the pixels on screen inside its rectangle, which
/// reads whatever is actually there rather than the window: twice that was somebody else's content —
/// an editor holding a credential and a messaging app holding an appointment — and both reached a
/// transcript, which deleting the file afterwards does not undo.
///
/// This cannot do that. A <c>RenderTargetBitmap</c> over the window's own visual tree has no access to
/// anything outside it, and the window is shown at <c>-32000</c> so it is never composited onto a
/// desktop at all. That also means this works with no interactive desktop present, which a screen copy
/// does not — measured while shipping DD21, where a copy of the notification area came back as a
/// single flat colour on a locked session.
///
/// The screen copy is kept for the one thing a render cannot see: a popup is its own top-level window
/// and is not in this window's visual tree. That path lives in <c>scripts\Capture-Window.ps1</c>, with
/// the overlap check that decides whether it is safe to photograph anything at all.
/// </remarks>
internal static class WindowCapture
{
    private const int Ok = 0;
    private const int Failed = 1;
    private const int Usage = 2;

    /// <summary>How long to let the window settle before rendering it.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);

    /// <summary>Render the window to a PNG.</summary>
    /// <param name="args">The output path, then optionally a tab header.</param>
    /// <returns>The process exit code.</returns>
    internal static int Run(string[] args)
    {
        if (args.Length is 0 or > 2)
        {
            return Complain(
                "takes an output path and optionally a tab, "
                + $"e.g. {CommandLine.ExecutableName} {CommandLine.CaptureWindowVerb} window.png Images");
        }

        // A path that looks like a flag is a caller who forgot the path, and writing a file called
        // "--json" is how that mistake becomes silent. Borrowed from claude-tray, where exactly this
        // produced a file named after the flag next door.
        if (args[0].StartsWith('-'))
        {
            return Complain($"{args[0]} is not an output path");
        }

        var outPath = Path.GetFullPath(args[0]);
        var tab = args.Length == 2 ? args[1] : null;

        var app = new System.Windows.Application
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
        };

        // Not connected to anything, and it does not need to be: with no engine answering, the window
        // renders its own empty state, which is a picture of a real thing. A window drawn from a
        // fixture is DD38 and is a separate argument.
        var api = new DockerApi();
        var window = new Ui.MainWindow(api, () => EngineState.Stopped, () => { })
        {
            WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
            // Off the desktop entirely. Not merely unfocused: there is no screen region for anything
            // else to be composited into.
            Left = -32000,
            Top = -32000,
            // Off-screen there is no system backdrop to follow, so ThemeMode="System" would leave the
            // Fluent brushes unresolved and render light text on an unpainted surface.
            ThemeMode = System.Windows.ThemeMode.Dark,
        };

        var code = Failed;
        window.Show();

        if (tab is not null && !window.ShowTab(tab))
        {
            // Refused, not defaulted, and before any file exists. A name this window does not have
            // would otherwise render Containers into the file somebody asked to hold Images and report
            // success about it.
            Console.Error.WriteLine(
                $"{CommandLine.ExecutableName}: this window has no {tab} tab. It has: "
                + string.Join(", ", window.TabNames));
            app.Shutdown();
            return Usage;
        }

        // The same read the tray does when it opens the window, so the picture is of the window a user
        // gets rather than of one that was never asked to draw anything. With no engine this fails and
        // the window shows its own empty state, which is the honest thing to photograph — without it
        // the header row came back blank, seen by looking at the first capture.
        _ = window.RefreshAsync();

        var settle = new System.Windows.Threading.DispatcherTimer { Interval = Settle };
        settle.Tick += (_, _) =>
        {
            settle.Stop();
            try
            {
                var (width, height) = window.SaveSnapshot(outPath);
                Console.Out.WriteLine(
                    $"wrote {outPath} — {width}x{height}, "
                    + $"{tab ?? window.TabNames.FirstOrDefault() ?? "the window"} rendered off-screen");
                code = Ok;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException or InvalidOperationException
                or ArgumentException or NotSupportedException)
            {
                Console.Error.WriteLine($"{CommandLine.ExecutableName}: {exception.Message}");
                code = Failed;
            }
            finally
            {
                app.Shutdown();
            }
        };
        settle.Start();

        app.Run();
        api.Dispose();
        return code;
    }

    private static int Complain(string problem)
    {
        Console.Error.WriteLine(
            $"{CommandLine.ExecutableName} {CommandLine.CaptureWindowVerb}: {problem}");
        return Usage;
    }
}
