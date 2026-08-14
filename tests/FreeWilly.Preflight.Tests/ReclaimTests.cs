using FreeWilly.Core.Agent;
using FreeWilly.Core.Api;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The label, the plan and the token that keeps a delete from acting on a list nobody saw (DD29).
/// </summary>
public sealed class ReclaimTests
{
    private const string Session = "repro-17";

    private static ContainerSummary Container(
        string name, string? session, string key = SessionLabel.Key) => new()
    {
        Id = name + "0000000000000000",
        Names = ["/" + name],
        Image = "shop/api:latest",
        State = "exited",
        Labels = session is null
            ? null
            : new Dictionary<string, string>(StringComparer.Ordinal) { [key] = session },
    };

    private static VolumeSummary Volume(
        string name, string? session, string key = SessionLabel.Key) => new()
    {
        Name = name,
        Driver = "local",
        Labels = session is null
            ? null
            : new Dictionary<string, string>(StringComparer.Ordinal) { [key] = session },
    };

    // ---- where a session comes from ------------------------------------------------------------

    [Fact]
    public void The_variable_names_the_session()
    {
        // The answer to the question the section leaves open: every call is a separate process, so an id
        // minted per invocation would put every object in a session of its own.
        Assert.Equal("repro-17", SessionLabel.Resolve("repro-17", @"D:\shop"));
    }

    [Fact]
    public void Without_it_the_id_is_derived_and_says_that_it_was()
    {
        var derived = SessionLabel.Resolve(named: null, @"D:\shop");

        Assert.StartsWith(SessionLabel.DerivedPrefix, derived, StringComparison.Ordinal);
        Assert.True(SessionLabel.IsDerived(derived));

        // Stable, or the same directory would reclaim to a different scope on every call - and a
        // trailing separator is the same folder, which is exactly how a caller's cwd differs from
        // the path it typed.
        Assert.Equal(derived, SessionLabel.Resolve(null, @"D:\shop\"));
        Assert.NotEqual(derived, SessionLabel.Resolve(null, @"D:\other"));
    }

    [Fact]
    public void A_name_a_person_would_actually_type_is_taken_rather_than_refused()
    {
        // "repro #17" is a caller's own word for its own work. Refusing it over a space would be this
        // tool arguing about naming instead of doing its job.
        Assert.Equal("repro--17", SessionLabel.Resolve("repro #17", @"D:\shop"));
    }

    // ---- the two spellings a machine can be carrying (DD72) ------------------------------------

    [Fact]
    public void What_is_written_is_the_new_key_and_it_is_spelled_out_here()
    {
        // Both literals, because the point of this task is that the key is state on somebody's
        // machine rather than spelling in a build: asserting `SessionLabel.Key` against itself would
        // stay green through exactly the respelling that loses a session.
        Assert.Equal("freewilly.session", SessionLabel.Key);
        Assert.Equal("dockerdesk.session", SessionLabel.LegacyKey);
        Assert.Equal(SessionLabel.Key, SessionLabel.For(Session).Keys.Single());
    }

    [Fact]
    public void An_object_stamped_before_the_rename_is_still_the_callers_own()
    {
        // The failure this exists to stop is silent: with a read that knew only the new key, this
        // container is nobody's, `read changes --session` answers empty on a machine full of the
        // caller's own work, and `do reclaim --session` finds nothing to remove — which reads as
        // "there was nothing there" rather than as an error.
        var before = Container("made-earlier", Session, SessionLabel.LegacyKey);

        Assert.True(SessionLabel.Owns(before.Labels, Session));
        Assert.Equal(Session, SessionLabel.StampedOn(before.Labels));
        Assert.False(SessionLabel.Owns(before.Labels, "repro-16"));
    }

    [Fact]
    public void One_plan_holds_both_generations()
    {
        // The state a machine is actually in during the migration: objects made by the build before
        // the rename beside objects made by the one after it, in one session. A reclaim that took
        // only half would leave the rest behind with no way left to name them.
        var plan = Reclaim.Plan(
            Session,
            [
                Container("made-later", Session),
                Container("made-earlier", Session, SessionLabel.LegacyKey),
                Container("theirs", null),
            ],
            [
                Volume("data-later", Session),
                Volume("data-earlier", Session, SessionLabel.LegacyKey),
            ],
            includeVolumes: true);

        Assert.Equal(
            ["made-earlier", "made-later", "data-earlier", "data-later"],
            plan.Removing.Select(i => i.Name).ToArray());
    }

    [Fact]
    public void The_key_this_build_writes_wins_where_an_object_carries_both()
    {
        // Not a state anything here creates, and reachable the moment somebody re-creates a
        // container from a compose file an older build stamped. The current key is the one this
        // build writes, so it is the one that is true.
        var both = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SessionLabel.LegacyKey] = "repro-16",
            [SessionLabel.Key] = Session,
        };

        Assert.Equal(Session, SessionLabel.StampedOn(both));
    }

    // ---- the plan ------------------------------------------------------------------------------

    [Fact]
    public void A_plan_holds_what_carries_the_label_and_nothing_else()
    {
        var plan = Reclaim.Plan(
            Session,
            [Container("mine-a", Session), Container("theirs", null), Container("other", "repro-16")],
            [Volume("mine-data", Session), Volume("postgres-data", null)],
            includeVolumes: true);

        // The whole point. `prune` cannot tell these apart, which is why nobody delegates it.
        Assert.Equal(
            ["mine-a", "mine-data"],
            plan.Removing.Select(i => i.Name).ToArray());
    }

    [Fact]
    public void A_volume_is_kept_and_said_out_loud()
    {
        var plan = Reclaim.Plan(
            Session,
            [Container("mine-a", Session)],
            [Volume("mine-data", Session)],
            includeVolumes: false);

        Assert.Equal(["mine-a"], plan.Removing.Select(i => i.Name).ToArray());
        Assert.Equal(["mine-data"], plan.Keeping.Select(i => i.Name).ToArray());

        var text = Reclaim.Render(plan);
        Assert.Contains("KEEPING", text, StringComparison.Ordinal);
        Assert.Contains(Reclaim.VolumeReason, text, StringComparison.Ordinal);

        // The confirm line has to be the whole command: copied and re-run without --volumes, a line
        // that had silently included them would refuse against its own token.
        Assert.DoesNotContain("--volumes --confirm", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Taking_the_volumes_too_is_a_different_token()
    {
        ContainerSummary[] containers = [Container("mine-a", Session)];
        VolumeSummary[] volumes = [Volume("mine-data", Session)];

        var withoutData = Reclaim.Plan(Session, containers, volumes, includeVolumes: false);
        var withData = Reclaim.Plan(Session, containers, volumes, includeVolumes: true);

        // So a token issued for the containers cannot be replayed to take the data with them.
        Assert.NotEqual(withoutData.Token, withData.Token);
        Assert.False(Reclaim.Confirms(withData, withoutData.Token));
        Assert.Contains("--volumes --confirm", Reclaim.Render(withData), StringComparison.Ordinal);
    }

    [Fact]
    public void A_container_that_arrived_in_between_invalidates_the_token()
    {
        var printed = Reclaim.Plan(
            Session, [Container("mine-a", Session)], [], includeVolumes: false);
        var now = Reclaim.Plan(
            Session,
            [Container("mine-a", Session), Container("mine-b", Session)],
            [],
            includeVolumes: false);

        Assert.False(Reclaim.Confirms(now, printed.Token));

        var refusal = Reclaim.Stale(now, printed.Token).ToText();
        Assert.Contains(printed.Token, refusal, StringComparison.Ordinal);
        Assert.Contains(now.Token, refusal, StringComparison.Ordinal);
        Assert.Contains("Nothing was removed", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void The_order_the_daemon_answered_in_does_not_change_the_token()
    {
        // Or a plan and its confirm would disagree about a list that did not change at all.
        var one = Reclaim.Plan(
            Session, [Container("a", Session), Container("b", Session)], [], includeVolumes: false);
        var other = Reclaim.Plan(
            Session, [Container("b", Session), Container("a", Session)], [], includeVolumes: false);

        Assert.Equal(one.Token, other.Token);
    }

    // ---- what it costs -------------------------------------------------------------------------

    /// <summary>A file in the repository, found by walking up from the test binary.</summary>
    private static string RepositoryFile(string name)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null)
        {
            var candidate = System.IO.Path.Combine(here.FullName, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            here = here.Parent;
        }

        throw new InvalidOperationException(name + " was not found");
    }

    /// <summary>A ceiling agent-budget.json records, read from the file itself.</summary>
    private static int Ceiling(string shape)
    {
        using var budget = System.Text.Json.JsonDocument.Parse(
            File.ReadAllBytes(RepositoryFile("agent-budget.json")));
        return budget.RootElement.GetProperty("surface").GetProperty("shapes")
            .GetProperty(shape).GetInt32();
    }

    [Fact]
    public void The_token_the_README_prints_is_the_one_this_code_computes()
    {
        // A worked example in a README is a claim, and a stale confirm token is the kind of claim that
        // teaches a reader to expect a refusal. So it is asserted rather than transcribed.
        var readme = File.ReadAllText(RepositoryFile("README.md"));
        var token = Reclaim.TokenFor(
            "repro-17",
            [new(Reclaim.Container, "shop-api-1", ""), new(Reclaim.Container, "shop-db-1", "")]);

        Assert.Contains("--confirm " + token, readme, StringComparison.Ordinal);
    }

    /// <summary>A stack an agent would plausibly have stood up to reproduce something.</summary>
    private static ReclaimPlan Realistic(bool includeVolumes) => Reclaim.Plan(
        Session,
        [Container("shop-api-1", Session), Container("shop-db-1", Session),
         Container("shop-worker-1", Session), Container("shop-cache-1", Session),
         Container("untouched", null)],
        [Volume("shop-data", Session), Volume("shop-cache", Session), Volume("theirs", null)],
        includeVolumes);

    [Fact]
    public void A_plan_stays_under_the_ceiling_recorded_for_it()
    {
        var plan = TokenEstimate.Of(Reclaim.Render(Realistic(includeVolumes: false)));
        Assert.True(
            plan <= Ceiling("do reclaim"),
            $"a four-container plan with two volumes kept is {plan} estimated tokens against the "
            + $"{Ceiling("do reclaim")} recorded in agent-budget.json. Tighten it, or raise the ceiling "
            + "and say in the commit what the tokens bought.");

        var changes = TokenEstimate.Of(Reclaim.RenderChanges(Realistic(includeVolumes: true)));
        Assert.True(
            changes <= Ceiling("read changes"),
            $"the same session's changes are {changes} estimated tokens against the "
            + $"{Ceiling("read changes")} recorded in agent-budget.json.");
    }

    [Fact]
    public void An_empty_plan_confirms_nothing()
    {
        // A token over an empty list is a well-formed token for deleting nothing, and accepting it would
        // make "confirmed" mean nothing in the one case where the caller is most likely to be surprised.
        var empty = Reclaim.Plan(Session, [], [], includeVolumes: true);

        Assert.False(Reclaim.Confirms(empty, empty.Token));
        Assert.Contains("nothing to reclaim", Reclaim.Render(empty), StringComparison.Ordinal);
    }
}
