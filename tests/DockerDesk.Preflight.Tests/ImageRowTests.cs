using DockerDesk.Core.Api;
using DockerDesk.Tray.Ui;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// The images list: the join the CLI makes you do in your head, and the arithmetic in front of a
/// prune. "Which of these can I delete" is the only question this view exists to answer.
/// </summary>
public sealed class ImageRowTests
{
    private static ImageSummary Image(string id, long size, params string[] tags) =>
        new() { Id = id, Size = size, RepoTags = tags };

    private static ContainerSummary Using(string imageId, string name) =>
        new() { Id = $"c-{name}", Names = [$"/{name}"], ImageId = imageId };

    // ---- sizes, as a person reads them ---------------------------------------------------------

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(999, "999 B")]
    [InlineData(1000, "1.0 KB")]
    [InlineData(48_900_000, "49 MB")]
    [InlineData(1_200_000_000, "1.2 GB")]
    [InlineData(14_000_000_000, "14 GB")]
    public void A_size_reads_the_way_docker_images_prints_it(long bytes, string expected)
    {
        // Decimal units, because that is what the CLI shows. A list that disagrees with the CLI
        // about how big the same image is invites the user to distrust both.
        Assert.Equal(expected, Bytes.Human(bytes));
    }

    [Fact]
    public void Precision_drops_once_the_number_is_big_enough_not_to_need_it()
    {
        Assert.Equal("9.4 GB", Bytes.Human(9_400_000_000));
        Assert.Equal("12 GB", Bytes.Human(12_000_000_000));
    }

    // ---- the join ------------------------------------------------------------------------------

    [Fact]
    public void An_image_a_container_holds_names_that_container()
    {
        // The whole point: the daemon will say how big an image is and what a container was made
        // from, and nothing in the API puts the two together.
        var rows = ImageRow.From(
            [Image("sha256:aaa", 100, "nginx:alpine")],
            [Using("sha256:aaa", "web")]);

        Assert.True(rows[0].IsInUse);
        Assert.Equal(["web"], rows[0].UsedBy);
        Assert.Equal("web", rows[0].Status);
    }

    [Fact]
    public void Two_containers_on_one_image_are_both_named()
    {
        var rows = ImageRow.From(
            [Image("sha256:aaa", 100, "nginx:alpine")],
            [Using("sha256:aaa", "web"), Using("sha256:aaa", "web2")]);

        Assert.Equal("web, web2", rows[0].Status);
    }

    [Fact]
    public void The_join_is_on_the_image_id_and_not_on_the_name()
    {
        // A tag moves. A container created from nginx:alpine yesterday holds the image that tag
        // used to point at, and joining on the name would report the old one as free.
        var rows = ImageRow.From(
            [Image("sha256:old", 100), Image("sha256:new", 100, "nginx:alpine")],
            [new ContainerSummary
            {
                Id = "c1", Names = ["/web"], Image = "nginx:alpine", ImageId = "sha256:old",
            }]);

        var old = rows.Single(row => row.Id == "sha256:old");
        var current = rows.Single(row => row.Id == "sha256:new");
        Assert.True(old.IsInUse);
        Assert.False(current.IsInUse);
    }

    [Fact]
    public void A_stopped_container_still_holds_its_image()
    {
        // The daemon refuses to delete it either way, so a list that only counted running ones
        // would offer a removal that cannot succeed.
        var rows = ImageRow.From(
            [Image("sha256:aaa", 100, "postgres:16")],
            [new ContainerSummary
            {
                Id = "c1", Names = ["/pgdata"], State = "exited", ImageId = "sha256:aaa",
            }]);

        Assert.True(rows[0].IsInUse);
    }

    // ---- dangling ------------------------------------------------------------------------------

    [Fact]
    public void An_image_with_no_tags_at_all_is_dangling()
    {
        var rows = ImageRow.From([Image("sha256:aaa", 100)], []);

        Assert.True(rows[0].IsDangling);
        Assert.Equal("dangling", rows[0].Status);
        Assert.Equal("<none>", rows[0].Repository);
    }

    [Fact]
    public void An_image_tagged_none_none_is_the_same_thing_said_differently()
    {
        // The daemon reports an untagged image both ways depending on version and on how it lost
        // its tag, and a reader that knows only one of them under-reports what can be reclaimed.
        var rows = ImageRow.From([Image("sha256:aaa", 100, "<none>:<none>")], []);

        Assert.True(rows[0].IsDangling);
    }

    [Fact]
    public void A_tagged_image_nothing_holds_is_unused_rather_than_dangling()
    {
        // Different answers: prune will not touch this one, and deleting it is a real decision.
        var rows = ImageRow.From([Image("sha256:aaa", 100, "redis:7")], []);

        Assert.False(rows[0].IsDangling);
        Assert.False(rows[0].IsInUse);
        Assert.Equal("unused", rows[0].Status);
        Assert.Equal("redis", rows[0].Repository);
        Assert.Equal("7", rows[0].Tag);
    }

    [Fact]
    public void A_registry_prefixed_tag_splits_on_the_last_colon_and_not_the_port()
    {
        var rows = ImageRow.From([Image("sha256:aaa", 100, "registry.local:5000/app:2.1")], []);

        Assert.Equal("registry.local:5000/app", rows[0].Repository);
        Assert.Equal("2.1", rows[0].Tag);
    }

    // ---- order ---------------------------------------------------------------------------------

    [Fact]
    public void The_biggest_image_is_first_because_the_decision_is_about_disk()
    {
        var rows = ImageRow.From(
            [Image("sha256:a", 100, "small:1"), Image("sha256:b", 9_000, "big:1"),
             Image("sha256:c", 500, "middle:1")],
            []);

        Assert.Equal(["big", "middle", "small"], rows.Select(row => row.Repository));
    }

    // ---- the totals and what prune promises ----------------------------------------------------

    [Fact]
    public void The_summary_states_the_total_and_what_prune_would_give_back()
    {
        var rows = ImageRow.From(
            [Image("sha256:a", 1_000_000_000, "nginx:alpine"), Image("sha256:b", 200_000_000)],
            []);

        var totals = ImageTotals.For(rows);

        Assert.Equal(1_200_000_000, totals.Total);
        Assert.Equal(200_000_000, totals.Reclaimable);
        Assert.Equal(1, totals.DanglingCount);
        Assert.Contains("1.2 GB in images", totals.Summary, StringComparison.Ordinal);
        Assert.Contains("200 MB reclaimable", totals.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dangling_image_a_container_still_holds_is_not_counted_as_reclaimable()
    {
        // Real: a container built from an image whose tag later moved. Prune leaves it alone, so
        // counting it would promise space that is not coming back.
        var rows = ImageRow.From(
            [Image("sha256:a", 500_000_000)],
            [Using("sha256:a", "web")]);

        var totals = ImageTotals.For(rows);

        Assert.Equal(0, totals.Reclaimable);
        Assert.Equal(0, totals.DanglingCount);
        Assert.False(totals.CanPrune);
    }

    [Fact]
    public void With_nothing_dangling_prune_is_off_and_the_summary_says_so()
    {
        var totals = ImageTotals.For(ImageRow.From([Image("sha256:a", 100, "nginx:alpine")], []));

        Assert.False(totals.CanPrune);
        Assert.Contains("nothing dangling", totals.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void The_question_names_the_count_and_the_space_before_anything_is_clicked()
    {
        // "Are you sure" about an unknown quantity is a question nobody can answer.
        var rows = ImageRow.From(
            [Image("sha256:a", 700_000_000), Image("sha256:b", 500_000_000)], []);

        var question = ImageTotals.For(rows).PruneQuestion;

        Assert.Contains("2 dangling images", question, StringComparison.Ordinal);
        Assert.Contains("1.2 GB", question, StringComparison.Ordinal);
        Assert.Contains("Tagged images stay", question, StringComparison.Ordinal);
    }

    [Fact]
    public void One_dangling_image_is_singular_in_both_sentences()
    {
        var totals = ImageTotals.For(ImageRow.From([Image("sha256:a", 100)], []));

        Assert.Contains("Delete 1 dangling image and", totals.PruneQuestion, StringComparison.Ordinal);
        Assert.DoesNotContain("1 dangling images", totals.PruneQuestion, StringComparison.Ordinal);
        Assert.DoesNotContain("1 dangling images", totals.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void The_outcome_reports_the_daemon_s_own_numbers_rather_than_the_estimate()
    {
        // Another client may have taken some of it between the question and the answer.
        var outcome = ImageTotals.PruneOutcome(new ImagesPruned
        {
            SpaceReclaimed = 1_200_000_000,
            ImagesDeleted =
            [
                new DeletedImage { Deleted = "sha256:a" },
                new DeletedImage { Untagged = "nginx:alpine" },
                new DeletedImage { Deleted = "sha256:b" },
            ],
        });

        // Untagged entries are layers losing a name, not images going away.
        Assert.Contains("Removed 2 images", outcome, StringComparison.Ordinal);
        Assert.Contains("1.2 GB", outcome, StringComparison.Ordinal);
    }

    [Fact]
    public void A_prune_that_found_nothing_says_that_rather_than_reporting_zero_bytes()
    {
        var outcome = ImageTotals.PruneOutcome(new ImagesPruned { SpaceReclaimed = 0 });

        Assert.Contains("Nothing was dangling", outcome, StringComparison.Ordinal);
    }

    // ---- when the list has to be re-read --------------------------------------------------------

    [Theory]
    [InlineData("image", "pull")]
    [InlineData("image", "delete")]
    [InlineData("image", "untag")]
    [InlineData("container", "create")]
    [InlineData("container", "destroy")]
    public void An_event_that_changes_what_is_on_disk_or_what_holds_it_asks_for_a_read(
        string type, string action)
    {
        Assert.True(new DockerEvent { Type = type, Action = action }.ChangesTheImageList);
    }

    [Theory]
    [InlineData("container", "start")]
    [InlineData("container", "die")]
    [InlineData("network", "connect")]
    [InlineData("container", "exec_start")]
    public void An_event_that_changes_neither_does_not(string type, string action)
    {
        // A container starting held its image before and holds it after. Re-reading two lists for
        // that is the poll this project keeps not writing.
        Assert.False(new DockerEvent { Type = type, Action = action }.ChangesTheImageList);
    }
}
