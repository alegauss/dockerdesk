using FreeWilly.Core.Agent;
using FreeWilly.Core.Api;
using FreeWilly.Tray.Cli;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// <c>do reclaim</c> against a daemon, where what matters is which requests it did not make (DD29).
/// </summary>
/// <remarks>
/// Driven over the pipe rather than against a hand-written double, because the assertion worth making is
/// about the DELETEs that reached the daemon — their method, their path and their absence. A double that
/// counted calls would agree with a reclaim that built the wrong URL.
/// </remarks>
public sealed class AgentReclaimTests
{
    private const string Session = "repro-17";

    private static string Path(string endpoint) => $"/{DockerApi.ApiVersion}/{endpoint}";

    /// <summary>A request line as the daemon recorded it, version and all.</summary>
    private static string Deleting(string endpoint) => $"DELETE {Path(endpoint)} HTTP/1.1";

    /// <summary>Two containers and a volume this session made, beside two that somebody else did.</summary>
    private const string Containers = """
        [{"Id":"aaaaaaaaaaaa0000","Names":["/mine-a"],"Image":"shop/api:latest","State":"exited",
          "Labels":{"dockerdesk.session":"repro-17"},"Ports":[]},
         {"Id":"bbbbbbbbbbbb0000","Names":["/mine-b"],"Image":"shop/api:latest","State":"running",
          "Labels":{"dockerdesk.session":"repro-17"},"Ports":[]},
         {"Id":"cccccccccccc0000","Names":["/theirs"],"Image":"postgres:16-alpine","State":"running",
          "Ports":[]},
         {"Id":"dddddddddddd0000","Names":["/older"],"Image":"shop/api:latest","State":"exited",
          "Labels":{"dockerdesk.session":"repro-16"},"Ports":[]}]
        """;

    private const string Volumes = """
        {"Volumes":[{"Name":"mine-data","Driver":"local","Labels":{"dockerdesk.session":"repro-17"}},
                    {"Name":"postgres-data","Driver":"local"}]}
        """;

    private static FakeDockerDaemon Daemon() => new FakeDockerDaemon()
        .Fails(Path("_ping"), "200 OK", "OK")
        .Json(Path("containers/json?all=1"), Containers)
        .Json(Path("volumes"), Volumes)
        .Fails(Path("containers/mine-a?force=1"), "204 No Content", "")
        .Fails(Path("containers/mine-b?force=1"), "204 No Content", "")
        .Fails(Path("volumes/mine-data"), "204 No Content", "");

    /// <summary>
    /// The token the plan over this fixture prints.
    /// </summary>
    /// <remarks>
    /// Built here rather than scraped out of the first call's output, because a test that read the token
    /// back off the text it is testing would still pass if the token were a constant.
    /// </remarks>
    private static string Token(bool volumes = false) => Reclaim.TokenFor(
        Session,
        volumes
            ? [new(Reclaim.Container, "mine-a", ""), new(Reclaim.Container, "mine-b", ""),
               new(Reclaim.Volume, "mine-data", "")]
            : [new(Reclaim.Container, "mine-a", ""), new(Reclaim.Container, "mine-b", "")]);

    [Fact]
    public async Task Asking_prints_the_plan_and_removes_nothing()
    {
        await using var daemon = Daemon();
        using var api = new DockerApi(daemon.PipeName);
        var output = new StringWriter();

        var code = AgentSurface.DoReclaim(api, ["--session", Session], output);

        Assert.Equal(0, code);
        Assert.Contains("mine-a", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("KEEPING", output.ToString(), StringComparison.Ordinal);

        // The plan is a plan. Nothing is removed until a token comes back over exactly this list.
        Assert.DoesNotContain(daemon.Requested, line => line.StartsWith("DELETE ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_right_token_removes_exactly_what_the_plan_named()
    {
        await using var daemon = Daemon();
        using var api = new DockerApi(daemon.PipeName);
        var output = new StringWriter();

        var code = AgentSurface.DoReclaim(
            api, ["--session", Session, "--confirm", Token()], output);

        Assert.Equal(0, code);

        // Exactly these, in this order, and nothing else: the two containers this session made. The
        // other two containers and both volumes are somebody else's, and a reclaim that took them would
        // be the prune this task exists to replace.
        Assert.Equal(
            [Deleting("containers/mine-a?force=1"), Deleting("containers/mine-b?force=1")],
            daemon.Requested.Where(l => l.StartsWith("DELETE ", StringComparison.Ordinal)).ToArray());
    }

    [Fact]
    public async Task A_volume_needs_its_own_word_and_its_own_token()
    {
        await using var daemon = Daemon();
        using var api = new DockerApi(daemon.PipeName);

        // The container token, replayed with --volumes: the flag changes the list, so it changes the
        // token, so this is refused rather than taking the data along with the containers.
        var replayed = AgentSurface.DoReclaim(
            api, ["--session", Session, "--volumes", "--confirm", Token()], new StringWriter());

        Assert.Equal(1, replayed);
        Assert.DoesNotContain(daemon.Requested, line => line.StartsWith("DELETE ", StringComparison.Ordinal));

        var code = AgentSurface.DoReclaim(
            api, ["--session", Session, "--volumes", "--confirm", Token(volumes: true)],
            new StringWriter());

        Assert.Equal(0, code);
        Assert.Contains(Deleting("volumes/mine-data"), daemon.Requested);
        Assert.DoesNotContain(Deleting("volumes/postgres-data"), daemon.Requested);
    }

    [Fact]
    public async Task A_token_computed_over_a_different_list_removes_nothing()
    {
        await using var daemon = Daemon();
        using var api = new DockerApi(daemon.PipeName);
        var output = new StringWriter();

        // What a plan printed before mine-b was started would have been computed over mine-a alone.
        var stale = Reclaim.TokenFor(Session, [new(Reclaim.Container, "mine-a", "")]);

        var code = AgentSurface.DoReclaim(
            api, ["--session", Session, "--confirm", stale], output);

        Assert.Equal(1, code);
        Assert.DoesNotContain(daemon.Requested, line => line.StartsWith("DELETE ", StringComparison.Ordinal));

        // "wrong is a refusal naming what would go now" - so the caller's next call is a decision
        // rather than an investigation.
        Assert.Contains("mine-b", output.ToString(), StringComparison.Ordinal);
        Assert.Contains(stale, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_parsing_caller_is_told_what_would_go_now_too()
    {
        await using var daemon = Daemon();
        using var api = new DockerApi(daemon.PipeName);
        var output = new StringWriter();

        var code = AgentSurface.DoReclaim(
            api,
            ["--session", Session, "--json", "--confirm",
             Reclaim.TokenFor(Session, [new(Reclaim.Container, "mine-a", "")])],
            output);

        Assert.Equal(1, code);

        // --json is one document, so the list cannot be a second thing printed underneath: without it in
        // the refusal itself, a parsing caller learns the plan changed and never learns to what.
        using var refusal = System.Text.Json.JsonDocument.Parse(output.ToString());
        Assert.Equal(
            "container:mine-a, container:mine-b",
            refusal.RootElement.GetProperty("wouldRemoveNow").GetString());
        Assert.DoesNotContain(daemon.Requested, line => line.StartsWith("DELETE ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cursor_pasted_where_a_token_belongs_is_named_as_one()
    {
        await using var daemon = Daemon();
        using var api = new DockerApi(daemon.PipeName);

        // c: is a context cursor and t: is a log cursor. Three currencies on one surface, and only one
        // of them authorises a delete.
        var code = AgentSurface.DoReclaim(
            api, ["--session", Session, "--confirm", "c:231884"], new StringWriter());

        Assert.Equal(2, code);
        Assert.Empty(daemon.Requested);
    }

    [Fact]
    public async Task Read_changes_says_what_this_session_made_without_a_token()
    {
        await using var daemon = Daemon();
        using var api = new DockerApi(daemon.PipeName);
        var output = new StringWriter();

        var code = AgentSurface.Read(
            AgentSurface.Find(["read", "changes"])!, api, ["--session", Session], output);

        Assert.Equal(0, code);
        var text = output.ToString();
        Assert.Contains("mine-a", text, StringComparison.Ordinal);
        Assert.Contains("mine-data", text, StringComparison.Ordinal);
        Assert.DoesNotContain("theirs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("postgres-data", text, StringComparison.Ordinal);

        // A read issues no token, because there is nothing here to authorise.
        Assert.DoesNotContain(Reclaim.TokenPrefix, text, StringComparison.Ordinal);
    }
}
