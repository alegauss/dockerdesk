// WinForms and WPF both contribute an Application, and this file is about the WPF one. Pinned rather
// than qualified at every use, the same way Palette pins Brush and Color.
using System.Windows;
using Application = System.Windows.Application;

namespace DockerDesk.Tray.Ui;

/// <summary>
/// The one place an <see cref="Application"/> is made and the chrome is merged into it (DD34).
/// </summary>
/// <remarks>
/// A WinForms process has no <see cref="Application"/>, and WPF needs one before a window can resolve
/// theme resources. Two places needed that and both wrote it: the tray opening its window, and
/// <c>--capture-window</c> rendering one off-screen. That is the same defect this task is about, one
/// level up — so the construction, the shutdown mode and the merge are said here once, and both callers
/// ask for it rather than each knowing how.
///
/// <para><see cref="ShutdownMode.OnExplicitShutdown"/> is not a detail: the tray outlives its window,
/// and the default would take the process down with the last one closed.</para>
///
/// <para><b>ThemeMode is deliberately not here</b>, and a capture is why. It is a property of the
/// application as well as of a window, so saying it once here was the obvious move — and it renders
/// differently: wider control metrics and different button chrome, against a byte-identical baseline.
/// Setting it on each window from code, before or after <c>InitializeComponent</c>, does not reproduce
/// the markup attribute either. Four measurements, one conclusion: <c>ThemeMode="System"</c> stays on
/// each Window, which is the one thing DD34 did not deduplicate and the only one with a picture behind
/// the decision.</para>
/// </remarks>
internal static class Theme
{
    /// <summary>Where the shared chrome lives, as the pack URI both build configurations resolve.</summary>
    public const string Source = "pack://application:,,,/Ui/Theme.xaml";

    /// <summary>Make sure this process has an application carrying the chrome.</summary>
    /// <returns>The application, whether it was made here or already existed.</returns>
    public static Application Apply()
    {
        var app = Application.Current ?? new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };


        if (!app.Resources.MergedDictionaries.Any(d =>
            string.Equals(d.Source?.ToString(), Source, StringComparison.Ordinal)))
        {
            app.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri(Source, UriKind.Absolute) });
        }

        return app;
    }
}
