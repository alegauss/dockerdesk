// This is a WinForms + WPF hybrid, so System.Drawing and System.Windows.Media both contribute a Brush
// and a Color. Pin these names to the WPF (Media) types; the GDI+ ones below are spelled in full, which
// is the point of this file — the two edges are named, not guessed at.
using FreeWilly.Core.Engine;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace FreeWilly.Tray.Ui;

/// <summary>
/// The colours whose value is not a free choice, declared once (DD34).
///
/// <para><b>Danger</b> means one thing: <em>the engine refused, or this is stderr</em>. It was written
/// four times across two files — three in <c>MainWindow.xaml</c> and once in <c>LogWindow.xaml</c> — and
/// none of the four was pinned by anything, so all four could move independently. The failure mode is
/// quiet: a refusal under a container row and a stderr line in its log window in two reds, saying two
/// things where the whole point was one.</para>
///
/// <para>The engine's three state colours had a second, separate home in <see cref="StateIcon"/>, in
/// GDI+, converted to a WPF brush by hand at one call site. Same defect, opposite direction.</para>
///
/// <para><b>Why a value and not a brush.</b> The tray icon is GDI+ and wants a
/// <see cref="System.Drawing.Color"/>; the window is WPF and wants a frozen <see cref="Brush"/>; markup
/// wants something <c>x:Static</c> can reach. No one type serves all three, so what is shared is the
/// value and each edge converts — which is why the bytes, and not any one of the three, are what the
/// rest of this file is derived from. Borrowed from claude-tray's <c>Brand</c>, whose reasoning
/// transfers unchanged.</para>
/// </summary>
internal static class Palette
{
    /// <summary>Danger, as the three bytes everything else here is built from.</summary>
    public const byte DangerR = 0xE5;
    public const byte DangerG = 0x48;
    public const byte DangerB = 0x4D;

    /// <summary>The engine running.</summary>
    public const byte RunningR = 0x2E;
    public const byte RunningG = 0xA0;
    public const byte RunningB = 0x43;

    /// <summary>The engine on its way up.</summary>
    public const byte StartingR = 0xD2;
    public const byte StartingG = 0x9A;
    public const byte StartingB = 0x00;

    /// <summary>The engine down, and the state anything unknown is drawn as.</summary>
    public const byte StoppedR = 0x8B;
    public const byte StoppedG = 0x94;
    public const byte StoppedB = 0x9E;

    /// <summary>
    /// The three blues the mark's wave is drawn in, back to front, and its foam (DD69).
    /// </summary>
    /// <remarks>
    /// The site's own values rather than a lookalike: <c>site/src/index.css</c> declares these as
    /// <c>--wave-1</c>, <c>--wave-2</c> and <c>--wave-3</c>, and the window drawing a different blue
    /// would be two surfaces disagreeing about one mark.
    ///
    /// <para><b>Tints, not colours</b>, which is <see cref="RowStyle"/>'s rule and the reason one set
    /// of bytes serves a light window and a dark one. The site keeps a second trio for dark; here the
    /// water is drawn translucent over whatever the theme's background is, so it darkens by itself and
    /// there is nothing to keep in step.</para>
    /// </remarks>
    public const byte WaveBackR = 0x91;

    /// <summary>The back wave's green.</summary>
    public const byte WaveBackG = 0xCD;

    /// <summary>The back wave's blue.</summary>
    public const byte WaveBackB = 0xDD;

    /// <summary>The middle wave's red.</summary>
    public const byte WaveMidR = 0x62;

    /// <summary>The middle wave's green.</summary>
    public const byte WaveMidG = 0xB9;

    /// <summary>The middle wave's blue.</summary>
    public const byte WaveMidB = 0xCD;

    /// <summary>The front wave's red.</summary>
    public const byte WaveFrontR = 0x3D;

    /// <summary>The front wave's green.</summary>
    public const byte WaveFrontG = 0xA7;

    /// <summary>The front wave's blue.</summary>
    public const byte WaveFrontB = 0xB7;

    /// <summary>The back wave.</summary>
    public static readonly Color WaveBack = Color.FromRgb(WaveBackR, WaveBackG, WaveBackB);

    /// <summary>The middle wave.</summary>
    public static readonly Color WaveMid = Color.FromRgb(WaveMidR, WaveMidG, WaveMidB);

    /// <summary>The front wave, which the foam rides.</summary>
    public static readonly Color WaveFront = Color.FromRgb(WaveFrontR, WaveFrontG, WaveFrontB);

    /// <summary>A refusal, or stderr. Reached from markup as <c>{x:Static ui:Palette.Danger}</c>.</summary>
    public static readonly Color Danger = Color.FromRgb(DangerR, DangerG, DangerB);

    /// <summary>
    /// Frozen, like every brush this app keeps.
    /// </summary>
    /// <remarks>
    /// They are shared across windows and never mutated, and an unfrozen one pays a lock on every draw.
    /// </remarks>
    public static readonly Brush DangerBrush = Frozen(new SolidColorBrush(Danger));

    /// <summary>The engine running, for markup.</summary>
    public static readonly Color Running = Color.FromRgb(RunningR, RunningG, RunningB);

    /// <summary>The engine starting, for markup.</summary>
    public static readonly Color Starting = Color.FromRgb(StartingR, StartingG, StartingB);

    /// <summary>The engine stopped, for markup.</summary>
    public static readonly Color Stopped = Color.FromRgb(StoppedR, StoppedG, StoppedB);

    /// <summary>
    /// The face everything but a log is set in, and the fallback Windows 10 actually has.
    /// </summary>
    /// <remarks>
    /// Here rather than as an implicit <c>Style TargetType="Window"</c> in Theme.xaml, and the capture
    /// is why: an implicit style is keyed by the exact type, so it does not reach a Window subclass —
    /// both windows silently fell back to the message font, and only a picture showed it. Reached from
    /// markup the same way the colours are.
    /// </remarks>
    public static readonly System.Windows.Media.FontFamily Body =
        new("Segoe UI Variable Text, Segoe UI");

    /// <summary>Danger for anything that wants text.</summary>
    public static string DangerHex => $"#{DangerR:X2}{DangerG:X2}{DangerB:X2}";

    /// <summary>
    /// The state's colour for GDI+, which draws the tray icon and knows nothing about WPF's types.
    /// </summary>
    /// <param name="state">The engine state.</param>
    /// <returns>The colour.</returns>
    /// <remarks>
    /// This is the one that must always render: the tray icon is drawn in a WinForms process before any
    /// <see cref="System.Windows.Application"/> exists, so the direction of the dependency matters — GDI+
    /// reads bytes, and never a brush out of a resource dictionary that may not be there.
    /// </remarks>
    public static System.Drawing.Color EngineGdi(EngineState state) => state switch
    {
        EngineState.Running => System.Drawing.Color.FromArgb(RunningR, RunningG, RunningB),
        EngineState.Starting => System.Drawing.Color.FromArgb(StartingR, StartingG, StartingB),
        _ => System.Drawing.Color.FromArgb(StoppedR, StoppedG, StoppedB),
    };

    /// <summary>The same state's colour as a frozen brush, for the window's engine dot.</summary>
    /// <param name="state">The engine state.</param>
    /// <returns>The brush.</returns>
    public static Brush EngineBrush(EngineState state) => state switch
    {
        EngineState.Running => RunningBrush,
        EngineState.Starting => StartingBrush,
        _ => StoppedBrush,
    };

    private static readonly Brush RunningBrush = Frozen(new SolidColorBrush(Running));
    private static readonly Brush StartingBrush = Frozen(new SolidColorBrush(Starting));
    private static readonly Brush StoppedBrush = Frozen(new SolidColorBrush(Stopped));

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
