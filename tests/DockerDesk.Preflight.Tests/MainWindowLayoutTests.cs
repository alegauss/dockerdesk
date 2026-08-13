using System.Text.RegularExpressions;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// The one thing about the list window that can be wrong without any code being wrong.
/// </summary>
/// <remarks>
/// The captions live in a Grid of their own and the cells live in a DataTemplate, and the two carry
/// separate copies of the same column definitions. They drifted apart once already — a search and
/// replace matched the header's indentation and not the template's — and the result was every
/// heading sitting over the wrong cell and the action buttons clipped at the right edge. No test
/// that constructs a row can see that; reading the markup can.
/// </remarks>
public sealed class MainWindowLayoutTests
{
    private static string RepositoryFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DockerDesk.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "the repository root was not found above the test binaries");
        return Path.Combine(directory!.FullName, relative);
    }

    private static List<List<string>> ColumnBlocks()
    {
        var xaml = File.ReadAllText(RepositoryFile("src/DockerDesk.Tray/Ui/MainWindow.xaml"));
        return
        [
            .. Regex.Matches(
                    xaml,
                    @"<Grid\.ColumnDefinitions>(.*?)</Grid\.ColumnDefinitions>",
                    RegexOptions.Singleline)
                .Select(block => Regex
                    .Matches(block.Groups[1].Value, @"Width=""([^""]+)""")
                    .Select(width => width.Groups[1].Value)
                    .ToList()),
        ];
    }

    [Fact]
    public void Every_header_is_laid_out_on_the_same_columns_as_the_rows_under_it()
    {
        // The file order is header, then the row template it captions, per tab. Pairing them this
        // way is what makes adding a tab safe: a new pair is checked by the same rule.
        var blocks = ColumnBlocks();

        Assert.NotEmpty(blocks);
        Assert.True(blocks.Count % 2 == 0, $"{blocks.Count} column blocks: one is unpaired");
        for (var pair = 0; pair < blocks.Count; pair += 2)
        {
            Assert.Equal(blocks[pair], blocks[pair + 1]);
        }
    }

    [Fact]
    public void The_actions_column_is_wide_enough_for_the_five_buttons_a_running_row_shows()
    {
        // Measured against the window: Logs, Shell, Stop, Restart and Remove come to about 281
        // device-independent pixels together, and at 236 the last one was clipped to a sliver.
        var widths = ColumnBlocks()[0];

        Assert.True(
            int.TryParse(widths[^1], out var actions),
            $"the actions column should be a fixed width, not '{widths[^1]}'");
        Assert.True(actions >= 300, $"the actions column is {actions}, too narrow for five buttons");
    }

    [Fact]
    public void Both_tabs_are_present_and_the_containers_one_is_first()
    {
        var xaml = File.ReadAllText(RepositoryFile("src/DockerDesk.Tray/Ui/MainWindow.xaml"));
        var headers = Regex.Matches(xaml, @"<TabItem Header=""([^""]+)""")
            .Select(match => match.Groups[1].Value)
            .ToList();

        // The index the code-behind switches on is positional, so the order is not cosmetic.
        Assert.Equal(["Containers", "Images", "Volumes"], headers);
    }
}
