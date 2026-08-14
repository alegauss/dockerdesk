using System.Windows.Interop;

namespace FreeWilly.Tray.Ui;

/// <summary>
/// Gives the WPF windows the message pre-processing pass WPF's own pump performs, which the WinForms
/// pump this application runs on does not.
/// </summary>
/// <remarks>
/// The tray is a WinForms process — <c>Application.Run(new TrayApplication())</c> — that shows WPF
/// windows, and WPF keyboard input does not arrive through <c>WndProc</c> alone: <c>HwndSource</c>
/// subscribes to <see cref="ComponentDispatcher"/> and expects the pump to offer it every thread
/// message <b>before</b> <c>TranslateMessage</c>. WPF's own <c>Dispatcher.PushFrame</c> loop does
/// exactly that; the WinForms loop knows nothing about it.
///
/// <para>What that costs is a window that looks right and clicks right — mouse input <i>is</i>
/// WndProc-driven — while every key press is dropped: nothing can be typed into the filter box, and
/// Tab and Escape do nothing either. The filter box is where it shows first, because it is the one
/// control in this window whose entire purpose is typing.</para>
///
/// <para>It survived every capture because <c>--capture-window</c> runs under a WPF pump (see
/// <see cref="Theme"/>), where input works — the review harness renders a window it can photograph
/// but never types into one. claude-tray hit the same defect for the same reason and fixed it the
/// same way (its T135); this is that fix, and the shared cause is that both are WinForms trays
/// hosting WPF.</para>
///
/// <para>Forwarding through a WinForms <see cref="IMessageFilter"/> reproduces WPF's loop exactly,
/// including the "handled means do not translate or dispatch" contract: a message WPF consumed must
/// not go on to <c>TranslateMessage</c>, or the keystroke is delivered twice. Messages belonging to
/// windows that are not WPF — the tray icon and its menu — are offered to no subscriber and pass
/// straight through, so this costs one delegate call per message and changes nothing about
/// WinForms.</para>
/// </remarks>
internal sealed class WpfInputBridge : IMessageFilter
{
    /// <summary>Offer one thread message to WPF before the pump translates it.</summary>
    /// <param name="m">The message, as WinForms carries it.</param>
    /// <returns><see langword="true"/> where WPF consumed it, which stops the pump dispatching it.</returns>
    public bool PreFilterMessage(ref Message m)
    {
        var msg = new MSG
        {
            hwnd = m.HWnd,
            message = m.Msg,
            wParam = m.WParam,
            lParam = m.LParam,
        };

        return ComponentDispatcher.RaiseThreadMessage(ref msg);
    }

    /// <summary>Install the bridge on this thread's WinForms pump.</summary>
    /// <remarks>
    /// Called from the one entry point that pumps with WinForms while showing WPF windows. It has to
    /// be in place before <c>Application.Run</c>, not before the first window: the filter is a
    /// property of the pump, and a window opened later is served by the same one.
    /// </remarks>
    internal static void Install() => Application.AddMessageFilter(new WpfInputBridge());
}
