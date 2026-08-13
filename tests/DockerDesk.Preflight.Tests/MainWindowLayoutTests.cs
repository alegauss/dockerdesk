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
///
/// <para>It moved with the pages it checks (DD35). Each list is its own file now, so the rule is
/// applied per page rather than to one window that happened to hold all three — which is also what
/// makes a fourth list safe: it is checked by the same rule with no edit here.</para>
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

    /// <summary>Every page that draws a list, found rather than listed.</summary>
    private static IEnumerable<string> Pages() =>
        Directory.EnumerateFiles(
            RepositoryFile("src/DockerDesk.Tray/Ui/Pages"), "*Page.xaml", SearchOption.TopDirectoryOnly);

    private static List<List<string>> ColumnBlocks(string page)
    {
        var xaml = File.ReadAllText(page);
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
        // The file order is header, then the row template it captions. Pairing them this way is what
        // makes adding a page safe: a new pair is checked by the same rule with no edit here.
        var checkedAny = false;
        foreach (var page in Pages())
        {
            var blocks = ColumnBlocks(page);
            Assert.True(
                blocks.Count % 2 == 0,
                $"{Path.GetFileName(page)} has {blocks.Count} column blocks: one is unpaired");

            for (var pair = 0; pair < blocks.Count; pair += 2)
            {
                Assert.Equal(blocks[pair], blocks[pair + 1]);
                checkedAny = true;
            }
        }

        Assert.True(checkedAny, "no page was checked, so this guard proved nothing");
    }

    [Fact]
    public void The_actions_column_is_wide_enough_for_the_five_buttons_a_running_row_shows()
    {
        // Measured against the window: Logs, Shell, Stop, Restart and Remove come to about 281
        // device-independent pixels together, and at 236 the last one was clipped to a sliver.
        var widths = ColumnBlocks(
            Pages().Single(p => Path.GetFileName(p) == "ContainersPage.xaml"))[0];

        Assert.True(
            int.TryParse(widths[^1], out var actions),
            $"the actions column should be a fixed width, not '{widths[^1]}'");
        Assert.True(actions >= 300, $"the actions column is {actions}, too narrow for five buttons");
    }

    [Fact]
    public void Every_destination_is_in_the_strip_and_the_containers_one_is_first()
    {
        var xaml = File.ReadAllText(RepositoryFile("src/DockerDesk.Tray/Ui/MainWindow.xaml"));
        var destinations = Regex.Matches(xaml, @"Tag=""([^""]+)"" Checked=""Destination_Checked""")
            .Select(match => match.Groups[1].Value)
            .ToList();

        // Containers first because it is what the window is opened for, and it is the one built with
        // the window rather than on first visit.
        Assert.Equal(["Containers", "Images", "Volumes"], destinations);

        // And there is a page behind each one: a strip entry with nothing to show would navigate to
        // an empty host and look like a window that failed to load.
        foreach (var destination in destinations)
        {
            Assert.Contains(
                Pages(),
                page => Path.GetFileName(page) == destination + "Page.xaml");
        }
    }
}
