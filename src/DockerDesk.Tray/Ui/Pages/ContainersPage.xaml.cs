using System.Diagnostics;
using System.IO;
using System.Windows;
using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;

namespace DockerDesk.Tray.Ui.Pages;

/// <summary>
/// The containers list, and everything that is only about containers (DD35).
/// </summary>
/// <remarks>
/// It owns its header, its rows, the four verbs, the log windows it opens and the shell it launches.
/// What it does not own is the engine banner or the strip above it: the shell owns the chrome, and a
/// list owns its page. Before this, one window class held three of these, and DD12 and networks were
/// each about to add a fourth copy of the same stanza.
///
/// <para>Built the first time it is navigated to and then kept, collapsed, for the life of the window
/// — so a scroll position and a list already read survive switching away. That is claude-tray's
/// destination host, and it is why this is a <see cref="System.Windows.Controls.UserControl"/> rather
/// than a <c>TabItem</c>, which tears its content down on every switch.</para>
/// </remarks>
internal partial class ContainersPage : System.Windows.Controls.UserControl
{
    private readonly IEngineClient _api;
    private readonly Func<EngineState> _engineState;
    private readonly Action _startEngine;
    private readonly RowActivity _activity = new();
    private readonly Dictionary<string, LogWindow> _logs = new(StringComparer.Ordinal);

    /// <summary>Construct the page.</summary>
    /// <param name="api">The Engine API client.</param>
    /// <param name="engineState">What the engine is doing, asked at render time.</param>
    /// <param name="startEngine">What the empty state's button does.</param>
    internal ContainersPage(IEngineClient api, Func<EngineState> engineState, Action startEngine)
    {
        InitializeComponent();
        _api = api;
        _engineState = engineState;
        _startEngine = startEngine;
    }

    /// <summary>
    /// Re-read the list. Called when an event says the list changed, never on a timer and never
    /// from a refresh button: the stream is what makes this correct.
    /// </summary>
    /// <param name="settled">
    /// The container an event just arrived for, whose pending state that event confirms. This is
    /// what ends a wait — not the HTTP response, which only says the daemon took the call.
    /// </param>
    /// <returns>A task that completes when the window has been redrawn.</returns>
    internal async Task RefreshAsync(string? settled = null)
    {
        if (settled is not null)
        {
            _activity.Settled(settled);
        }

        var engine = _engineState();

        IReadOnlyList<ContainerRow> rows = [];
        if (engine is EngineState.Running)
        {
            try
            {
                var containers = await _api.ContainersAsync().ConfigureAwait(true);
                _activity.Prune(containers.Select(c => c.Id));

                // Resolved once for the whole render rather than per row: this list is rebuilt on
                // every engine event, and a FindResource per row is a dictionary walk per row.
                var style = RowStyle.For(this);
                rows =
                [
                    .. containers.Select(ContainerRow.From)
                        .Select(_activity.Dress)
                        .Select(row => row.WithChip(style)),
                ];
            }
            catch (DockerApiException)
            {
                // The engine went away between the event and this call. The empty state below says
                // so, which is more use than a dialog about a race the user did not cause.
                rows = [];
            }
        }

        Containers.ItemsSource = rows;
        var empty = rows.Count == 0;
        Containers.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        HeaderRow.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        Empty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

        if (empty)
        {
            var state = EmptyState.For(engine);
            EmptyHeadline.Text = state.Headline;
            EmptyDetail.Text = state.Detail;
            EmptyStart.Visibility = state.OffersStart ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OpenPort(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string url } && url.Length > 0)
        {
            // The whole reason ports are links: making somebody retype localhost:8080 is a small
            // daily tax a GUI exists to remove.
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
    }

    private void StartEngine(object sender, RoutedEventArgs e) => _startEngine();

    /// <summary>
    /// Open this container's log, or bring the window already open on it to the front.
    /// </summary>
    /// <remarks>
    /// One window per container, keyed by id. Pressing Logs twice on the same row opening a second
    /// window would mean two streams against one container and two buffers filling in parallel.
    /// </remarks>
    private void OpenLogs(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ContainerRow row })
        {
            return;
        }

        if (_logs.TryGetValue(row.Id, out var open))
        {
            open.Activate();
            return;
        }

        var window = new LogWindow(_api, row.Id, row.Name) { Owner = Window.GetWindow(this) };
        _logs[row.Id] = window;
        window.Closed += (_, _) => _logs.Remove(row.Id);
        window.Show();
    }

    /// <summary>
    /// Ask the container which shell it has, then hand the terminal the user already has a
    /// <c>docker exec</c> against it.
    /// </summary>
    /// <remarks>
    /// The probe is why the row goes pending first: two round trips to the daemon before any window
    /// appears, and a click that looks like it did nothing for that long is a click people press
    /// again. An image with no shell says so on the row rather than opening a terminal that closes.
    /// </remarks>
    private async void OpenShell(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ContainerRow row })
        {
            return;
        }

        _activity.Began(row.Id, ContainerVerb.Shell);
        Redress();
        try
        {
            var shell = await ContainerShell
                .FindAsync((command, token) => _api.RunInContainerAsync(row.Id, command, token))
                .ConfigureAwait(true);

            if (shell is null)
            {
                _activity.Failed(row.Id, ContainerShell.NoShellMessage(row.Name));
                return;
            }

            var launch = ContainerShell.LaunchFor(
                Terminals.Choose(File.Exists), new EnginePaths().DockerCli, row.Id, shell);

            var start = new ProcessStartInfo(launch.FileName) { UseShellExecute = false };
            foreach (var argument in launch.Arguments)
            {
                start.ArgumentList.Add(argument);
            }

            Process.Start(start)?.Dispose();
            _activity.Settled(row.Id);
        }
        catch (DockerApiException failure)
        {
            _activity.Failed(row.Id, failure.Detail ?? failure.Message);
        }
        catch (Exception failure) when (failure is System.ComponentModel.Win32Exception
            or InvalidOperationException or IOException)
        {
            // The terminal is not where it was expected, or Windows refused to start it. The row is
            // where the click was, so the row is where this goes.
            _activity.Failed(row.Id, $"the terminal would not start: {failure.Message}");
        }
        finally
        {
            Redress();
        }
    }

    /// <summary>
    /// The one verb the row shows, which is Stop for a live container and Start for anything else.
    /// </summary>
    /// <remarks>
    /// Decided from the row rather than from two buttons with a visibility binding each: the row
    /// already knows which verb it is offering, and reading it in two places is how the two answers
    /// drift apart.
    /// </remarks>
    private void PrimaryAction(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContainerRow row })
        {
            Send(row, row.CanStop ? ContainerVerb.Stop : ContainerVerb.Start);
        }
    }

    /// <summary>
    /// Open the row's overflow, on a left click rather than only on a right one.
    /// </summary>
    /// <remarks>
    /// A context menu that only answers the right button is the discovery problem the section warns
    /// about wearing a different hat. Placed under the button so it reads as belonging to that row and
    /// not to wherever the pointer happened to be.
    /// </remarks>
    private void OpenOverflow(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { ContextMenu: { } menu } button)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.DataContext = button.Tag;
        menu.IsOpen = true;
    }

    private void StartContainer(object sender, RoutedEventArgs e) =>
        Act(sender, ContainerVerb.Start);

    private void StopContainer(object sender, RoutedEventArgs e) =>
        Act(sender, ContainerVerb.Stop);

    private void RestartContainer(object sender, RoutedEventArgs e) =>
        Act(sender, ContainerVerb.Restart);

    private void RemoveContainer(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ContainerRow row })
        {
            return;
        }

        if (ContainerAction.AsksBeforeRemoving(row))
        {
            var answer = System.Windows.MessageBox.Show(
                Window.GetWindow(this),
                ContainerAction.RemovalPrompt(row),
                "Remove container",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,

                // The safe answer is the one Enter and Escape both land on.
                System.Windows.MessageBoxResult.No);

            if (answer is not System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
        }

        // force is what the dialog was about: without it the daemon answers 409 for a running
        // container and the row would say so after the user already agreed to the kill.
        Send(row, ContainerVerb.Remove, force: row.IsLive);
    }

    private void Act(object sender, ContainerVerb verb)
    {
        if (sender is FrameworkElement { Tag: ContainerRow row })
        {
            Send(row, verb);
        }
    }

    private void Send(ContainerRow row, ContainerVerb verb, bool force = false)
    {
        // Pending first, and without asking the daemon anything: this is the half-second between
        // the click and the engine's first word, and it is the half-second the row has to account
        // for.
        _activity.Began(row.Id, verb);
        Redress();
        _ = CallAsync(row.Id, verb, force);
    }

    private async Task CallAsync(string id, ContainerVerb verb, bool force)
    {
        try
        {
            await ContainerAction.InvokeAsync(_api, verb, id, force).ConfigureAwait(true);
        }
        catch (DockerApiException failure)
        {
            // On the row, in the engine's words. Detail is those words alone; Message wraps them in
            // the endpoint, which a log wants and a row does not. A dialog would take the message
            // away from the place the user was looking when they pressed the button.
            _activity.Failed(id, failure.Detail ?? failure.Message);
            Redress();
        }
    }

    /// <summary>
    /// Redraw the rows already on screen against what is now known about them, with no call to the
    /// daemon. The list itself has not changed; what a row is waiting for has.
    /// </summary>
    private void Redress()
    {
        if (Containers.ItemsSource is IEnumerable<ContainerRow> rows)
        {
            // The chip is carried through: a row going pending keeps the colour it already had, and
            // re-resolving the brushes here would be the per-row lookup the render avoids.
            Containers.ItemsSource = rows.Select(_activity.Dress).ToList();
        }
    }
}
