using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FreeWilly.Tray.Ui;

/// <summary>
/// A window's position in pixels, read and written through Win32 rather than through WPF (DD39).
/// </summary>
/// <remarks>
/// <c>Window.Left</c> and its neighbours are device-independent units, and this application asks for
/// PerMonitorV2 in its manifest — so on a desk where one screen runs at 150% and the other at 100%, what
/// WPF reports depends on which screen the window is on and cannot be compared with a monitor's bounds
/// without knowing that screen's scale. <c>GetWindowPlacement</c> answers in pixels, which is the unit
/// <see cref="System.Windows.Forms.Screen"/> reports monitors in, so <see cref="WindowMemory.LandsOn"/>
/// compares like with like and no scaling arithmetic exists to be wrong.
///
/// <para>It also answers the other half of DD39 in the same call: a placement carries
/// <c>rcNormalPosition</c> — the rectangle the window would restore to — beside the maximised flag, so
/// a maximised window is remembered as maximised over its own rectangle without a special case.</para>
///
/// <para>One approximation, stated so it is not mistaken for a bug: a placement is in workspace
/// coordinates, which differ from screen coordinates by the primary monitor's work-area origin — a
/// taskbar docked left or top. A round trip is exact regardless, since both ends speak the same
/// coordinates; only the reachability test shifts, and by less than the strip it looks for.</para>
/// </remarks>
internal static class WindowPlace
{
    private const int ShowNormal = 1;
    private const int ShowMaximised = 3;

    /// <summary>The monitors attached right now, in pixels.</summary>
    /// <returns>One rectangle per screen.</returns>
    internal static IReadOnlyList<Rectangle> Screens() =>
        [.. System.Windows.Forms.Screen.AllScreens.Select(screen => screen.Bounds)];

    /// <summary>Where a window is, and whether it is maximised over that.</summary>
    /// <param name="window">The window, which must already have a handle.</param>
    /// <returns>Its restore rectangle and state, or <see langword="null"/> before it is sourced.</returns>
    internal static (int Left, int Top, int Width, int Height, bool Maximised)? Of(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var placement = new Placement { Length = Marshal.SizeOf<Placement>() };
        if (!GetWindowPlacement(handle, ref placement))
        {
            return null;
        }

        var rectangle = placement.Normal;
        return (rectangle.Left,
                rectangle.Top,
                rectangle.Right - rectangle.Left,
                rectangle.Bottom - rectangle.Top,
                placement.ShowCommand == ShowMaximised);
    }

    /// <summary>Put a window back where it was.</summary>
    /// <param name="window">The window, which must already have a handle.</param>
    /// <param name="memory">What was remembered, already checked against the screens that exist.</param>
    internal static void Restore(Window window, WindowMemory memory)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(memory);
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var placement = new Placement
        {
            Length = Marshal.SizeOf<Placement>(),
            ShowCommand = memory.Maximised ? ShowMaximised : ShowNormal,
            Normal = new NativeRectangle
            {
                Left = memory.Left,
                Top = memory.Top,
                Right = memory.Left + memory.Width,
                Bottom = memory.Top + memory.Height,
            },
        };

        SetWindowPlacement(handle, ref placement);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Placement
    {
        public int Length;
        public int Flags;
        public int ShowCommand;
        public NativePoint Minimised;
        public NativePoint Maximised;
        public NativeRectangle Normal;
    }

    // DllImport rather than LibraryImport, for the reason Cli/ParentConsole.cs gives: the generated
    // marshalling stubs need AllowUnsafeBlocks, and turning unsafe code on for the whole application to
    // reach two calls is a poor trade.
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr window, ref Placement placement);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPlacement(IntPtr window, ref Placement placement);
}
