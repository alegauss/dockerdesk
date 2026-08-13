using FreeWilly.Tray.Ui;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// A heading that sorts, and a box that narrows (DD37).
/// </summary>
public sealed class ListShapeTests
{
    private static ContainerRow Row(string name, string state, string status = "", string image = "img") =>
        new(name, image, state, status, [], name + "-id");

    private static string Page(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FreeWilly.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "the repository root was not found above the test binaries");
        return File.ReadAllText(
            Path.Combine(directory!.FullName, "src/FreeWilly.Tray/Ui/Pages", file));
    }

    // ---- the heading -----------------------------------------------------------------------------

    [Fact]
    public void Clicking_the_sorted_column_flips_it_and_another_starts_at_its_own_direction()
    {
        var shape = new ListShape(ContainerRow.Columns.Name, Descending: false);

        Assert.True(shape.Toggled(ContainerRow.Columns.Name, descendsFirst: false).Descending);

        // Sorting by SIZE and getting the smallest first is the sort nobody wanted, so a column
        // starts where it reads best rather than where the last one happened to be.
        var bySize = shape.Toggled(ImageRow.Columns.Size, descendsFirst: true);
        Assert.Equal(ImageRow.Columns.Size, bySize.Column);
        Assert.True(bySize.Descending);
    }

    [Fact]
    public void Only_the_sorted_heading_carries_a_glyph()
    {
        // Six affordances competing to say which one is in force is no affordance at all.
        var shape = new ListShape(ContainerRow.Columns.State, Descending: true);

        Assert.Equal(" ↓", shape.GlyphFor(ContainerRow.Columns.State));
        Assert.Equal("", shape.GlyphFor(ContainerRow.Columns.Name));
        Assert.Equal(" ↑", shape with { Descending = false } is var up ? up.GlyphFor(ContainerRow.Columns.State) : "");
    }

    // ---- the default order ------------------------------------------------------------------------

    [Fact]
    public void A_container_list_opens_running_first_then_alphabetical()
    {
        // The window is opened to act on something, and the things that can be stopped, shelled into
        // or read are the running ones. Creation order — what the daemon returns — answers neither
        // that question nor "where is the one I am looking for".
        var shown = ContainerRow.Shaped(
            [Row("zeta", "exited"), Row("alpha", "running"), Row("beta", "exited"), Row("gamma", "running")],
            new ListShape(ContainerRow.DefaultColumn, Descending: false));

        Assert.Equal(["alpha", "gamma", "beta", "zeta"], shown.Select(r => r.Name).ToArray());
    }

    [Fact]
    public void The_name_tie_break_stays_ascending_when_the_sort_is_flipped()
    {
        // Rows equal on the sorted column must not swap places when the direction changes: this list
        // redraws on every engine event, and a row moving under the pointer is the defect.
        var rows = new[] { Row("beta", "running"), Row("alpha", "running"), Row("gamma", "running") };

        var down = ContainerRow.Shaped(rows, new ListShape(ContainerRow.Columns.State, Descending: true));

        Assert.Equal(["alpha", "beta", "gamma"], down.Select(r => r.Name).ToArray());
    }

    // ---- the filter ------------------------------------------------------------------------------

    [Fact]
    public void The_filter_matches_any_field_already_on_the_row()
    {
        // Somebody looking for a container by the port it publishes should not have to know that
        // ports are not the name column.
        var shape = new ListShape(ContainerRow.Columns.Name, false, "POSTGRES");

        Assert.True(shape.Keeps("shop-db-1", "postgres:16-alpine"));
        Assert.False(shape.Keeps("shop-api-1", "shop/api:latest"));

        // Nothing typed keeps everything, rather than matching an empty string against each field.
        Assert.True(new ListShape("x", false).Keeps("anything"));
        Assert.True(new ListShape("x", false, "   ").Keeps("anything"));
    }

    [Fact]
    public void Narrowing_keeps_the_order_it_was_narrowed_under()
    {
        var rows = new[] { Row("shop-db-1", "exited"), Row("shop-api-1", "running"), Row("other", "running") };
        var shape = new ListShape(ContainerRow.DefaultColumn, false).Narrowed("shop");

        var shown = ContainerRow.Shaped(rows, shape);

        Assert.Equal(["shop-api-1", "shop-db-1"], shown.Select(r => r.Name).ToArray());
    }

    [Fact]
    public void An_empty_result_says_what_was_typed()
    {
        // The third empty state. "No containers" and "nothing matched api" are different answers and
        // only one of them is fixed by clearing a box.
        var filtered = new ListShape("NAME", false, "nginx").EmptyBecauseFiltered("containers");

        Assert.NotNull(filtered);
        Assert.Contains("nginx", filtered.Value.Headline, StringComparison.Ordinal);

        // And an unfiltered empty list is not this state: it is the engine being down, or a machine
        // with nothing on it.
        Assert.Null(new ListShape("NAME", false).EmptyBecauseFiltered("containers"));
    }

    // ---- what the markup has to carry -------------------------------------------------------------

    [Theory]
    [InlineData("ContainersPage.xaml", 5)]
    [InlineData("ImagesPage.xaml", 4)]
    [InlineData("VolumesPage.xaml", 3)]
    public void Every_heading_sorts_and_every_list_has_a_box(string file, int headings)
    {
        var markup = Page(file);

        // A dead TextBlock heading is the thing this replaced; one left behind is a column that
        // silently does not sort while its neighbours do.
        Assert.Equal(headings, markup.Split("Click=\"SortBy\"").Length - 1);
        Assert.DoesNotContain("Style=\"{DynamicResource Header}\"", markup, StringComparison.Ordinal);

        Assert.Contains("TextChanged=\"FilterChanged\"", markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ContainersPage.xaml.cs")]
    [InlineData("ImagesPage.xaml.cs")]
    [InlineData("VolumesPage.xaml.cs")]
    public void The_shape_outlives_a_refresh(string file)
    {
        // The part DD37 calls easy to get wrong. This window redraws on every engine event, so a sort
        // held by the ListView would be thrown away each time and snap back while somebody was
        // reading it. The page holds it, and every path draws through the one Show().
        var code = Page(file);

        Assert.Contains("private ListShape _shape", code, StringComparison.Ordinal);
        Assert.Contains("private void Show()", code, StringComparison.Ordinal);

        // And the redraw is over the rows in hand, never a second call to the daemon.
        Assert.Contains("_rows", code, StringComparison.Ordinal);
    }

    [Fact]
    public void A_heading_click_asks_the_daemon_for_nothing()
    {
        // The rows are already here and the question is about presentation. A sort that re-read the
        // engine would be a round trip for an answer the window is holding.
        var code = Page("ContainersPage.xaml.cs");
        var sort = code[code.IndexOf("private void SortBy", StringComparison.Ordinal)..];
        var body = sort[..sort.IndexOf("\n    }", StringComparison.Ordinal)];

        Assert.DoesNotContain("_api", body, StringComparison.Ordinal);
        Assert.DoesNotContain("await", body, StringComparison.Ordinal);
    }
}
