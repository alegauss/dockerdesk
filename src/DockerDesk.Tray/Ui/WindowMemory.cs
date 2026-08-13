using System.Drawing;
using System.IO;
using System.Text.Json;

namespace DockerDesk.Tray.Ui;

/// <summary>
/// Where the window was, and what was being read, when it last closed (DD39).
/// </summary>
/// <remarks>
/// A tool opened several times a day landed in the middle of the primary monitor every time, at a size
/// chosen for a laptop, on a desk that has two screens and a place this window belongs. The tab was the
/// sharper loss: somebody clearing disk space works in Images and somebody debugging works in
/// Containers, and that is a piece of state the user set on purpose.
///
/// <para>Not a settings system. This is a handful of values in one small file beside everything else
/// this tool owns, and the two rules below are the whole of its behaviour — because both are how a
/// window that remembers its place is usually got wrong.</para>
///
/// <para><b>A remembered rectangle is used only if it still lands on a monitor that exists.</b> A window
/// remembered onto a laptop's docked second screen is a window that opens off-screen the next morning,
/// and the user's only recourse is a keyboard shortcut they do not know. What is checked is not overlap
/// but reach: enough of the strip the window is dragged by has to be on one screen to take hold of
/// it.</para>
///
/// <para><b>A maximised window is remembered as maximised plus the rectangle it restores to</b>, never
/// as a rectangle the size of the screen — otherwise un-maximising it gives back a window welded to the
/// monitor it was last maximised on. <see cref="WindowPlace"/> reads both in one call.</para>
///
/// <para>The window's own numbers are pixels on the virtual screen, which is the unit
/// <see cref="WindowPlace"/> and a monitor's bounds both speak. <see cref="LogWidth"/> and
/// <see cref="LogHeight"/> are the exception and are device-independent units: the log window is a
/// child placed on its owner, so only its size is remembered, and a size is set through WPF.</para>
/// </remarks>
public sealed record WindowMemory
{
    /// <summary>The destination a window with nothing remembered opens on.</summary>
    public const string FirstDestination = "Containers";

    /// <summary>The height of the strip a window is dragged by, in pixels.</summary>
    private const int TitleBar = 32;

    /// <summary>How much of that strip has to be reachable for the rectangle to be used.</summary>
    private const int Grasp = 160;

    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    /// <summary>Left edge of the restored window, in pixels on the virtual screen.</summary>
    public int Left { get; init; }

    /// <summary>Top edge of the restored window, in pixels on the virtual screen.</summary>
    public int Top { get; init; }

    /// <summary>Width of the restored window, in pixels. Zero where nothing was remembered.</summary>
    public int Width { get; init; }

    /// <summary>Height of the restored window, in pixels. Zero where nothing was remembered.</summary>
    public int Height { get; init; }

    /// <summary>Whether it was maximised over the rectangle above, rather than sitting in it.</summary>
    public bool Maximised { get; init; }

    /// <summary>The destination that was being read.</summary>
    public string Destination { get; init; } = FirstDestination;

    /// <summary>Width of the log window, in device-independent units. Zero for the markup's own.</summary>
    public int LogWidth { get; init; }

    /// <summary>Height of the log window, in device-independent units. Zero for the markup's own.</summary>
    public int LogHeight { get; init; }

    /// <summary>Whether enough of the remembered window lands on a screen to take hold of it.</summary>
    /// <param name="screens">The monitors that exist now, in the same pixels this was saved in.</param>
    /// <returns><see langword="false"/> where the window would open somewhere nobody can reach it.</returns>
    /// <remarks>
    /// The test is against the title bar rather than the whole window, and that is deliberate: a window
    /// whose bottom edge clips the top of a screen overlaps it and is still ungrabbable. A window
    /// narrower than <see cref="Grasp"/> only has to be wholly on a screen.
    /// </remarks>
    public bool LandsOn(IEnumerable<Rectangle> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);
        if (Width < 1 || Height < 1)
        {
            return false;
        }

        var handle = new Rectangle(Left, Top, Width, TitleBar);
        var needed = Math.Min(Grasp, Width);
        return screens.Any(screen =>
        {
            var reachable = Rectangle.Intersect(handle, screen);
            return reachable.Width >= needed && reachable.Height >= TitleBar;
        });
    }

    /// <summary>Read what was remembered.</summary>
    /// <param name="path">The file <see cref="Write"/> wrote.</param>
    /// <returns>What it held, or <see langword="null"/> where there is nothing usable.</returns>
    /// <remarks>
    /// Every failure is the same answer — open where a window with no history opens. A preference file
    /// that was truncated by a power cut is not a reason to refuse to show a window.
    /// </remarks>
    public static WindowMemory? Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<WindowMemory>(File.ReadAllText(path))
                : null;
        }
        catch (Exception failure) when (failure is IOException or JsonException
            or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Write this down.</summary>
    /// <param name="path">Where to write it.</param>
    /// <remarks>
    /// Failing is silent for the same reason reading is: this runs while a window is closing, and a
    /// full disk must not turn that into an unhandled exception on the UI thread — which, in this
    /// process, takes the tray icon with it.
    /// </remarks>
    public void Write(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Layout));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException
            or NotSupportedException)
        {
        }
    }
}
