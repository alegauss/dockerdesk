using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;
using DockerDesk.Core.Licensing;

namespace DockerDesk.Tray.Ui;

/// <summary>
/// The container list. A view of the engine, so it is correct without being asked.
/// </summary>
internal partial class MainWindow : Window
{
    private readonly DockerApi _api;
    private readonly Func<EngineState> _engineState;
    private readonly Action _startEngine;
    private readonly RowActivity _activity = new();
    private readonly RowActivity _imageActivity = new();
    private readonly RowActivity _volumeActivity = new();
    private VolumeTotals _volumeTotals = new(0, 0, Measured: true);
    private readonly Dictionary<string, LogWindow> _logs = new(StringComparer.Ordinal);
    private ImageTotals _totals = new(0, 0, 0);

    /// <summary>Construct the window.</summary>
    /// <param name="api">The Engine API client.</param>
    /// <param name="engineState">What the engine is doing, asked at render time.</param>
    /// <param name="startEngine">What the empty state's button does.</param>
    internal MainWindow(DockerApi api, Func<EngineState> engineState, Action startEngine)
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
        ShowEngine(engine);

        IReadOnlyList<ContainerRow> rows = [];
        if (engine is EngineState.Running)
        {
            try
            {
                var containers = await _api.ContainersAsync().ConfigureAwait(true);
                _activity.Prune(containers.Select(c => c.Id));
                rows = [.. containers.Select(ContainerRow.From).Select(_activity.Dress)];
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

    /// <summary>
    /// Re-read the images, joined against the containers that hold them.
    /// </summary>
    /// <remarks>
    /// Both lists, every time. "Which of these can I delete" is answered by the join, and a cached
    /// container list would answer it about a container that has since gone.
    /// </remarks>
    /// <returns>A task that completes when the tab has been redrawn.</returns>
    internal async Task RefreshImagesAsync()
    {
        IReadOnlyList<ImageRow> rows = [];
        string? failure = null;
        if (_engineState() is EngineState.Running)
        {
            try
            {
                var images = await _api.ImagesAsync().ConfigureAwait(true);
                var containers = await _api.ContainersAsync().ConfigureAwait(true);
                _imageActivity.Prune(images.Select(image => image.Id));
                rows = [.. ImageRow.From(images, containers).Select(_imageActivity.Dress)];
            }
            catch (DockerApiException refused)
            {
                failure = refused.Detail ?? refused.Message;
            }
        }

        _totals = ImageTotals.For(rows);
        Images.ItemsSource = rows;
        ImageTotalsLine.Text = rows.Count == 0 ? "" : _totals.Summary;
        PruneImages.IsEnabled = _totals.CanPrune;

        var empty = rows.Count == 0;
        Images.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ImageHeaderRow.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ImagesEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (!empty)
        {
            return;
        }

        (ImagesEmptyHeadline.Text, ImagesEmptyDetail.Text) = (failure, _engineState()) switch
        {
            (not null, _) => ("The images could not be read", failure!),
            (_, EngineState.Running) => (
                "No images",
                "Nothing has been pulled or built yet. Images appear here as they arrive."),
            _ => ("The engine is not running", "Start it to see what is on disk."),
        };
    }

    /// <summary>
    /// Re-read the images only when somebody is looking at them.
    /// </summary>
    /// <remarks>
    /// The event stream fires whether the tab is visible or not, and two list reads to redraw a
    /// pane behind another one is the poll this project keeps not writing.
    /// </remarks>
    /// <returns>A task that completes when the tab has been redrawn, or immediately.</returns>
    internal Task RefreshImagesIfShowingAsync() =>
        Tabs.SelectedIndex == 1 ? RefreshImagesAsync() : Task.CompletedTask;

    /// <summary>
    /// Re-read the volumes, then measure them.
    /// </summary>
    /// <remarks>
    /// Two calls on purpose. The list and the join are metadata and come back at once; the sizes
    /// come from <c>/system/df</c>, which walks the filesystem and can take seconds, so it fills a
    /// column that already says what it is waiting for rather than holding the tab blank.
    /// </remarks>
    /// <returns>A task that completes when the sizes have landed too.</returns>
    internal async Task RefreshVolumesAsync()
    {
        IReadOnlyList<VolumeRow> rows = [];
        string? failure = null;
        if (_engineState() is EngineState.Running)
        {
            try
            {
                var volumes = await _api.VolumesAsync().ConfigureAwait(true);
                var containers = await _api.ContainersAsync().ConfigureAwait(true);
                _volumeActivity.Prune(volumes.Select(volume => volume.Name));
                rows = [.. VolumeRow.From(volumes, containers).Select(_volumeActivity.Dress)];
            }
            catch (DockerApiException refused)
            {
                failure = refused.Detail ?? refused.Message;
            }
        }

        ShowVolumes(rows, failure);
        if (rows.Count == 0)
        {
            return;
        }

        try
        {
            var measured = await _api.VolumeSizesAsync().ConfigureAwait(true);

            // Against what is on screen now, not against the list captured above: the user may
            // have deleted a row while the daemon was counting.
            if (Volumes.ItemsSource is IEnumerable<VolumeRow> showing)
            {
                ShowVolumes([.. VolumeRow.WithSizes(showing, measured)], failure: null);
            }
        }
        catch (DockerApiException)
        {
            // The sizes are the optional half. A column that keeps saying it is measuring is a
            // better answer than throwing away a list that is otherwise correct.
        }
    }

    private void ShowVolumes(IReadOnlyList<VolumeRow> rows, string? failure)
    {
        _volumeTotals = VolumeTotals.For(rows);
        Volumes.ItemsSource = rows;
        VolumeTotalsLine.Text = rows.Count == 0 ? "" : _volumeTotals.Summary;
        PruneVolumes.IsEnabled = _volumeTotals.CanPrune;

        var empty = rows.Count == 0;
        Volumes.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        VolumeHeaderRow.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        VolumesEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (!empty)
        {
            return;
        }

        (VolumesEmptyHeadline.Text, VolumesEmptyDetail.Text) = (failure, _engineState()) switch
        {
            (not null, _) => ("The volumes could not be read", failure!),
            (_, EngineState.Running) => (
                "No volumes",
                "Nothing has been created yet. A named volume or a `docker run -v` appears here."),
            _ => ("The engine is not running", "Start it to see what is on disk."),
        };
    }

    private void TabChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Only when it becomes the visible tab: reading two or three lists to render a pane nobody
        // is looking at is the poll this project keeps not writing.
        if (!ReferenceEquals(e.OriginalSource, Tabs))
        {
            return;
        }

        _ = Tabs.SelectedIndex switch
        {
            1 => RefreshImagesAsync(),
            2 => RefreshVolumesAsync(),
            _ => Task.CompletedTask,
        };
    }

    private async void RemoveVolume(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: VolumeRow row })
        {
            return;
        }

        // Always asked. This is the one thing in this tool that does not come back.
        var answer = System.Windows.MessageBox.Show(
            this, VolumeRemoval.Question(row), "Delete volume",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);

        if (answer is not System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        _volumeActivity.Began(row.Name, ContainerVerb.Remove);
        RedressVolumes();
        try
        {
            await _api.RemoveVolumeAsync(row.Name).ConfigureAwait(true);
            _volumeActivity.Settled(row.Name);
        }
        catch (DockerApiException refused)
        {
            _volumeActivity.Failed(row.Name, refused.Detail ?? refused.Message);
            RedressVolumes();
            return;
        }

        await RefreshVolumesAsync().ConfigureAwait(true);
    }

    private async void PruneAnonymous(object sender, RoutedEventArgs e)
    {
        if (!_volumeTotals.CanPrune)
        {
            return;
        }

        var answer = System.Windows.MessageBox.Show(
            this, _volumeTotals.PruneQuestion, "Prune anonymous volumes",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);

        if (answer is not System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        PruneVolumes.IsEnabled = false;
        VolumeOutcome.Text = "Pruning…";
        try
        {
            VolumeOutcome.Text = VolumeTotals.PruneOutcome(
                await _api.PruneAnonymousVolumesAsync().ConfigureAwait(true));
        }
        catch (DockerApiException refused)
        {
            VolumeOutcome.Text = refused.Detail ?? refused.Message;
        }

        await RefreshVolumesAsync().ConfigureAwait(true);
    }

    private void RedressVolumes()
    {
        if (Volumes.ItemsSource is IEnumerable<VolumeRow> rows)
        {
            Volumes.ItemsSource = rows.Select(_volumeActivity.Dress).ToList();
        }
    }

    private async void RemoveImage(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ImageRow row })
        {
            return;
        }

        _imageActivity.Began(row.Id, ContainerVerb.Remove);
        RedressImages();
        try
        {
            // Never forced. The refusal for an image a container holds names that container, and
            // that sentence is the answer to what the user has to deal with first.
            await _api.RemoveImageAsync(row.Id).ConfigureAwait(true);
            _imageActivity.Settled(row.Id);
        }
        catch (DockerApiException refused)
        {
            _imageActivity.Failed(row.Id, refused.Detail ?? refused.Message);
            RedressImages();
            return;
        }

        await RefreshImagesAsync().ConfigureAwait(true);
    }

    private async void PruneDangling(object sender, RoutedEventArgs e)
    {
        if (!_totals.CanPrune)
        {
            return;
        }

        var answer = System.Windows.MessageBox.Show(
            this, _totals.PruneQuestion, "Prune dangling images",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);

        if (answer is not System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        PruneImages.IsEnabled = false;
        ImageOutcome.Text = "Pruning…";
        try
        {
            var pruned = await _api.PruneDanglingImagesAsync().ConfigureAwait(true);

            // Reported after, in the daemon's own numbers rather than the estimate shown before:
            // another client may have taken some of it in between.
            ImageOutcome.Text = ImageTotals.PruneOutcome(pruned);
        }
        catch (DockerApiException refused)
        {
            ImageOutcome.Text = refused.Detail ?? refused.Message;
        }

        await RefreshImagesAsync().ConfigureAwait(true);
    }

    private void RedressImages()
    {
        if (Images.ItemsSource is IEnumerable<ImageRow> rows)
        {
            Images.ItemsSource = rows.Select(_imageActivity.Dress).ToList();
        }
    }

    private void ShowEngine(EngineState engine)
    {
        var colour = StateIcon.ColourFor(engine);
        EngineDot.Fill = new SolidColorBrush(
            System.Windows.Media.Color.FromRgb(colour.R, colour.G, colour.B));
        EngineLabel.Text = engine switch
        {
            EngineState.Running => "Engine running",
            EngineState.Starting => "Engine starting",
            _ => "Engine stopped",
        };
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

    private void ShowAbout(object sender, RoutedEventArgs e) =>
        System.Windows.MessageBox.Show(
            this,
            Attribution.About(EngineManifest.Current, BuildVersion.Current),
            "About DockerDesk",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);

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

        var window = new LogWindow(_api, row.Id, row.Name) { Owner = this };
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
                this,
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
            Containers.ItemsSource = rows.Select(_activity.Dress).ToList();
        }
    }
}
