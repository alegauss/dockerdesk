using System.Windows;
using FreeWilly.Core.Api;
using FreeWilly.Core.Engine;

namespace FreeWilly.Tray.Ui.Pages;

/// <summary>
/// The images list, joined against the containers that hold them (DD35).
/// </summary>
internal partial class ImagesPage : System.Windows.Controls.UserControl
{
    private readonly IEngineClient _api;
    private readonly Func<EngineState> _engineState;
    private readonly RowActivity _imageActivity = new();
    private ImageTotals _totals = new(0, 0, 0);

    /// <summary>Construct the page.</summary>
    /// <param name="api">The Engine API client.</param>
    /// <param name="engineState">What the engine is doing, asked at render time.</param>
    internal ImagesPage(IEngineClient api, Func<EngineState> engineState)
    {
        InitializeComponent();
        _api = api;
        _engineState = engineState;
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

        _rows = rows;
        _failure = failure;
        Show();
    }

    /// <summary>What the join last produced, before the sort and the filter are applied.</summary>
    private IReadOnlyList<ImageRow> _rows = [];

    /// <summary>The list, reconciled rather than replaced, so a new row can say so (DD70).</summary>
    private LiveRows<ImageRow>? _liveRows;

    private LiveRows<ImageRow> _live =>
        _liveRows ??= new LiveRows<ImageRow>(Images, row => row.Id);

    private string? _failure;

    /// <summary>Draw the rows in hand, shaped.</summary>
    private void Show()
    {
        var shown = ImageRow.Shaped(_rows, _shape);

        // The totals are about the machine, not about what is currently shown: a filter narrows the
        // list, and telling somebody they have 400 MB of images because they typed a name would be a
        // number that means nothing.
        _totals = ImageTotals.For(_rows);
        _live.Show(shown);
        ImageTotalsLine.Text = _rows.Count == 0 ? "" : _totals.Summary;
        PruneImages.IsEnabled = _totals.CanPrune;

        RepositoryHeading.Content = ImageRow.Columns.Repository + _shape.GlyphFor(ImageRow.Columns.Repository);
        TagHeading.Content = ImageRow.Columns.Tag + _shape.GlyphFor(ImageRow.Columns.Tag);
        SizeHeading.Content = ImageRow.Columns.Size + _shape.GlyphFor(ImageRow.Columns.Size);
        UsedByHeading.Content = ImageRow.Columns.UsedBy + _shape.GlyphFor(ImageRow.Columns.UsedBy);

        var empty = shown.Count == 0;
        Images.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ImageHeaderRow.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ImagesEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (!empty)
        {
            return;
        }

        if (_shape.EmptyBecauseFiltered("images") is var (headline, detail) && _rows.Count > 0)
        {
            (ImagesEmptyHeadline.Text, ImagesEmptyDetail.Text) = (headline, detail);
            return;
        }

        (ImagesEmptyHeadline.Text, ImagesEmptyDetail.Text) = (_failure, _engineState()) switch
        {
            (not null, _) => ("The images could not be read", _failure!),
            (_, EngineState.Running) => (
                "No images",
                "Nothing has been pulled or built yet. Images appear here as they arrive."),
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
    private ListShape _shape = new(ImageRow.DefaultColumn, Descending: true);

    /// <summary>Re-sort on a heading click, and redraw from the rows already in hand.</summary>
    private void SortBy(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string column })
        {
            return;
        }

        // A size reads best biggest-first and a name does not, so a column starts at its own natural
        // direction rather than at whatever the last one happened to be.
        _shape = _shape.Toggled(column, descendsFirst: column is ImageRow.Columns.Size or ImageRow.Columns.UsedBy);
        Show();
    }

    /// <summary>Re-narrow as it is typed, over the rows in hand.</summary>
    private void FilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _shape = _shape.Narrowed((sender as System.Windows.Controls.TextBox)?.Text);
        Show();
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
            Window.GetWindow(this), _totals.PruneQuestion, "Prune dangling images",
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
        // Through the rows in hand and the same Show(), so a row going pending keeps the order and
        // the filter it was drawn under.
        _rows = [.. _rows.Select(_imageActivity.Dress)];
        Show();
    }
}
