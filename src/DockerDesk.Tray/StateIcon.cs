using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DockerDesk.Core.Engine;

namespace DockerDesk.Tray;

/// <summary>
/// The engine state, drawn small enough to be read out of the corner of an eye.
/// </summary>
/// <remarks>
/// Shape carries the state and colour only reinforces it. At sixteen pixels a colour is a hint —
/// two of these are seen on a taskbar at a glance, by people who may not separate red from green,
/// against a background that is light on one machine and dark on the next. So Running is a filled
/// disc, Starting is a ring with a gap, and Stopped is a plain ring: distinguishable in a
/// screenshot printed in black and white, which is the bar worth holding.
/// </remarks>
public static class StateIcon
{
    /// <summary>The colour each state is drawn in.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The colour.</returns>
    public static Color ColourFor(EngineState state) => state switch
    {
        EngineState.Running => Color.FromArgb(0x2E, 0xA0, 0x43),
        EngineState.Starting => Color.FromArgb(0xD2, 0x9A, 0x00),
        EngineState.Stopped => Color.FromArgb(0x8B, 0x94, 0x9E),
        _ => Color.FromArgb(0x8B, 0x94, 0x9E),
    };

    /// <summary>The words the tooltip says, for when the shape is not enough.</summary>
    /// <param name="state">The state.</param>
    /// <returns>One line.</returns>
    public static string TooltipFor(EngineState state) => state switch
    {
        EngineState.Running => "DockerDesk — engine running",
        EngineState.Starting => "DockerDesk — engine starting",
        EngineState.Stopped => "DockerDesk — engine stopped",
        _ => "DockerDesk",
    };

    /// <summary>Draw the state at <paramref name="size"/> pixels square.</summary>
    /// <param name="state">The state.</param>
    /// <param name="size">The edge, in pixels.</param>
    /// <returns>The bitmap, owned by the caller.</returns>
    public static Bitmap Draw(EngineState state, int size = 16)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 8);

        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var colour = ColourFor(state);
        var pen = Math.Max(2f, size / 8f);
        var inset = pen / 2f;
        var box = new RectangleF(inset, inset, size - pen, size - pen);

        switch (state)
        {
            case EngineState.Running:
                using (var brush = new SolidBrush(colour))
                {
                    graphics.FillEllipse(brush, box);
                }

                break;

            case EngineState.Starting:
                // A ring with a bite out of it: the same outline as Stopped and unmistakably not it,
                // which is what tells "on its way up" from "not running" without relying on hue.
                using (var stroke = new Pen(colour, pen))
                {
                    graphics.DrawArc(stroke, box, startAngle: -45, sweepAngle: 270);
                }

                break;

            default:
                using (var stroke = new Pen(colour, pen))
                {
                    graphics.DrawEllipse(stroke, box);
                }

                break;
        }

        return bitmap;
    }

    /// <summary>Draw the state as an icon a <c>NotifyIcon</c> can wear.</summary>
    /// <param name="state">The state.</param>
    /// <param name="size">The edge, in pixels.</param>
    /// <returns>The icon, owned by the caller.</returns>
    public static Icon Icon(EngineState state, int size = 16)
    {
        using var bitmap = Draw(state, size);
        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>
    /// How many pixels of the drawing are painted at all, ignoring colour.
    /// </summary>
    /// <remarks>
    /// The measurement that makes "shape, not colour" testable: a filled disc covers far more of the
    /// box than a ring, and a ring with a gap covers less than a closed one. Asserting on this is
    /// asserting that the three are told apart by a reader who sees no colour.
    /// </remarks>
    /// <param name="bitmap">The drawing.</param>
    /// <returns>The count of pixels with any opacity.</returns>
    public static int InkedPixels(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var inked = 0;
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                // Half opacity, so the anti-aliased fringe is not counted as ink on both sides.
                if (bitmap.GetPixel(x, y).A > 128)
                {
                    inked++;
                }
            }
        }

        return inked;
    }
}
