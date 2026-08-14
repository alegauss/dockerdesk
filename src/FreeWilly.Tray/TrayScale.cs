using Microsoft.Win32;

namespace FreeWilly.Tray;

/// <summary>
/// Notices when the notification area starts asking for a different size (DD99).
/// </summary>
/// <remarks>
/// <see cref="StateIcon.NotificationAreaSize"/> answers the question at the moment of drawing, which
/// is the whole answer only for a machine whose display never changes. It does change: a laptop
/// docked to a 4K panel re-scales without a restart, and so does the scale slider in Settings. Under
/// <c>PerMonitorV2</c> Windows does not resample for the app, and <c>NotifyIcon</c> keeps wearing
/// whatever image it was last handed — so without this the sharp icon the first half of DD99 bought
/// lasts exactly until the display it was drawn for goes away, and the tray goes back to the blurry
/// square with no event anybody can see.
///
/// <para><b>The size is compared, not the notification.</b> A display event fires for a resolution
/// change, a monitor arriving, a monitor leaving and a scale change alike, and only the last of those
/// changes what the tray should draw. Redrawing on all of them would destroy and rebuild an
/// unmanaged icon handle for events that changed nothing; asking Windows for one integer and
/// comparing it is cheaper than the redraw it avoids, and it makes "did this matter" the thing the
/// test can assert.</para>
///
/// <para><b>Two events, because one of them is not guaranteed.</b> A mode change broadcasts
/// <c>WM_DISPLAYCHANGE</c>, which is <see cref="SystemEvents.DisplaySettingsChanged"/>; a scale
/// changed with the slider alone can arrive as <c>WM_SETTINGCHANGE</c> instead, which is
/// <see cref="SystemEvents.UserPreferenceChanged"/>. Subscribing to both costs nothing a redundant
/// notification does not already cost, because the comparison above is what decides.</para>
///
/// <para><b>Both handlers arrive off the UI thread.</b> <see cref="SystemEvents"/> owns a window on a
/// thread of its own, so the <c>redraw</c> handed to the constructor is expected to marshal — it is
/// the caller's, and the caller is the one holding a synchronisation context. Detaching in
/// <see cref="Dispose"/> is not tidiness either: <see cref="SystemEvents"/> is static and holds the
/// subscriber alive, and this process is one that runs for days.</para>
/// </remarks>
internal sealed class TrayScale : IDisposable
{
    private readonly Func<int> _ask;
    private readonly Action _redraw;
    private int _drawnAt;
    private bool _watching;

    /// <summary>Construct the watch.</summary>
    /// <param name="redraw">
    /// What to do when the size moved. Called off the UI thread, so it marshals.
    /// </param>
    /// <param name="ask">
    /// What the notification area is asking for, or nothing for Windows' own answer. A test supplies
    /// its own, because the display this suite runs on is not one it can re-scale.
    /// </param>
    internal TrayScale(Action redraw, Func<int>? ask = null)
    {
        _redraw = redraw;
        _ask = ask ?? StateIcon.NotificationAreaSize;
    }

    /// <summary>The size the icon now on screen was drawn at.</summary>
    internal int DrawnAt => _drawnAt;

    /// <summary>Ask what to draw at, and record it as what is on screen.</summary>
    /// <returns>The edge, in pixels.</returns>
    /// <remarks>
    /// Called by the one place that draws, so the recorded size is the size that actually shipped to
    /// the shell rather than the size something intended. A drawing at a size the caller named for
    /// its own reasons is not recorded here, because it is not the tray's icon.
    /// </remarks>
    internal int Drawing() => _drawnAt = _ask();

    /// <summary>Start listening for the display changing under the icon.</summary>
    internal void Watch()
    {
        if (_watching)
        {
            return;
        }

        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        _watching = true;
    }

    /// <summary>Redraw if, and only if, the shell has started asking for a different size.</summary>
    /// <returns>Whether it redrew.</returns>
    internal bool RedrawIfMoved()
    {
        if (_ask() == _drawnAt)
        {
            return false;
        }

        _redraw();
        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_watching)
        {
            return;
        }

        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        _watching = false;
    }

    private void OnDisplayChanged(object? sender, EventArgs e) => _ = RedrawIfMoved();

    private void OnPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // The slider's own category, so a theme or a colour change is not asked the question at all.
        // The comparison would answer no, but this handler fires for every preference on the machine
        // and a P/Invoke per accent colour is not a cost worth paying to learn nothing.
        if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.General)
        {
            _ = RedrawIfMoved();
        }
    }
}
