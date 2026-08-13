using System.Text.Json;
using DockerDesk.Core.Agent;
using DockerDesk.Core.Api;
using DockerDesk.Core.Preflight;
using DockerDesk.Tray.Cli;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// Every refusal carries the fact that explains it (DD28).
/// </summary>
/// <remarks>
/// <c>port is already allocated</c> is the refusal an agent cannot act on: the daemon knows a bind
/// failed and no Docker command anywhere knows what holds the socket. A Windows process does, and
/// <c>heldBy</c> is the argument for this surface existing at all — a JSON re-wrapping of what
/// <c>docker</c> already says adds nothing, since <c>--format json</c> exists.
/// </remarks>
public sealed class AgentProblemTests
{
    /// <summary>Answers whatever it was told to, so a port holder is a fixture rather than a machine.</summary>
    private sealed class FakeOwners(params (int Port, PortHolder Holder)[] held) : IPortOwners
    {
        public PortHolder? Holding(int port) =>
            held.FirstOrDefault(h => h.Port == port).Holder;
    }

    // ---- the fact only Windows has -------------------------------------------------------------

    [Fact]
    public void A_held_port_names_the_process_that_holds_it()
    {
        var problem = AgentProblem.PortAllocated(
            8080, new PortHolder(14032, "node.exe", @"d:\Git\other-project\node.exe"));

        Assert.Contains("pid 14032", problem.ToText(), StringComparison.Ordinal);
        Assert.Contains("node.exe", problem.ToText(), StringComparison.Ordinal);
        // The fix names the action, not the diagnosis.
        Assert.Contains("Stop process 14032", problem.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void A_path_this_process_may_not_read_is_reported_as_absent_rather_than_guessed()
    {
        // MainModule is refused for a service running as another user. A pid and an image are already
        // enough to act on, so its absence is stated rather than fabricated.
        var problem = AgentProblem.PortAllocated(8080, new PortHolder(4, "System.exe", null));

        Assert.Contains("not readable", problem.ToText(), StringComparison.Ordinal);
        Assert.Contains("pid 4", problem.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_port_held_by_something_unidentifiable_still_says_what_to_do()
    {
        var problem = AgentProblem.PortAllocated(8080, holder: null);

        Assert.False(string.IsNullOrWhiteSpace(problem.Fix));
        Assert.Contains("different host port", problem.Fix, StringComparison.Ordinal);
    }

    // ---- one sentence, three causes -------------------------------------------------------------

    [Fact]
    public void A_rival_engine_is_named_as_the_cause_rather_than_reported_as_cannot_connect()
    {
        var problem = AgentProblem.CannotConnect(
            [new RivalEngine("Docker Desktop", "docker resolves to C:\\x\\docker.exe")],
            client: null,
            ourPipe: "docker_engine");

        Assert.Equal("rival-engine", problem.Type);
        Assert.Contains("Docker Desktop", problem.Title, StringComparison.Ordinal);
        Assert.Contains("docker.exe", problem.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_context_pointing_elsewhere_is_a_different_cause_with_a_different_remedy()
    {
        var problem = AgentProblem.CannotConnect(
            [],
            new DockerClientTarget
            {
                ContextName = "desktop-linux",
                Host = "npipe:////./pipe/dockerDesktopLinuxEngine",
            },
            ourPipe: "docker_engine");

        Assert.Equal("context-elsewhere", problem.Type);
        Assert.Contains("desktop-linux", problem.Title, StringComparison.Ordinal);
        Assert.Contains("docker context use default", problem.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void DOCKER_HOST_deciding_is_said_differently_because_the_remedy_differs()
    {
        // `docker context use` cannot win against the variable, so naming it would be advice that
        // changes nothing - the same trap DD20 already found.
        var problem = AgentProblem.CannotConnect(
            [],
            new DockerClientTarget { Host = "tcp://10.0.0.5:2375", FromEnvironment = true },
            ourPipe: "docker_engine");

        Assert.Contains("DOCKER_HOST", problem.Title, StringComparison.Ordinal);
        Assert.DoesNotContain("context use", problem.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void An_engine_that_is_simply_down_is_the_third_cause()
    {
        var problem = AgentProblem.CannotConnect(
            [],
            new DockerClientTarget { ContextName = "default", Host = "npipe:////./pipe/docker_engine" },
            ourPipe: "docker_engine");

        Assert.Equal("engine-stopped", problem.Type);
        Assert.Equal(503, problem.Status);
        Assert.Contains("do engine start", problem.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void The_three_causes_have_three_different_types()
    {
        // An error that names the wrong cause is worse than none, so a caller branches on the type
        // rather than on the prose.
        var rival = AgentProblem.CannotConnect(
            [new RivalEngine("Docker Desktop", "x")], null, "docker_engine").Type;
        var context = AgentProblem.CannotConnect(
            [], new DockerClientTarget { ContextName = "other", Host = "npipe:////./pipe/other" },
            "docker_engine").Type;
        var stopped = AgentProblem.CannotConnect(
            [], new DockerClientTarget { Host = "npipe:////./pipe/docker_engine" }, "docker_engine").Type;

        Assert.Equal(3, new[] { rival, context, stopped }.Distinct(StringComparer.Ordinal).Count());
    }

    // ---- what a refusal always has --------------------------------------------------------------

    [Fact]
    public void Every_refusal_names_an_action()
    {
        foreach (var problem in new[]
        {
            AgentProblem.PortAllocated(8080, new PortHolder(1, "a.exe", null)),
            AgentProblem.PortAllocated(8080, null),
            AgentProblem.CannotConnect([new RivalEngine("x", "y")], null, "docker_engine"),
            AgentProblem.CannotConnect([], null, "docker_engine"),
            AgentProblem.NoSuchName("container", "shop-ap", ["shop-api-1"]),
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(problem.Fix), $"{problem.Type} has no fix");
            Assert.False(string.IsNullOrWhiteSpace(problem.Title), $"{problem.Type} has no title");
        }
    }

    [Fact]
    public void A_name_that_does_not_exist_offers_the_nearest_one_that_does()
    {
        var problem = AgentProblem.NoSuchName("container", "shop-ap", ["shop-api-1", "shop-db-1"]);

        Assert.Equal("shop-api-1", problem.NearestMatch);
        Assert.Contains("Did you mean shop-api-1", problem.Fix, StringComparison.Ordinal);
        Assert.Contains("shop-db-1", problem.Allowed!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_name_nothing_is_close_to_gets_no_suggestion()
    {
        // A suggestion that is not close sends a caller to spend a call on the wrong thing.
        var problem = AgentProblem.NoSuchName("container", "postgres", ["shop-api-1"]);

        Assert.Null(problem.NearestMatch);
        Assert.Contains("read context", problem.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("shop-api-1", "shop-api-1")]
    [InlineData("shop-api", "shop-api-1")]
    [InlineData("shop-apo-1", "shop-api-1")]
    [InlineData("completely-different", null)]
    public void The_nearest_match_is_only_offered_when_it_is_near(string given, string? expected) =>
        Assert.Equal(expected, AgentProblem.Nearest(given, ["shop-api-1", "postgres-main"]));

    // ---- the two shapes -------------------------------------------------------------------------

    [Fact]
    public void The_json_form_carries_the_same_facts_under_stable_names()
    {
        var problem = AgentProblem.PortAllocated(
            8080, new PortHolder(14032, "node.exe", @"d:\Git\other"));

        using var json = JsonDocument.Parse(problem.ToJson());
        var root = json.RootElement;

        Assert.EndsWith("port-allocated", root.GetProperty("type").GetString()!, StringComparison.Ordinal);
        Assert.Equal(409, root.GetProperty("status").GetInt32());
        Assert.Contains("14032", root.GetProperty("heldBy").GetString()!, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("fix").GetString()));
    }

    [Fact]
    public void The_line_form_is_the_default_and_is_cheaper_than_the_json_one()
    {
        // One convention for the whole surface: lines unless --json. A refusal an agent can act on is
        // read rather than parsed, and the section is explicit that a refusal should be cheap.
        var problem = AgentProblem.PortAllocated(
            8080, new PortHolder(14032, "node.exe", @"d:\Git\other"));

        Assert.True(
            TokenEstimate.Of(problem.ToText()) < TokenEstimate.Of(problem.ToJson()),
            $"lines {TokenEstimate.Of(problem.ToText())} vs json {TokenEstimate.Of(problem.ToJson())}");
    }

    // ---- read ports ------------------------------------------------------------------------------

    [Fact]
    public void One_port_is_answered_without_asking_the_engine_at_all()
    {
        // The interesting case is exactly the one Docker has nothing to say about: something that is
        // not a container holds the port. Asking the daemon first would fail on the machine where the
        // question matters most.
        var output = new StringWriter();

        var code = AgentSurface.ReadPorts(
            new ThrowingEngine(), ["8080"], output,
            new FakeOwners((8080, new PortHolder(14032, "node.exe", @"d:\Git\other"))));

        Assert.Equal(0, code);
        Assert.Contains("14032", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("node.exe", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_free_port_says_so()
    {
        var output = new StringWriter();

        AgentSurface.ReadPorts(new ThrowingEngine(), ["9999"], output, new FakeOwners());

        Assert.Contains("free", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_json_form_of_a_free_port_parses()
    {
        var output = new StringWriter();

        AgentSurface.ReadPorts(new ThrowingEngine(), ["9999", "--json"], output, new FakeOwners());

        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("heldBy").ValueKind);
    }

    [Theory]
    [InlineData("70000")]
    [InlineData("0")]
    [InlineData("--nope")]
    public void An_argument_that_is_not_a_port_is_refused(string argument)
    {
        var was = Console.Error;
        try
        {
            Console.SetError(new StringWriter());
            Assert.Equal(2, AgentSurface.ReadPorts(
                new ThrowingEngine(), [argument], new StringWriter(), new FakeOwners()));
        }
        finally
        {
            Console.SetError(was);
        }
    }

    // ---- the trap that names the wrong process --------------------------------------------------

    [Theory]
    [InlineData(0x5000u, 80)]
    [InlineData(0x901Fu, 8080)]
    [InlineData(0x3500u, 53)]
    public void A_port_is_read_in_the_order_the_table_stores_it(uint stored, int expected) =>
        // The table keeps the port big-endian inside a 32-bit field. Reading it as a number gives 20480
        // for port 80, which is the classic way to report the wrong process with complete confidence.
        Assert.Equal(expected, PortOwners.HostOrder(stored));

    [Fact]
    public void The_real_table_answers_without_throwing()
    {
        // Not asserted on content: whatever is listening on this machine is not this test's business.
        // What is asserted is that the read is safe, since it is a P/Invoke against a native table.
        var owners = new PortOwners();

        var holder = owners.Holding(1);

        Assert.True(holder is null || holder.Pid > 0);
    }

    [Fact]
    public void A_port_outside_the_range_is_a_defect_here_rather_than_a_lookup() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PortOwners().Holding(0));

    /// <summary>An engine that fails if anything asks it, so "did not ask" is provable.</summary>
    private sealed class ThrowingEngine : IEngineReads
    {
        public Task<bool> PingAsync(CancellationToken cancellation = default) =>
            throw new InvalidOperationException("the engine was asked and should not have been");

        public Task<EngineVersion> VersionAsync(CancellationToken cancellation = default) =>
            throw new InvalidOperationException("the engine was asked and should not have been");

        public Task<IReadOnlyList<ContainerSummary>> ContainersAsync(
            bool all = true, CancellationToken cancellation = default) =>
            throw new InvalidOperationException("the engine was asked and should not have been");

        public Task<ContainerInspect> InspectAsync(string id, CancellationToken cancellation = default) =>
            throw new InvalidOperationException("the engine was asked and should not have been");

        public Task<IReadOnlyList<ImageSummary>> ImagesAsync(CancellationToken cancellation = default) =>
            throw new InvalidOperationException("the engine was asked and should not have been");

        public Task<IReadOnlyList<VolumeSummary>> VolumesAsync(CancellationToken cancellation = default) =>
            throw new InvalidOperationException("the engine was asked and should not have been");

        public Task<Stream> LogsAsync(
            string id,
            int tail = 2000,
            bool follow = true,
            bool timestamps = false,
            DateTimeOffset? since = null,
            CancellationToken cancellation = default) =>
            throw new InvalidOperationException("the engine was asked and should not have been");
    }
}
