using System.Collections;
using System.Windows.Controls;
using FreeWilly.Tray.Ui;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The list reconciled rather than replaced, which is what lets a row say it arrived (DD70).
/// </summary>
/// <remarks>
/// The fade itself is not asserted here and could not usefully be: an animation on a container the
/// generator has not built yet is a no-op, and a test that drove the dispatcher long enough to see
/// one would be asserting WPF's scheduler. What is asserted is the part that decides <em>which</em>
/// rows are news, which is the part that would flash the whole list if it were wrong — and the part
/// a capture cannot see.
/// </remarks>
[Collection(ConsoleCollection.Name)]
public sealed class LiveRowsTests
{
    private sealed record Row(string Id, string Text);

    /// <summary>Run a body on a thread WPF will talk to, and surface whatever it threw.</summary>
    private static void OnUiThread(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }

    /// <summary>Drive a list with motion off, which is the deterministic half.</summary>
    private static void WithList(Action<ItemsControl, LiveRows<Row>> body) =>
        OnUiThread(() =>
        {
            var was = Motion.Still;
            Motion.Still = true;
            try
            {
                var host = new ItemsControl();
                body(host, new LiveRows<Row>(host, row => row.Id));
            }
            finally
            {
                Motion.Still = was;
            }
        });

    private static string[] Shown(ItemsControl host) =>
        [.. ((IEnumerable)host.ItemsSource).Cast<Row>().Select(r => r.Id)];

    [Fact]
    public void The_first_draw_is_the_whole_list_and_none_of_it_is_news()
    {
        // A page that just opened is not a page where five containers arrived. Fading the first
        // draw would be the flash this exists to avoid, on the one refresh that is guaranteed.
        WithList((host, live) =>
        {
            live.Show([new Row("a", "one"), new Row("b", "two")]);

            Assert.Equal(["a", "b"], Shown(host));
        });
    }

    [Fact]
    public void A_row_that_joined_is_inserted_where_it_belongs()
    {
        WithList((host, live) =>
        {
            live.Show([new Row("a", "one"), new Row("c", "three")]);
            live.Show([new Row("a", "one"), new Row("b", "two"), new Row("c", "three")]);

            Assert.Equal(["a", "b", "c"], Shown(host));
        });
    }

    [Fact]
    public void A_row_that_left_goes_at_once_when_motion_is_off()
    {
        // The end state rather than a slower version of it — the rule the dot and the water already
        // follow, and what keeps a capture from catching a row mid-fade.
        WithList((host, live) =>
        {
            live.Show([new Row("a", "one"), new Row("b", "two")]);
            live.Show([new Row("b", "two")]);

            Assert.Equal(["b"], Shown(host));
        });
    }

    [Fact]
    public void The_same_rows_reordered_are_moved_rather_than_rebuilt()
    {
        // A heading click and a keystroke both redraw from rows already in hand. Nothing arrived, so
        // nothing should read as arriving.
        WithList((host, live) =>
        {
            live.Show([new Row("a", "one"), new Row("b", "two"), new Row("c", "three")]);
            live.Show([new Row("c", "three"), new Row("b", "two"), new Row("a", "one")]);

            Assert.Equal(["c", "b", "a"], Shown(host));
        });
    }

    [Fact]
    public void A_row_whose_values_changed_keeps_its_place()
    {
        // The container's state moving from running to exited is a cell changing, not a row
        // arriving: the chip says so itself and a fade would claim something else happened.
        WithList((host, live) =>
        {
            live.Show([new Row("a", "running"), new Row("b", "two")]);
            live.Show([new Row("a", "exited"), new Row("b", "two")]);

            Assert.Equal(["a", "b"], Shown(host));
            Assert.Equal(
                "exited",
                ((IEnumerable)host.ItemsSource).Cast<Row>().First(r => r.Id == "a").Text);
        });
    }

    [Fact]
    public void An_emptied_list_empties()
    {
        WithList((host, live) =>
        {
            live.Show([new Row("a", "one")]);
            live.Show([]);

            Assert.Empty(Shown(host));
        });
    }
}
