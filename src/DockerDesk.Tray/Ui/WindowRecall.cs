using System.Windows;

namespace DockerDesk.Tray.Ui;

/// <summary>
/// Attaches a window to what was remembered of it, and writes it back down (DD39).
/// </summary>
/// <remarks>
/// Its own class rather than more code-behind. The shell owns the chrome and nothing else (DD35), and
/// "restore a rectangle, save it on close" is neither chrome nor a list — it is the same handful of
/// moves for any window, and putting it here keeps MainWindow the size it was cut down to.
///
/// <para>The three moments are all it does: pick a startup location before the window is created,
/// apply the placement the instant there is a handle, and write it back as it closes. Everything it
/// decides is in <see cref="WindowMemory"/>, where it can be tested without a window.</para>
/// </remarks>
internal sealed class WindowRecall
{
    private readonly Window _window;
    private readonly string _file;
    private readonly Func<string> _destination;
    private WindowMemory _memory;
    private bool _sourced;

    /// <summary>Attach to a window that has not been shown yet.</summary>
    /// <param name="window">The window, still in its constructor.</param>
    /// <param name="file">Where the values are kept.</param>
    /// <param name="destination">What is being read, asked as the window closes.</param>
    internal WindowRecall(Window window, string file, Func<string> destination)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentNullException.ThrowIfNull(destination);
        _window = window;
        _file = file;
        _destination = destination;
        _memory = WindowMemory.Read(file) ?? new WindowMemory();

        // The startup location is decided now, not on SourceInitialized: CenterScreen is applied as the
        // window is created, so leaving it on and then moving the window is a visible jump on the way
        // to the same place. Manual only when there is somewhere real to go.
        if (_memory.LandsOn(WindowPlace.Screens()))
        {
            _window.WindowStartupLocation = WindowStartupLocation.Manual;
            _window.SourceInitialized += (_, _) => WindowPlace.Restore(_window, _memory);
        }

        _window.SourceInitialized += (_, _) => _sourced = true;

        // On closing rather than on every move: a window is dragged and resized far more often than it
        // is closed, and the value only has to be right the next time one is opened.
        _window.Closing += (_, _) => Remember();
    }

    /// <summary>The destination that was being read when this window last closed.</summary>
    internal string Destination => _memory.Destination;

    /// <summary>The size a log window should open at, or null for the one its markup names.</summary>
    internal (double Width, double Height)? LogSize =>
        _memory is { LogWidth: > 0, LogHeight: > 0 } ? (_memory.LogWidth, _memory.LogHeight) : null;

    /// <summary>Remember how big a log window was left.</summary>
    /// <param name="width">Its width, in device-independent units.</param>
    /// <param name="height">Its height.</param>
    internal void RememberLogSize(double width, double height)
    {
        _memory = _memory with { LogWidth = (int)width, LogHeight = (int)height };
        _memory.Write(_file);
    }

    private void Remember()
    {
        if (!_sourced)
        {
            return;
        }

        // Kept apart: a placement that could not be read must not overwrite a good rectangle with
        // zeroes, which is the same as forgetting it.
        var closing = WindowPlace.Of(_window) is { } place
            ? _memory with
            {
                Left = place.Left,
                Top = place.Top,
                Width = place.Width,
                Height = place.Height,
                Maximised = place.Maximised,
            }
            : _memory;

        // The same rule that guards the restore, applied to the save — a window nobody could reach is
        // not a place worth remembering. This is not hypothetical: --capture-window shows the window at
        // -32000 with no desktop under it, and without this a screenshot run would overwrite the
        // rectangle and the tab a person had chosen with the render harness's own.
        if (!closing.LandsOn(WindowPlace.Screens()))
        {
            return;
        }

        _memory = closing with { Destination = _destination() };
        _memory.Write(_file);
    }
}
