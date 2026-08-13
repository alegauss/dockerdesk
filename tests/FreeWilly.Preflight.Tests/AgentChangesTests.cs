using FreeWilly.Core.Agent;
using FreeWilly.Core.Api;
using FreeWilly.Tray.Cli;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// <c>read changes</c> through the surface: the delta, its window, and what <c>--session</c> narrows
/// (DD31, over DD29).
/// </summary>
public sealed class AgentChangesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private const string Session = "repro-17";

    private static string Path(string endpoint) => $"/{DockerApi.ApiVersion}/{endpoint}";

    /// <summary>The window a call with no cursor asks for, spelled the way the daemon takes it.</summary>
    private static string Window(DateTimeOffset since) =>
        $"events?since={since.ToUnixTimeSeconds()}&until={Now.ToUnixTimeSeconds()}";

    /// <summary>Two containers moving, one of them this session's.</summary>
    private const string Events = """
        {"Type":"container","Action":"start","Actor":{"ID":"aaaa","Attributes":{"name":"shop-worker-1","dockerdesk.session":"repro-17"}},"time":1}
        {"Type":"container","Action":"die","Actor":{"ID":"aaaa","Attributes":{"name":"shop-worker-1","dockerdesk.session":"repro-17","exitCode":"137"}},"time":2}
        {"Type":"container","Action":"start","Actor":{"ID":"aaaa","Attributes":{"name":"shop-worker-1","dockerdesk.session":"repro-17"}},"time":3}
        {"Type":"container","Action":"die","Actor":{"ID":"aaaa","Attributes":{"name":"shop-worker-1","dockerdesk.session":"repro-17","exitCode":"137"}},"time":4}
        {"Type":"container","Action":"stop","Actor":{"ID":"bbbb","Attributes":{"name":"theirs-db-1"}},"time":5}
        """;

    private static FakeDockerDaemon Daemon(string window) => new FakeDockerDaemon()
        .Fails(Path("_ping"), "200 OK", "OK")
        .Json(Path(window), Events);

    private static int Changes(FakeDockerDaemon daemon, string[] arguments, TextWriter output)
    {
        using var api = new DockerApi(daemon.PipeName);
        return AgentSurface.ReadChanges(api, arguments, output, Now);
    }

    [Fact]
    public async Task A_bare_call_asks_for_the_default_window_and_answers_with_a_cursor()
    {
        await using var daemon = Daemon(Window(Now - ChangeFeed.DefaultWindow));
        var output = new StringWriter();

        var code = Changes(daemon, [], output);

        Assert.Equal(0, code);
        var text = output.ToString();

        // The whole delta in two lines, which is the point: a follow-up session syncs on this rather
        // than re-deriving the machine.
        Assert.Contains("shop-worker-1", text, StringComparison.Ordinal);
        Assert.Contains("restarted ×1, exited 137", text, StringComparison.Ordinal);
        Assert.Contains("theirs-db-1", text, StringComparison.Ordinal);
        Assert.Contains(ChangeFeed.CursorFor(Now), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cursor_becomes_the_window_the_daemon_is_asked_for()
    {
        var since = Now.AddHours(-2);
        await using var daemon = Daemon(Window(since));
        var output = new StringWriter();

        var code = Changes(daemon, ["--since", ChangeFeed.CursorFor(since)], output);

        // The route was keyed on that exact window, so a 200 proves the URL rather than the parsing.
        Assert.Equal(0, code);
        Assert.Contains(
            $"GET {Path(Window(since))} HTTP/1.1", daemon.Requested);
    }

    [Fact]
    public async Task The_feed_reports_what_the_user_did_and_not_only_what_this_session_did()
    {
        await using var daemon = Daemon(Window(Now - ChangeFeed.DefaultWindow));
        var output = new StringWriter();

        Changes(daemon, [], output);

        // A container the user stopped from the tray is a change. A feed that only reported the
        // agent's own writes would be a memory of its intentions rather than of the machine.
        Assert.Contains("theirs-db-1", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Session_narrows_the_same_delta_to_what_carries_the_label()
    {
        await using var daemon = Daemon(Window(Now - ChangeFeed.DefaultWindow));
        var output = new StringWriter();

        var code = Changes(daemon, ["--session", Session, "--since", ChangeFeed.CursorFor(Now - ChangeFeed.DefaultWindow)], output);

        Assert.Equal(0, code);
        var text = output.ToString();
        Assert.Contains("shop-worker-1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("theirs-db-1", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Session_without_a_cursor_is_still_DD29s_listing_and_asks_no_events_at_all()
    {
        // State rather than history on purpose: what this session created is answered by the label the
        // objects carry, so it is still true after the daemon's ring has rolled past it.
        await using var daemon = new FakeDockerDaemon()
            .Fails(Path("_ping"), "200 OK", "OK")
            .Json(Path("containers/json?all=1"),
                """[{"Id":"aaaa","Names":["/mine-a"],"Image":"shop/api:latest","State":"exited","Labels":{"dockerdesk.session":"repro-17"},"Ports":[]}]""")
            .Json(Path("volumes"), """{"Volumes":[]}""");
        var output = new StringWriter();

        var code = Changes(daemon, ["--session", Session], output);

        Assert.Equal(0, code);
        Assert.Contains("mine-a", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(daemon.Requested, line => line.Contains("events", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_context_cursor_is_refused_before_the_daemon_is_asked_anything()
    {
        await using var daemon = Daemon(Window(Now - ChangeFeed.DefaultWindow));

        var code = Changes(daemon, ["--since", "c:231884"], new StringWriter());

        Assert.Equal(2, code);
        Assert.Empty(daemon.Requested);
    }

    [Fact]
    public async Task A_delta_that_may_be_missing_its_beginning_exits_non_zero()
    {
        // Built by replacement rather than by interpolation: a raw string cannot disambiguate an
        // interpolation hole from the JSON braces that close beside it.
        const string Template =
            """{"Type":"container","Action":"start","Actor":{"ID":"id-@I@","Attributes":{"name":"c-@I@"}},"time":1}""";

        var many = string.Join(
            "\n",
            Enumerable.Range(0, ChangeFeed.DaemonRing).Select(i =>
                Template.Replace("@I@", i.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)));

        await using var daemon = new FakeDockerDaemon()
            .Fails(Path("_ping"), "200 OK", "OK")
            .Json(Path(Window(Now - ChangeFeed.DefaultWindow)), many);
        var output = new StringWriter();

        var code = Changes(daemon, [], output);

        // The exit code carries the one thing a script must not miss, so it does not have to read
        // the text to find out the delta is not complete.
        Assert.Equal(1, code);
        Assert.StartsWith("too old", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_engine_that_is_not_answering_is_a_refusal_and_not_an_empty_delta()
    {
        await using var daemon = new FakeDockerDaemon()
            .Fails(Path("_ping"), "500 Internal Server Error", "no");
        var output = new StringWriter();

        var code = Changes(daemon, [], output);

        // An empty delta reads as "nothing moved", which is a confidently wrong answer about the one
        // thing this verb exists to report.
        Assert.Equal(3, code);
        Assert.DoesNotContain("(nothing moved)", output.ToString(), StringComparison.Ordinal);
    }
}
