using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using Size = System.Windows.Size;
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
/// <para><b>Twice the band has to be arranged twice the band, not merely sized it</b> (DD104). A
/// child whose render size exceeds the slot it was arranged in gets a layout clip, that clip lives
/// in the child's own coordinates, and a <c>RenderTransform</c> moves the child <i>and its clip</i>
/// together — so the second repeat was never drawn and the drift slid the first one out to the left,
/// leaving the water to withdraw off the right edge and snap back. <see cref="ArrangeOverride"/>
/// hands every layer a slot two bands wide, which is the only thing that removes the clip. Nothing
/// about it shows in a still capture, which is why the review harness could not see it.</para>
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

    /// <summary>
    /// How far past the floor the fill runs, in the same logical units.
    /// </summary>
    /// <remarks>
    /// Enough to cover <see cref="RiseBy"/> at any band height this is used at — forty units is six
    /// pixels of a fifty-two pixel band and fifteen of a hundred-and-twenty-eight pixel one, both more
    /// than the rise — and all of it is clipped away, so overshooting costs nothing.
    /// </remarks>
    private const double Undertow = 40;

    /// <summary>
    /// How tall the band is where nothing says otherwise.
    /// </summary>
    /// <remarks>
    /// A default and not a fixed size: the window's foot wants a strip and the About band wants
    /// something the mark can sit inside, so a host that sets <c>Height</c> gets what it asked for
    /// and the layers follow it (DD83).
    /// </remarks>
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

    /// <summary>One layer of water: where it sits, how far it swells, and how it travels.</summary>
    private readonly record struct Layer(
        double Baseline, double Amplitude, double Period, Color Colour, double Drift,
        bool Rightward, double Rise, bool Foam);

    /// <summary>
    /// Back to front. Nearer water is drawn lower, shorter and shallower, which is what reads as
    /// depth, and travels faster, which is what reads as nearness.
    /// </summary>
    /// <remarks>
    /// <para>The far layer travels the other way, as it does on the site: three bands sliding one way
    /// at three speeds resolve into one moving thing, and one opposed to the other two is what keeps
    /// them reading as separate water.</para>
    ///
    /// <para>Every one of these numbers is the site's, the rise included — three layers rising and
    /// falling on 11, 8.5 and 7 seconds against drifts of 43, 31 and 22 never repeat the same frame
    /// twice in any span anybody watches, and one shared rise made the three read as one sheet of
    /// painted water sliding under itself.</para>
    /// </remarks>
    private static readonly Layer[] Layers =
    [
        new(72, 26, 720, Palette.WaveBack, 43, Rightward: true, Rise: 11, Foam: false),
        new(100, 16, 480, Palette.WaveMid, 31, Rightward: false, Rise: 8.5, Foam: false),
        new(122, 11, 360, Palette.WaveFront, 22, Rightward: false, Rise: 7, Foam: true),
    ];

    /// <summary>How far the sea lifts at the top of its rise.</summary>
    /// <remarks>
    /// Three pixels against the site's seven, because this band is a third of the height of that one
    /// and the rise is a proportion of the water, not a distance.
    /// </remarks>
    private const double RiseBy = 3;

    private readonly TranslateTransform[] _drifts = new TranslateTransform[Layers.Length];
    private readonly TranslateTransform[] _swells = new TranslateTransform[Layers.Length];
    private readonly FrameworkElement[] _bands = new FrameworkElement[Layers.Length];
    private bool _running;

    /// <summary>Build the band.</summary>
    public Waves()
    {
        Height = BandHeight;
        ClipToBounds = true;
        IsHitTestVisible = false;

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
            _swells[i] = new TranslateTransform();
            band.RenderTransform = Travel(i);
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
                RenderTransform = Travel(i),
            };
            _bands = [.. _bands.Append(foamBand)];
            Children.Add(foamBand);
        }

        SizeChanged += (_, _) => Resize();
        IsVisibleChanged += (_, _) => Apply();
    }

    /// <summary>One layer's sideways travel and its own rise, on one element.</summary>
    /// <remarks>
    /// Two animations on one property are one overwriting the other, which is why the site puts the
    /// drift and the swell on two nested elements; two transforms in one group is the same arithmetic
    /// with one element fewer. It matters that the rise is <i>inside</i> the band rather than on it:
    /// this control carries the clip that makes it a band, and lifting that clip along with the water
    /// opened a strip of bare window under the sea for half of every cycle.
    /// </remarks>
    /// <param name="layer">Which layer, back to front.</param>
    /// <returns>The transform the layer renders through.</returns>
    private Transform Travel(int layer) =>
        new TransformGroup { Children = { _drifts[layer], _swells[layer] } };

    /// <summary>
    /// Give every layer a slot two bands wide — the size it is already drawn at.
    /// </summary>
    /// <remarks>
    /// Not tidiness and not an optimisation: this is the whole of DD104. WPF clips a child whose
    /// render size overruns the slot it was arranged in, the clip is expressed in that child's own
    /// coordinates, and <c>RenderTransform</c> carries the child and the clip together — so a layer
    /// arranged in one band while drawn at two lost its second repeat and then drifted the first one
    /// off to the left, which read as the water withdrawing off the right edge and snapping back.
    /// Arranging at the size the layer actually is leaves nothing to clip; the band's own
    /// <c>ClipToBounds</c>, which is not moved by any of this, still crops it to the strip.
    /// </remarks>
    /// <param name="finalSize">The band.</param>
    /// <returns>The band, unchanged: the doubling is what the children are arranged in, not what
    /// this control occupies.</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(new Size(finalSize.Width * 2, finalSize.Height));
        return finalSize;
    }

    /// <summary>Whether the engine is running, which is the only thing that moves this.</summary>
    /// <param name="running">Whether it is up.</param>
    internal void Running(bool running)
    {
        _running = running;
        Apply();
    }

    /// <summary>Whether the water may drift.</summary>
    /// <remarks>
    /// The engine and this window's own visibility are this control's to ask; the rest is
    /// <see cref="Motion"/>, which DD70 shares with the engine dot. A window nobody can see has no
    /// reason to animate.
    /// </remarks>
    private bool MayMove => _running && IsVisible && Motion.Allowed;

    private void Resize()
    {
        // Two repeats laid out across twice the band, so 1440 units is one band width and the drift
        // travels exactly one of them.
        foreach (var band in _bands)
        {
            band.Width = ActualWidth * 2;
            band.Height = ActualHeight;
        }

        Apply();
    }

    private void Apply()
    {
        if (!MayMove)
        {
            // Removed rather than paused, and at phase zero rather than wherever it had reached:
            // a capture of a still window has to be the same bytes every time.
            for (var i = 0; i < _drifts.Length; i++)
            {
                _drifts[i].BeginAnimation(TranslateTransform.XProperty, null);
                _drifts[i].X = 0;
                _swells[i].BeginAnimation(TranslateTransform.YProperty, null);
                _swells[i].Y = 0;
            }

            return;
        }

        for (var i = 0; i < _drifts.Length; i++)
        {
            // One whole repeat, so the frame after the last is the first. A layer running the other
            // way starts one repeat out and arrives at zero: two repeats of the same crest are drawn,
            // so -ActualWidth and 0 are the same picture and the still band does not care which end
            // of the cycle it is caught at.
            var travel = new DoubleAnimation
            {
                From = Layers[i].Rightward ? -ActualWidth : 0,
                To = Layers[i].Rightward ? 0 : -ActualWidth,
                Duration = new Duration(TimeSpan.FromSeconds(Layers[i].Drift)),
                RepeatBehavior = RepeatBehavior.Forever,
            };
            _drifts[i].BeginAnimation(TranslateTransform.XProperty, travel);

            // Each layer rises on its own clock, as the site's three do. Eased, because water
            // decelerates at the top of a swell and a linear rise reads as a lift.
            _swells[i].BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation
                {
                    From = 0,
                    To = -RiseBy,
                    Duration = new Duration(TimeSpan.FromSeconds(Layers[i].Rise)),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                });
        }
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

    /// <summary>The same crest, closed below the floor: the water under the line.</summary>
    /// <remarks>
    /// Below, not down to it. A layer that ends exactly on the floor ends exactly on the bottom edge
    /// of the band, so the moment it rises it takes its own floor with it and leaves a sliver of bare
    /// window under the sea. The site hides that behind a mask that fades the water into the page;
    /// this band has a hard edge at the frame instead, so the fill runs past it and the band's
    /// <c>ClipToBounds</c> ends the water rather than the geometry doing it.
    /// </remarks>
    private static string Water(Layer layer) =>
        Invariant($"{Crest(layer)} V{Floor + Undertow} H0 Z");

    /// <summary>Path data is a format, not prose: a comma decimal separator would not parse.</summary>
    private static string Invariant(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
