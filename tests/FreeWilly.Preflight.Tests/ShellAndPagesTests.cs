using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The shell owns the chrome and a list owns its page (DD35).
/// </summary>
/// <remarks>
/// The defect this replaced was not a bug: it was that three lists were three hand-written copies of
/// one stanza in one file, so DD12 and networks would each have added a fourth. These pin the split
/// rather than the appearance — the appearance is a capture, and a capture is looked at.
/// </remarks>
public sealed class ShellAndPagesTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FreeWilly.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "the repository root was not found above the test binaries");
        return directory!.FullName;
    }

    private static string Shell(string extension = ".xaml") =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "src/FreeWilly.Tray/Ui/MainWindow.xaml" + (extension == ".xaml" ? "" : ".cs")));

    private static IEnumerable<string> PageMarkup() =>
        Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot(), "src/FreeWilly.Tray/Ui/Pages"), "*Page.xaml");

    [Fact]
    public void The_shell_holds_no_list_of_its_own()
    {
        // What is left in the window is the engine's state, the terms, and the strip. A ListView here
        // means a fourth list went back into the file the split took three out of.
        var shell = Shell();

        Assert.DoesNotContain("<ListView", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabControl", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Prune", shell, StringComparison.Ordinal);
        Assert.Contains("DestinationHost", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void No_page_holds_more_than_one_list()
    {
        // "Exactly one" until DD83, when About became a destination: it holds no list at all, which
        // breaks the letter of that rule and none of its point. The point is that a SECOND list is a
        // page of its own — that is what kept the shell from growing back — and a page with none is
        // simply not a list page.
        var pages = PageMarkup().ToList();
        Assert.NotEmpty(pages);

        var lists = 0;
        foreach (var page in pages)
        {
            var here = File.ReadAllText(page).Split("<ListView ").Length - 1;
            Assert.True(
                here <= 1,
                $"{Path.GetFileName(page)} holds {here} lists. A second one is a page of its own.");
            lists += here;
        }

        // And the list pages are still there, or this passed by finding none. Four since DD126 added
        // the build history — which is the rule working rather than bending: the fourth list arrived
        // as a page of its own, which is the whole thing being guarded.
        Assert.Equal(4, lists);
    }

    [Fact]
    public void A_page_is_built_on_its_first_visit_and_then_kept()
    {
        // The two halves of "lazily built, kept alive". A TabControl gives neither: it reuses one
        // content presenter, so it builds eagerly enough and tears the tree down on every switch.
        var code = Shell(".cs");

        // Cached in a field and null-coalesced into existence — built once, on the visit.
        Assert.Contains("_images ??= Add(new ImagesPage(", code, StringComparison.Ordinal);
        Assert.Contains("_volumes ??= Add(new VolumesPage(", code, StringComparison.Ordinal);

        // And never taken out again: switching away collapses a page, it does not discard it.
        Assert.DoesNotContain("DestinationHost.Children.Remove", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DestinationHost.Children.Clear", code, StringComparison.Ordinal);
        Assert.Contains("Visibility.Collapsed", code, StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_reaches_the_shells_styles_by_DynamicResource()
    {
        // Not a preference. A StaticResource is resolved while the page is parsed, when it is not yet
        // in the window's tree — and the Fluent Button these styles are BasedOn lives in the WINDOW's
        // resources, because ThemeMode puts it there (DD34). A StaticResource here resolves against
        // nothing and the row buttons come back as pre-Fluent grey rectangles.
        foreach (var page in PageMarkup())
        {
            var markup = File.ReadAllText(page);
            // Only the styles that are still the shell's. PortLink and PortText moved into the page
            // that uses them (DD66): they are BasedOn by a row template, BasedOn is a CLR property,
            // and a DynamicResource on it throws when the first row is measured.
            foreach (var style in new[] { "RowAction", "Header" })
            {
                Assert.DoesNotContain(
                    $"StaticResource {style}", markup, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void The_shell_is_a_fraction_of_what_it_replaced()
    {
        // 452 and 698 before, one file each. The number is not the point on its own — the point is
        // that a fourth list adds a file instead of growing these two, and DD126 is the first one to
        // test that: BuildsPage and BuildRow are its own files, and what landed here is the 32 lines
        // a destination costs at minimum — a field, a lazily built property, a case in the switch,
        // a constructor seam, and the one method a docker-desktop:// link enters by.
        //
        // So the code bound moved 300 → 340 once, with that spent. It is not a budget to top up: the
        // next destination that needs another 32 lines of shell is a destination whose shape should
        // be argued about, which is what a failure here is for.
        var markup = File.ReadAllLines(
            Path.Combine(RepositoryRoot(), "src/FreeWilly.Tray/Ui/MainWindow.xaml")).Length;
        var code = File.ReadAllLines(
            Path.Combine(RepositoryRoot(), "src/FreeWilly.Tray/Ui/MainWindow.xaml.cs")).Length;

        Assert.True(markup < 250, $"the shell's markup is {markup} lines, against 452 before");
        Assert.True(code < 340, $"the shell's code-behind is {code} lines, against 698 before");
    }
}
