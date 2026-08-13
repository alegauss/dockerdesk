using System.Windows;
using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;

namespace DockerDesk.Tray.Ui.Pages;

/// <summary>
/// The volumes list, which is the one thing here that does not come back (DD35).
/// </summary>
internal partial class VolumesPage : System.Windows.Controls.UserControl
{
    private readonly DockerApi _api;
    private readonly Func<EngineState> _engineState;
    private readonly RowActivity _volumeActivity = new();
    private VolumeTotals _volumeTotals = new(0, 0, Measured: true);

    /// <summary>Construct the page.</summary>
    /// <param name="api">The Engine API client.</param>
    /// <param name="engineState">What the engine is doing, asked at render time.</param>
    internal VolumesPage(DockerApi api, Func<EngineState> engineState)
    {
        InitializeComponent();
        _api = api;
        _engineState = engineState;
    }

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

    private async void RemoveVolume(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: VolumeRow row })
        {
            return;
        }

        // Always asked. This is the one thing in this tool that does not come back.
        var answer = System.Windows.MessageBox.Show(
            Window.GetWindow(this), VolumeRemoval.Question(row), "Delete volume",
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
            Window.GetWindow(this), _volumeTotals.PruneQuestion, "Prune anonymous volumes",
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
}
