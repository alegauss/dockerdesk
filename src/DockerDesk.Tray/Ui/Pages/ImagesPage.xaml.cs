using System.Windows;
using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;

namespace DockerDesk.Tray.Ui.Pages;

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
        if (Images.ItemsSource is IEnumerable<ImageRow> rows)
        {
            Images.ItemsSource = rows.Select(_imageActivity.Dress).ToList();
        }
    }
}
