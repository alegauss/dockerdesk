using System.Windows;
using System.Windows.Media;

namespace FreeWilly.Tray.Ui;

/// <summary>
/// Whether this window may move anything, asked in one place (DD70).
/// </summary>
/// <remarks>
/// DD69 answered this for the water at the foot of the window and DD70 needs the same answer for the
/// engine dot, so the question moved here rather than being asked twice. Two surfaces disagreeing
/// about whether animation is allowed is the defect this prevents: a machine with animation switched
/// off would have a still band and a breathing dot, which is worse than either.
///
/// <para><b>None of these is a preference.</b> <c>ClientAreaAnimation</c> is the accessibility
/// setting Windows already asks the question with, so asking it again in this app's own settings
/// would be a second answer to one question. A render tier of 0 has no hardware behind it, which is
/// where a perpetual animation stops being free. And a capture has to be the same bytes every time.</para>
/// </remarks>
internal static class Motion
{
    /// <summary>
    /// Held still for a capture.
    /// </summary>
    /// <remarks>
    /// Set by <c>--capture-window</c> before any window exists, and static because that is what the
    /// timing needs: the capture shows a window whose fixture reports the engine running, so by the
    /// time anything holds a reference to a control the animation would already have started and the
    /// picture would catch a random phase. Measured — two captures of one build differed until this
    /// existed, and the whole review harness rests on them not differing.
    /// </remarks>
    internal static bool Still { get; set; }

    /// <summary>Whether anything in this window may animate at all.</summary>
    internal static bool Allowed =>
        !Still
        && SystemParameters.ClientAreaAnimation
        && (RenderCapability.Tier >> 16) > 0;
}
