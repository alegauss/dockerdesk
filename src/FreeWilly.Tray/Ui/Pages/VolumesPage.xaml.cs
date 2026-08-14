using System.Windows;
using FreeWilly.Core.Api;
using FreeWilly.Core.Engine;

namespace FreeWilly.Tray.Ui.Pages;

/// <summary>
/// The volumes list, which is the one thing here that does not come back (DD35).
/// </summary>
internal partial class VolumesPage : System.Windows.Controls.UserControl
{
    private readonly IEngineClient _api;
    private readonly Func<EngineState> _engineState;
    private readonly RowActivity _volumeActivity = new();
    private VolumeTotals _volumeTotals = new(0, 0, Measured: true);

    /// <summary>Construct the page.</summary>
    /// <param name="api">The Engine API client.</param>
    /// <param name="engineState">What the engine is doing, asked at render time.</param>
    internal VolumesPage(IEngineClient api, Func<EngineState> engineState)
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

    /// <summary>What the join last produced, before the sort and the filter are applied.</summary>
    private IReadOnlyList<VolumeRow> _rows = [];

    /// <summary>The list, reconciled rather than replaced, so a new row can say so (DD70).</summary>
    private LiveRows<VolumeRow>? _liveRows;

    private LiveRows<VolumeRow> _live =>
        _liveRows ??= new LiveRows<VolumeRow>(Volumes, row => row.Name);

    private string? _failure;

    private void ShowVolumes(IReadOnlyList<VolumeRow> rows, string? failure)
    {
        _rows = rows;
        _failure = failure;
        Show();
    }

    /// <summary>Draw the rows in hand, shaped.</summary>
    private void Show()
    {
        var shown = VolumeRow.Shaped(_rows, _shape);

        // Over everything, not over what is shown: a filter narrows the list and does not change how
        // much is on disk.
        _volumeTotals = VolumeTotals.For(_rows);
        _live.Show(shown);
        VolumeTotalsLine.Text = _rows.Count == 0 ? "" : _volumeTotals.Summary;
        PruneVolumes.IsEnabled = _volumeTotals.CanPrune;

        NameHeading.Content = VolumeRow.Columns.Name + _shape.GlyphFor(VolumeRow.Columns.Name);
        SizeHeading.Content = VolumeRow.Columns.Size + _shape.GlyphFor(VolumeRow.Columns.Size);
        MountedByHeading.Content =
            VolumeRow.Columns.MountedBy + _shape.GlyphFor(VolumeRow.Columns.MountedBy);

        var empty = shown.Count == 0;
        Volumes.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        VolumeHeaderRow.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        VolumesEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (!empty)
        {
            return;
        }

        if (_shape.EmptyBecauseFiltered("volumes") is var (headline, detail) && _rows.Count > 0)
        {
            (VolumesEmptyHeadline.Text, VolumesEmptyDetail.Text) = (headline, detail);
            return;
        }

        (VolumesEmptyHeadline.Text, VolumesEmptyDetail.Text) = (_failure, _engineState()) switch
        {
            (not null, _) => ("The volumes could not be read", _failure!),
            (_, EngineState.Running) => (
                "No volumes",
                "Nothing has been created yet. A named volume or a `docker run -v` appears here."),
            _ => ("The engine is not running", "Start it to see what is on disk."),
        };
    }


    /// <summary>
    /// The sort and the filter, held by the page rather than by the controls.
    /// </summary>
    /// <remarks>
    /// This is the part DD37 calls easy to get wrong. The list redraws on every engine event, so a
    /// shape that lived only in the ListView would be thrown away each time and snap back to its
    /// default while somebody was reading it.
    /// </remarks>
    private ListShape _shape = new(VolumeRow.DefaultColumn, Descending: false);

    /// <summary>Re-sort on a heading click, and redraw from the rows already in hand.</summary>
    private void SortBy(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string column })
        {
            return;
        }

        // A size reads best biggest-first and a name does not, so a column starts at its own natural
        // direction rather than at whatever the last one happened to be.
        _shape = _shape.Toggled(column, descendsFirst: column is VolumeRow.Columns.Size or VolumeRow.Columns.MountedBy);
        Show();
    }

    /// <summary>Re-narrow as it is typed, over the rows in hand.</summary>
    private void FilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _shape = _shape.Narrowed((sender as System.Windows.Controls.TextBox)?.Text);
        Show();
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
        _rows = [.. _rows.Select(_volumeActivity.Dress)];
        Show();
    }
}
