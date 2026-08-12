using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using DockerDesk.Core.Api;
using DockerDesk.Core.Engine;

namespace DockerDesk.Tray.Ui;

/// <summary>
/// The container list. A view of the engine, so it is correct without being asked.
/// </summary>
internal partial class MainWindow : Window
{
    private readonly DockerApi _api;
    private readonly Func<EngineState> _engineState;
    private readonly Action _startEngine;

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
    internal async Task RefreshAsync()
    {
        var engine = _engineState();
        ShowEngine(engine);

        IReadOnlyList<ContainerRow> rows = [];
        if (engine is EngineState.Running)
        {
            try
            {
                var containers = await _api.ContainersAsync().ConfigureAwait(true);
                rows = [.. containers.Select(ContainerRow.From)];
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
}
