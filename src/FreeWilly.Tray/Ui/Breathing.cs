using System.Windows;
using System.Windows.Media.Animation;

namespace FreeWilly.Tray.Ui;

/// <summary>
/// The engine dot breathes while the engine is starting, and stops the moment it is not (DD70).
/// </summary>
/// <remarks>
/// Starting is the one state here that is a <em>wait</em> rather than a settled answer, and until
/// this the only thing saying so was the colour amber. Fluent's motion exists to answer "did that
/// change, or did I misread it?", and a pending state is that question in its purest form.
///
/// <para>It invents nothing. The label beside the dot already reads "Engine starting"; this is the
/// same fact a second way, which is the order the window constitution sets — shape and word first,
/// motion only reinforcing.</para>
///
/// <para><b>Its own file rather than the shell's code-behind.</b> A guard holds `MainWindow.xaml.cs`
/// under 300 lines so that a fourth thing adds a file instead of growing the two the shell is, and
/// this is the fourth thing. It went there first and the guard caught it.</para>
/// </remarks>
internal static class Breathing
{
    /// <summary>
    /// How long one breath takes.
    /// </summary>
    /// <remarks>
    /// Slow enough to read as waiting rather than as blinking: a pulse fast enough to catch the eye
    /// at a glance is one that competes with the list for attention, which is the opposite of what
    /// motion that informs is for.
    /// </remarks>
    private static readonly Duration Breath = new(TimeSpan.FromSeconds(1.4));

    /// <summary>How far down the breath dips before coming back.</summary>
    private const double Dip = 0.35;

    /// <summary>Start or stop the breath.</summary>
    /// <param name="dot">The element to breathe.</param>
    /// <param name="starting">Whether the engine is still coming up.</param>
    /// <remarks>
    /// Stopping restores full opacity rather than leaving it wherever the breath had reached. A dot
    /// frozen at <see cref="Dip"/> because the animation was removed mid-cycle would read as a
    /// disabled control, and it would make <c>--capture-window</c> differ from itself — which is the
    /// same trap the water at the foot of the window fell into, and the reason
    /// <see cref="Motion"/> is asked here rather than answered again.
    /// </remarks>
    internal static void Set(UIElement dot, bool starting)
    {
        ArgumentNullException.ThrowIfNull(dot);

        if (!starting || !Motion.Allowed)
        {
            dot.BeginAnimation(UIElement.OpacityProperty, null);
            dot.Opacity = 1;
            return;
        }

        dot.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation
            {
                From = 1,
                To = Dip,
                Duration = Breath,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            });
    }
}
