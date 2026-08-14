using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace FreeWilly.Tray.Ui;

/// <summary>
/// The ocean the mark swims in, at the foot of the window (DD69).
/// </summary>
/// <remarks>
/// The product mark is an orca cresting a wave, and the site closes its hero into water and opens
/// its footer out of it. The window's lowest strip was sixteen pixels of margin and then the frame —
/// the one place here with nothing to say — so the same water goes there, in the same three blues
/// and the same geometry.
///
/// <para><b>The obvious version is wrong.</b> A decorative band inside a utility window is scenery,
/// and this project already states the test it fails: scenery that outshines the copy is a bug.
/// Fluent accepts perpetual motion only where the motion informs. So the water <b>drifts while the
/// engine runs and lies flat and still while it does not</b> — a second reading of the state the dot
/// and its word already carry, which is the constitution's order: shape and word first, this only
/// reinforcing.</para>
///
/// <para><b>The geometry is the site's, not a lookalike.</b> Three layers at 72/26/720, 100/16/480
/// and 122/11/360 over a 2880-unit span drawn at twice the band's width, so 1440 units is exactly
/// one band and the drift translates by one whole repeat: the frame after the last is the first.
/// Every period divides 1440 for the same reason — a wave that does not close on 1440 shows a seam
/// once per cycle, and once per cycle is every few seconds.</para>
///
/// <para><b>Still means phase zero.</b> Three things switch the motion off — a window that is not
/// visible, <c>ClientAreaAnimation</c> off, and a render tier with no hardware behind it — and all
/// three leave the transforms at their start rather than wherever they had reached. That is what
/// keeps <c>--capture-window</c> byte-identical, which the whole review harness rests on.</para>
/// </remarks>
internal sealed class Waves : Grid
{
    /// <summary>Two identical repeats of 1440, so one band width is exactly one repeat.</summary>
    private const double Span = 2880;

    /// <summary>The logical floor the water is filled down to.</summary>
    private const double Floor = 200;

    /// <summary>How tall the band is on screen.</summary>
    private const double BandHeight = 52;

    /// <summary>How far the foam rides above the crest it sits on.</summary>
    private const double FoamRise = 7;

    /// <summary>
    /// How much of the background each layer lets through.
    /// </summary>
    /// <remarks>
    /// Low, and three layers compound it: the first attempt was 0x38 each, which stacked into a slab
    /// of solid teal at the foot of the window — scenery that outshines the copy, which is the test
    /// this band has to pass rather than the one it has to be pretty for.
    /// </remarks>
    private const byte WaterAlpha = 0x24;

    /// <summary>One layer of water: where it sits, how far it swells, and how fast it travels.</summary>
    private readonly record struct Layer(
        double Baseline, double Amplitude, double Period, Color Colour, double Drift, bool Foam);

    /// <summary>
    /// Back to front. Nearer water is drawn lower, shorter and shallower, which is what reads as
    /// depth, and travels faster, which is what reads as nearness.
    /// </summary>
    private static readonly Layer[] Layers =
    [
        new(72, 26, 720, Palette.WaveBack, 43, Foam: false),
        new(100, 16, 480, Palette.WaveMid, 31, Foam: false),
        new(122, 11, 360, Palette.WaveFront, 22, Foam: true),
    ];

    /// <summary>How long one rise and fall of the swell takes.</summary>
    private static readonly Duration Swell = new(TimeSpan.FromSeconds(9));

    private readonly TranslateTransform[] _drifts = new TranslateTransform[Layers.Length];
    private readonly FrameworkElement[] _bands = new FrameworkElement[Layers.Length];
    private readonly TranslateTransform _swell = new();
    private bool _running;

    /// <summary>Build the band.</summary>
    public Waves()
    {
        Height = BandHeight;
        ClipToBounds = true;
        IsHitTestVisible = false;
        RenderTransform = _swell;

        // Translucent, so one set of bytes works on a light window and a dark one: the water darkens
        // against whatever is behind it rather than needing a second palette to keep in step. The
        // same trade RowStyle's chip fills make.
        for (var i = 0; i < Layers.Length; i++)
        {
            var layer = Layers[i];
            var fill = new SolidColorBrush(Color.FromArgb(WaterAlpha, layer.Colour.R, layer.Colour.G, layer.Colour.B));
            fill.Freeze();

            // The path is NOT stretched: a Stretch on the shape scales the geometry's own bounding
            // box, which is the crest tops to the floor rather than 0..200 — so every crest came out
            // several times its height and the band read as scenery, which is the one thing it must
            // not. The canvas below is the site's viewBox, and the Viewbox is the only thing scaling.
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(Water(layer)),
                Fill = fill,
            };

            var band = new Viewbox
            {
                Child = Box(path),
                Stretch = Stretch.Fill,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Height = BandHeight,
            };

            _drifts[i] = new TranslateTransform();
            band.RenderTransform = _drifts[i];
            _bands[i] = band;
            Children.Add(band);

            if (!layer.Foam)
            {
                continue;
            }

            // The foam rides the nearest crest, on the same transform as the water it sits on, so the
            // two cannot drift out of step. A second element rather than a second figure in the same
            // path, because one is stroked and the other filled.
            var foam = new SolidColorBrush(Color.FromArgb(0x3A, 0xFF, 0xFF, 0xFF));
            foam.Freeze();
            var crest = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(Crest(layer with { Baseline = layer.Baseline - FoamRise })),
                Stroke = foam,
                StrokeThickness = 3,
            };

            var foamBand = new Viewbox
            {
                Child = Box(crest),
                Stretch = Stretch.Fill,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Height = BandHeight,
                RenderTransform = _drifts[i],
            };
            _bands = [.. _bands.Append(foamBand)];
            Children.Add(foamBand);
        }

        SizeChanged += (_, _) => Resize();
        IsVisibleChanged += (_, _) => Apply();
    }

    /// <summary>Whether the engine is running, which is the only thing that moves this.</summary>
    /// <param name="running">Whether it is up.</param>
    internal void Running(bool running)
    {
        _running = running;
        Apply();
    }

    /// <summary>
    /// Held still for a capture, which has to be the same bytes every time.
    /// </summary>
    /// <remarks>
    /// Set by <c>--capture-window</c> before the window exists, and static because that is what the
    /// timing needs: the capture shows a window whose fixture reports the engine running, so by the
    /// time anything holds a reference to this the animation would already have started and the
    /// picture would catch a random phase. Measured — two captures of one build differed until this
    /// existed, and the whole review harness rests on them not differing.
    ///
    /// <para>The window being off-screen is not enough on its own: it is shown at -32000 rather than
    /// hidden, so <see cref="UIElement.IsVisible"/> is true and every other switch-off below reads a
    /// perfectly ordinary session.</para>
    /// </remarks>
    internal static bool Still { get; set; }

    /// <summary>
    /// Whether motion is allowed at all.
    /// </summary>
    /// <remarks>
    /// Three refusals besides the capture, and none of them is a preference. A window nobody can see
    /// has no reason to animate; <c>ClientAreaAnimation</c> is the accessibility setting Windows
    /// already asks the question with, so asking it again here would be a second answer; and a render
    /// tier of 0 has no hardware behind it, which is where a perpetual animation stops being free.
    /// </remarks>
    private bool MayMove =>
        _running
        && !Still
        && IsVisible
        && SystemParameters.ClientAreaAnimation
        && (RenderCapability.Tier >> 16) > 0;

    private void Resize()
    {
        // Two repeats laid out across twice the band, so 1440 units is one band width and the drift
        // travels exactly one of them.
        foreach (var band in _bands)
        {
            band.Width = ActualWidth * 2;
        }

        Apply();
    }

    private void Apply()
    {
        if (!MayMove)
        {
            // Removed rather than paused, and at phase zero rather than wherever it had reached:
            // a capture of a still window has to be the same bytes every time.
            foreach (var drift in _drifts)
            {
                drift.BeginAnimation(TranslateTransform.XProperty, null);
                drift.X = 0;
            }

            _swell.BeginAnimation(TranslateTransform.YProperty, null);
            _swell.Y = 0;
            return;
        }

        for (var i = 0; i < _drifts.Length; i++)
        {
            var travel = new DoubleAnimation
            {
                From = 0,
                To = -ActualWidth,
                Duration = new Duration(TimeSpan.FromSeconds(Layers[i].Drift)),
                RepeatBehavior = RepeatBehavior.Forever,
            };
            _drifts[i].BeginAnimation(TranslateTransform.XProperty, travel);
        }

        _swell.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation
            {
                From = 0,
                To = -3,
                Duration = Swell,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            });
    }

    /// <summary>The site's own viewBox, so the proportions are its and not this window's.</summary>
    /// <param name="shape">The water or the foam.</param>
    /// <returns>The shape in a 2880 by 200 box for the Viewbox to scale.</returns>
    private static Canvas Box(UIElement shape)
    {
        var canvas = new Canvas { Width = Span, Height = Floor };
        canvas.Children.Add(shape);
        return canvas;
    }

    /// <summary>
    /// One crest as an open line: half a period up, half a period down, across the span.
    /// </summary>
    /// <remarks>
    /// The control points sit at a quarter and three quarters of each half, which makes the outgoing
    /// tangent of every segment equal the incoming tangent of the next — so the joins, and the wrap
    /// at 1440, are smooth rather than kinked. The same arithmetic as the site's <c>Waves.tsx</c>.
    /// </remarks>
    private static string Crest(Layer layer)
    {
        var half = layer.Period / 2;
        var c1 = half * 0.25;
        var c2 = half * 0.75;
        var d = new StringBuilder(Invariant($"M0 {layer.Baseline}"));
        for (var x = 0.0; x < Span; x += layer.Period)
        {
            d.Append(Invariant($" c{c1} {-layer.Amplitude} {c2} {-layer.Amplitude} {half} 0"));
            d.Append(Invariant($" c{c1} {layer.Amplitude} {c2} {layer.Amplitude} {half} 0"));
        }

        return d.ToString();
    }

    /// <summary>The same crest, closed down to the floor: the water under the line.</summary>
    private static string Water(Layer layer) =>
        Invariant($"{Crest(layer)} V{Floor} H0 Z");

    /// <summary>Path data is a format, not prose: a comma decimal separator would not parse.</summary>
    private static string Invariant(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
