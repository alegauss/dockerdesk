using FreeWilly.Core.Agent;
using FreeWilly.Core.Api;
using FreeWilly.Tray.Ui;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The order a project's verb reaches its containers in (DD107).
/// </summary>
/// <remarks>
/// Fanning a verb out in list order is the version that usually works: it leaves a service talking to
/// a database that already went, and on the way up it starts an api against a postgres that has not
/// opened its socket. Both look like the application's bug rather than the button's, which is why the
/// order is worth the code — and why the fallbacks matter more than the happy path.
/// </remarks>
public sealed class ComposeOrderTests
{
    private static ContainerRow Row(
        string service, string? dependsOn = null, string project = "shop") =>
        ContainerRow.From(new ContainerSummary
        {
            Id = service + "-id",
            Names = ["/shop-" + service + "-1"],
            Image = "img:1",
            State = "running",
            Status = "Up 4 minutes",
            Ports = [],
            Labels = Labels(project, service, dependsOn),
        });

    private static Dictionary<string, string> Labels(
        string project, string service, string? dependsOn)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ContextPack.ProjectLabel] = project,
            [ContextPack.ServiceLabel] = service,
        };

        if (dependsOn is not null)
        {
            labels[ComposeOrder.DependsOnLabel] = dependsOn;
        }

        return labels;
    }

    private static IEnumerable<string> Services(IReadOnlyList<ContainerRow> rows) =>
        rows.Select(row => row.Service!);

    // ---- reading the label -----------------------------------------------------------------------

    [Fact]
    public void The_label_names_services_and_the_condition_is_not_an_ordering()
    {
        // Compose writes `db:service_started:false,cache:service_healthy:true`. Only the first field
        // of each entry is an order; the condition is about waiting, which is compose's job.
        Assert.Equal(
            ["db", "cache"],
            ComposeOrder.DependenciesIn("db:service_started:false,cache:service_healthy:true"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_label_is_no_dependency(string? label) =>
        Assert.Empty(ComposeOrder.DependenciesIn(label));

    [Fact]
    public void A_service_named_twice_is_one_dependency() =>
        Assert.Equal(["db"], ComposeOrder.DependenciesIn("db:service_started:false,db:x:y"));

    // ---- the order -------------------------------------------------------------------------------

    [Fact]
    public void What_is_depended_on_starts_first_and_stops_last()
    {
        // The whole point in one assertion. api waits for db, so it goes up after it and comes down
        // before it — which is what keeps a service from talking to a database that already went.
        var children = new[] { Row("api", "db:service_started:false"), Row("db") };

        Assert.Equal(["db", "api"], Services(ComposeOrder.ToStart(children)));
        Assert.Equal(["api", "db"], Services(ComposeOrder.ToStop(children)));
    }

    [Fact]
    public void A_chain_is_walked_the_whole_way_down()
    {
        var children = new[]
        {
            Row("web", "api:service_started:false"),
            Row("api", "db:service_started:false"),
            Row("db"),
        };

        Assert.Equal(["db", "api", "web"], Services(ComposeOrder.ToStart(children)));
    }

    [Fact]
    public void Two_services_that_wait_for_nothing_keep_the_order_the_window_is_showing()
    {
        // The tie-break, and it is not cosmetic: pressing Stop twice must issue the same calls in the
        // same sequence, or a reader watching rows go pending sees a different animation each time
        // for no reason they could name.
        var children = new[] { Row("zebra"), Row("apple"), Row("mango") };

        Assert.Equal(["zebra", "apple", "mango"], Services(ComposeOrder.ToStart(children)));
    }

    [Fact]
    public void A_dependency_on_something_that_is_not_here_is_simply_not_an_edge()
    {
        // `docker rm` on one service leaves the rest carrying a label that names it. That is not a
        // reason to refuse to order the ones that are here.
        var children = new[] { Row("api", "gone:service_started:false"), Row("db") };

        Assert.Equal(["api", "db"], Services(ComposeOrder.ToStart(children)));
    }

    // ---- and where it gives up --------------------------------------------------------------------

    [Fact]
    public void A_cycle_falls_back_rather_than_dropping_half_the_project()
    {
        // Compose refuses to build one, so reaching here means a label this window did not write —
        // and an order that dropped what it could not place would stop half a stack.
        var children = new[]
        {
            Row("a", "b:service_started:false"),
            Row("b", "a:service_started:false"),
        };

        Assert.Equal(["a", "b"], Services(ComposeOrder.ToStart(children)));
    }

    [Fact]
    public void A_container_with_no_service_name_falls_the_whole_project_back()
    {
        // Half-ordered is the answer nobody can reason about: it would be neither the list the
        // window shows nor the order compose would use.
        var bare = ContainerRow.From(new ContainerSummary
        {
            Id = "bare-id",
            Names = ["/bare"],
            Image = "img:1",
            State = "running",
            Status = "Up 4 minutes",
            Ports = [],
            Labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ContextPack.ProjectLabel] = "shop",
            },
        });

        var children = new[] { Row("api", "db:service_started:false"), bare, Row("db") };

        Assert.Equal(["api-id", "bare-id", "db-id"], ComposeOrder.ToStart(children).Select(r => r.Id));
    }

    [Fact]
    public void Two_containers_of_one_service_fall_back_rather_than_being_guessed_at()
    {
        // A scaled service. Which of the two the label meant is not something a window can decide,
        // and picking one would order the project around a container chosen arbitrarily.
        var children = new[] { Row("api"), Row("api"), Row("db") };

        Assert.Equal(3, ComposeOrder.ToStart(children).Count);
        Assert.Equal(["api", "api", "db"], Services(ComposeOrder.ToStart(children)));
    }

    [Fact]
    public void One_container_needs_no_ordering_at_all()
    {
        var one = new[] { Row("api", "db:service_started:false") };

        Assert.Same(one, ComposeOrder.ToStart(one));
    }
}
