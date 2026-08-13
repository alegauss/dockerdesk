using System.Windows;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DockerDesk.Tray.Ui;

/// <summary>What a state chip is asserting, in the three tones a glance can tell apart.</summary>
/// <remarks>
/// Three and not five. The word inside the chip carries the detail, and five shades of one hue are
/// not distinguishable at a glance anyway — claude-tray reached the same three for the same reason.
/// </remarks>
public enum RowTone
{
    /// <summary>Nothing is wrong: it is up.</summary>
    Good,

    /// <summary>In between, or on its way somewhere.</summary>
    Warn,

    /// <summary>It stopped and did not mean to.</summary>
    Bad,

    /// <summary>True and uninteresting — it stopped cleanly, or has not started.</summary>
    Muted,
}

/// <summary>
/// The theme-resolved brushes a list is drawn with, looked up once per render rather than per row.
/// </summary>
/// <remarks>
/// DD36, borrowed from claude-tray's <c>RowStyle</c> along with its reasoning. Two things matter here.
///
/// <para><b>Resolved once.</b> A <c>FindResource</c> per row is a dictionary walk per row per refresh,
/// and the list re-renders on every engine event.</para>
///
/// <para><b>Tints, not colours.</b> The chip fills are translucent, so one pair of bytes works on both
/// the light and the dark card surface — a solid green that reads on dark is a highlighter pen on
/// light. The alpha is the only thing that changes between the two.</para>
/// </remarks>
/// <param name="Good">The chip fill for a container that is up.</param>
/// <param name="Warn">The chip fill for paused or restarting.</param>
/// <param name="Bad">The chip fill for a container that died.</param>
/// <param name="Muted">The chip fill for a clean exit or a container not yet started.</param>
/// <param name="ChipText">What a chip's word is written in.</param>
/// <param name="MutedText">What a muted chip's word is written in.</param>
public sealed record RowStyle(
    Brush Good, Brush Warn, Brush Bad, Brush Muted, Brush ChipText, Brush MutedText)
{
    /// <summary>The fill for one tone.</summary>
    /// <param name="tone">What the chip is asserting.</param>
    /// <returns>The brush.</returns>
    public Brush Fill(RowTone tone) => tone switch
    {
        RowTone.Good => Good,
        RowTone.Warn => Warn,
        RowTone.Bad => Bad,
        _ => Muted,
    };

    /// <summary>What the word on a chip of that tone is written in.</summary>
    /// <param name="tone">What the chip is asserting.</param>
    /// <returns>The brush.</returns>
    public Brush Text(RowTone tone) => tone is RowTone.Muted ? MutedText : ChipText;

    /// <summary>Resolve the brushes against the theme the host is currently in.</summary>
    /// <param name="host">Any element in the visual tree, for the Fluent resources.</param>
    /// <returns>The style.</returns>
    public static RowStyle For(FrameworkElement host)
    {
        ArgumentNullException.ThrowIfNull(host);

        // Read off the theme rather than guessed: the same test claude-tray uses, and it follows a
        // light/dark switch without this file knowing which one is on.
        var dark = host.TryFindResource("TextFillColorPrimaryBrush") is SolidColorBrush text
                   && (text.Color.R + text.Color.G + text.Color.B) / 3 > 0x80;

        Brush Tint(byte r, byte g, byte b) => Frozen(new SolidColorBrush(
            Color.FromArgb(dark ? (byte)0x40 : (byte)0x30, r, g, b)));

        return new RowStyle(
            // The same green, amber and red the engine's own dot uses, so the chip and the indicator
            // above it are not two vocabularies for one fact.
            Good: Tint(Palette.RunningR, Palette.RunningG, Palette.RunningB),
            Warn: Tint(Palette.StartingR, Palette.StartingG, Palette.StartingB),
            Bad: Tint(Palette.DangerR, Palette.DangerG, Palette.DangerB),
            Muted: (Brush)host.FindResource("SubtleFillColorSecondaryBrush"),
            ChipText: (Brush)host.FindResource("TextFillColorPrimaryBrush"),
            MutedText: (Brush)host.FindResource("TextFillColorSecondaryBrush"));
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
