using System.Text.Json;
using FreeWilly.Core.Agent;
using FreeWilly.Core.Api;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The one call that replaces the session's first five (DD25).
/// </summary>
/// <remarks>
/// The section names four properties and says none of them is cosmetic, so each one is a test here:
/// deterministic order, name addressing, a hard ceiling with an explicit truncation cursor, and state
/// stated rather than probed. The rendering is a pure function of <see cref="ContextFacts"/>, which is
/// what lets a machine with an OOM-killed container be one of these rather than something to arrange.
/// </remarks>
public sealed class ContextPackTests
{
    private static ContainerSummary Container(
        string name,
        string state,
        string status,
        string? project = "shop",
        string? service = null,
        int port = 0,
        string? id = null) => new()
    {
        Id = id ?? name + "000000000000",
        Names = [$"/{name}"],
        Image = "shop/" + name + ":latest",
        State = state,
        Status = status,
        Labels = project is null ? null : new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["com.docker.compose.project"] = project,
            ["com.docker.compose.service"] = service ?? name.Replace("shop-", "").Replace("-1", ""),
        },
        Ports = port == 0
            ? []
            : [new PortBinding { PrivatePort = port, PublicPort = port, Type = "tcp", Ip = "0.0.0.0" }],
    };

    private static ContextFacts Facts(
        IReadOnlyList<ContainerSummary>? containers = null,
        IReadOnlyDictionary<string, ContainerInspect>? diagnoses = null,
        IReadOnlyList<ImageSummary>? images = null,
        int volumes = 0,
        string engineState = "running",
        string? context = "default",
        bool reaches = true) => new(
            EngineState: engineState,
            Distribution: "dockerdesk",
            ApiVersion: "v1.43",
            ContextName: context,
            ContextReachesEngine: reaches,
            Containers: containers ?? [],
            Diagnoses: diagnoses ?? new Dictionary<string, ContainerInspect>(StringComparer.Ordinal),
            Images: images ?? [],
            VolumeCount: volumes);

    // ---- state stated rather than probed --------------------------------------------------------

    [Fact]
    public void The_engine_line_states_what_is_there_so_nothing_has_to_be_probed()
    {
        var text = ContextPack.Render(Facts());

        var engine = text.Split(Environment.NewLine)[0];
        Assert.StartsWith("engine  running", engine, StringComparison.Ordinal);
        Assert.Contains("wsl:dockerdesk", engine, StringComparison.Ordinal);
        Assert.Contains("api=v1.43", engine, StringComparison.Ordinal);
        Assert.Contains("ctx=default(ok)", engine, StringComparison.Ordinal);
    }

    [Fact]
    public void A_context_pointing_elsewhere_says_so_on_the_engine_line() =>
        // DD20's fact, carried where a session reads it first: a CLI aimed at another pipe is why the
        // tool looks broken with nothing wrong with it.
        Assert.Contains(
            "ctx=desktop-linux(elsewhere)",
            ContextPack.Render(Facts(context: "desktop-linux", reaches: false)),
            StringComparison.Ordinal);

    [Fact]
    public void An_engine_that_is_down_still_renders_a_pack()
    {
        // Not an error and not an empty string: the caller asked what the machine is doing, and "the
        // engine is stopped" is the answer rather than a failure to answer.
        var text = ContextPack.Render(Facts(engineState: "stopped", context: null));

        Assert.StartsWith("engine  stopped", text, StringComparison.Ordinal);
        Assert.Contains("ctx=?", text, StringComparison.Ordinal);
        Assert.Contains("cursor  c:", text, StringComparison.Ordinal);
    }

    // ---- deterministic order --------------------------------------------------------------------

    [Fact]
    public void Rows_are_sorted_by_name_whatever_order_the_daemon_answered_in()
    {
        var forwards = ContextPack.Render(Facts([
            Container("shop-api-1", "running", "Up 4 minutes"),
            Container("shop-db-1", "running", "Up 4 minutes"),
        ]));
        var backwards = ContextPack.Render(Facts([
            Container("shop-db-1", "running", "Up 4 minutes"),
            Container("shop-api-1", "running", "Up 4 minutes"),
        ]));

        // The daemon answers in creation order, which moves the moment anything is recreated. A payload
        // whose order moves cannot be diffed and cannot be cached.
        Assert.Equal(forwards, backwards);
    }

    [Fact]
    public void The_cursor_is_the_same_for_the_same_machine_and_different_for_a_changed_one()
    {
        var before = ContextPack.Render(Facts([Container("shop-api-1", "running", "Up 4 minutes")]));
        var same = ContextPack.Render(Facts([Container("shop-api-1", "running", "Up 4 minutes")]));
        var after = ContextPack.Render(Facts([Container("shop-api-1", "exited", "Exited (0) 1 second ago")]));

        Assert.Equal(Cursor(before), Cursor(same));
        Assert.NotEqual(Cursor(before), Cursor(after));
    }

    // ---- name addressing ------------------------------------------------------------------------

    [Fact]
    public void A_compose_container_carries_its_service_address()
    {
        var text = ContextPack.Render(Facts([
            Container("shop-api-1", "running", "Up 4 minutes", service: "api", port: 8080),
        ]));

        Assert.Contains("svc:shop/api", text, StringComparison.Ordinal);
        Assert.Contains("8080->8080/tcp", text, StringComparison.Ordinal);
        // The claim DD30 owns is absent on purpose: the mapping is stated, whether it answers is not.
        Assert.DoesNotContain("listening", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_container_with_no_compose_labels_is_still_a_row()
    {
        var text = ContextPack.Render(Facts([
            Container("lonely", "running", "Up 2 hours", project: null),
        ]));

        Assert.Contains("lonely", text, StringComparison.Ordinal);
        Assert.DoesNotContain("svc:", text, StringComparison.Ordinal);
    }

    // ---- what closes the canonical task ---------------------------------------------------------

    [Fact]
    public void An_OOM_killed_container_says_OOM_and_its_limit_in_the_first_call()
    {
        // The whole argument for the command. The constitution's sample closes the canonical task's
        // question with OOM limit=512m, and DD23 measured what asking for that inspect separately
        // costs: 1603 estimated tokens for four leaves of an entity tree.
        var api = Container("shop-worker-1", "exited", "Exited (137) 12 seconds ago", service: "worker");
        var text = ContextPack.Render(Facts(
            [api],
            new Dictionary<string, ContainerInspect>(StringComparer.Ordinal)
            {
                [api.Id] = new()
                {
                    Id = api.Id,
                    RestartCount = 3,
                    State = new ContainerState { Status = "exited", ExitCode = 137, OomKilled = true },
                    HostConfig = new ContainerHostConfig { Memory = 512L * 1024 * 1024 },
                },
            }));

        Assert.Contains("exited 137", text, StringComparison.Ordinal);
        Assert.Contains("OOM", text, StringComparison.Ordinal);
        Assert.Contains("limit=512M", text, StringComparison.Ordinal);
        Assert.Contains("×3", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Health_and_the_exit_code_come_out_of_the_list_without_an_inspect()
    {
        // Both are already in the list's own Status sentence, so reading them from an inspect would be
        // paying the projection cost for something in hand.
        var text = ContextPack.Render(Facts([
            Container("shop-api-1", "running", "Up 4 minutes (healthy)", service: "api"),
            Container("shop-db-1", "running", "Up 11 minutes (unhealthy)", service: "db"),
            Container("shop-old-1", "exited", "Exited (0) 3 days ago", service: "old"),
        ]));

        Assert.Contains("up 4m (healthy)", text, StringComparison.Ordinal);
        Assert.Contains("up 11m (unhealthy)", text, StringComparison.Ordinal);
        Assert.Contains("exited 0", text, StringComparison.Ordinal);
    }

    // ---- the disk line --------------------------------------------------------------------------

    [Fact]
    public void Images_are_totalled_and_dangling_ones_named()
    {
        var text = ContextPack.Render(Facts(
            images:
            [
                new ImageSummary { Size = 12L * 1024 * 1024 * 1024, RepoTags = ["shop/api:latest"] },
                new ImageSummary { Size = 2L * 1024 * 1024 * 1024, RepoTags = ["<none>:<none>"] },
                new ImageSummary { Size = 1L * 1024 * 1024 * 1024, RepoTags = [] },
            ],
            volumes: 3));

        Assert.Contains("images 15G (3G dangling)", text, StringComparison.Ordinal);
        // Counted, not sized: /system/df walks the filesystem and is seconds on a machine with data.
        Assert.Contains("volumes 3", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0L, "0B")]
    [InlineData(512L, "512B")]
    [InlineData(1024L, "1K")]
    [InlineData(1536L, "1.5K")]
    [InlineData(536870912L, "512M")]
    [InlineData(15L * 1024 * 1024 * 1024, "15G")]
    public void Bytes_read_as_two_significant_figures(long bytes, string expected) =>
        Assert.Equal(expected, ContextPack.Bytes(bytes));

    // ---- the hard ceiling, and never a silent cut -----------------------------------------------

    [Fact]
    public void A_machine_under_the_ceiling_is_not_truncated()
    {
        var text = ContextPack.Render(Facts([
            Container("shop-api-1", "running", "Up 4 minutes", service: "api", port: 8080),
            Container("shop-db-1", "running", "Up 4 minutes", service: "db", port: 5432),
        ]));

        Assert.DoesNotContain("truncated", text, StringComparison.Ordinal);
        Assert.True(
            TokenEstimate.Of(text) <= ContextPack.CeilingTokens,
            $"{TokenEstimate.Of(text)} tokens against a ceiling of {ContextPack.CeilingTokens}");
    }

    [Fact]
    public void A_machine_over_the_ceiling_says_how_many_rows_went()
    {
        // A payload that quietly drops a row is worse than one that refuses, so the count is stated.
        var many = Enumerable.Range(0, 60)
            .Select(i => Container($"shop-service-{i:D2}-1", "running", "Up 4 minutes",
                service: $"service-{i:D2}", port: 9000 + i))
            .ToList();

        var text = ContextPack.Render(Facts(many));

        Assert.Contains("truncated", text, StringComparison.Ordinal);
        Assert.True(
            TokenEstimate.Of(text) <= ContextPack.CeilingTokens,
            $"{TokenEstimate.Of(text)} tokens against a ceiling of {ContextPack.CeilingTokens}");
        // The engine line, the disk line and the cursor survive truncation: they are what the caller
        // needs to know it was truncated and to ask again.
        Assert.StartsWith("engine  ", text, StringComparison.Ordinal);
        Assert.Contains("disk    ", text, StringComparison.Ordinal);
        Assert.Contains("cursor  c:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Truncation_does_not_move_the_cursor()
    {
        // The cursor is over the state, not the text. A cursor that moved because a ceiling was reached
        // would report a change to the machine that did not happen, which is exactly what DD31's delta
        // would then act on.
        var many = Enumerable.Range(0, 60)
            .Select(i => Container($"shop-service-{i:D2}-1", "running", "Up 4 minutes",
                service: $"service-{i:D2}"))
            .ToList();

        var truncated = ContextPack.Render(Facts(many));
        var whole = ContextPack.Render(Facts(many.Take(2).ToList()));

        Assert.Contains("truncated", truncated, StringComparison.Ordinal);
        Assert.NotEqual(Cursor(whole), Cursor(truncated));

        // And the same machine truncated twice gives the same cursor both times.
        Assert.Equal(Cursor(truncated), Cursor(ContextPack.Render(Facts(many))));
    }

    [Fact]
    public void The_ceiling_in_code_is_the_ceiling_recorded_in_the_budget()
    {
        // The shipped executable carries no budget file, so the number lives in code - and this is what
        // stops the two drifting. Raising one without the other fails here.
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        string? found = null;
        while (here is not null && found is null)
        {
            var candidate = System.IO.Path.Combine(here.FullName, "agent-budget.json");
            found = File.Exists(candidate) ? candidate : null;
            here = here.Parent;
        }

        Assert.NotNull(found);
        using var budget = JsonDocument.Parse(File.ReadAllBytes(found));
        var recorded = budget.RootElement
            .GetProperty("surface").GetProperty("shapes").GetProperty("read context").GetInt32();

        Assert.Equal(ContextPack.CeilingTokens, recorded);
    }

    [Fact]
    public void The_pack_replaces_the_five_calls_it_was_measured_against()
    {
        // DD23 measured three list reads at 5718 estimated tokens. One pack of a comparable machine is
        // the number that argument has to beat, and this is where it stops being an argument.
        var six = Enumerable.Range(0, 6)
            .Select(i => Container($"shop-svc{i}-1", i == 0 ? "exited" : "running",
                i == 0 ? "Exited (137) 12 seconds ago" : "Up 4 minutes",
                service: $"svc{i}", port: 8000 + i))
            .ToList();

        var pack = TokenEstimate.Of(ContextPack.Render(Facts(six, volumes: 2)));

        Assert.True(pack < 5718 / 10, $"the pack is {pack} tokens against 5718 for three list reads");
    }

    // ---- the form a parser asks for -------------------------------------------------------------

    [Fact]
    public void The_json_form_carries_the_same_facts_and_the_same_cursor()
    {
        var api = Container("shop-api-1", "exited", "Exited (137) 12 seconds ago", service: "api", port: 8080);
        var facts = Facts(
            [api],
            new Dictionary<string, ContainerInspect>(StringComparer.Ordinal)
            {
                [api.Id] = new()
                {
                    Id = api.Id,
                    RestartCount = 3,
                    State = new ContainerState { Status = "exited", ExitCode = 137, OomKilled = true },
                    HostConfig = new ContainerHostConfig { Memory = 512L * 1024 * 1024 },
                },
            },
            volumes: 2);

        using var json = JsonDocument.Parse(ContextPack.RenderJson(facts));
        var root = json.RootElement;

        // The same cursor as the line form: it is a fingerprint of the machine, and the machine does
        // not change because a caller asked for a different rendering of it.
        Assert.Equal(
            Cursor(ContextPack.Render(facts)).Replace("cursor  ", "", StringComparison.Ordinal),
            root.GetProperty("cursor").GetString());

        var container = root.GetProperty("containers")[0];
        Assert.Equal("shop-api-1", container.GetProperty("name").GetString());
        Assert.Equal("svc:shop/api", container.GetProperty("address").GetString());
        Assert.True(container.GetProperty("oomKilled").GetBoolean());
        Assert.Equal(137, container.GetProperty("exitCode").GetInt32());
        Assert.Equal(3, container.GetProperty("restarts").GetInt32());
        Assert.Equal(512L * 1024 * 1024, container.GetProperty("memoryLimit").GetInt64());
    }

    [Fact]
    public void The_json_form_is_not_truncated_because_a_cut_document_is_wrong_rather_than_long()
    {
        // The ceiling protects a line format read by something paying per token. Truncating structured
        // output would hand a parser a document that is incorrect, which is worse than one that is big.
        var many = Enumerable.Range(0, 60)
            .Select(i => Container($"shop-service-{i:D2}-1", "running", "Up 4 minutes",
                service: $"service-{i:D2}"))
            .ToList();

        using var json = JsonDocument.Parse(ContextPack.RenderJson(Facts(many)));

        Assert.Equal(60, json.RootElement.GetProperty("containers").GetArrayLength());
        Assert.Contains("truncated", ContextPack.Render(Facts(many)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_running_container_carries_no_diagnosis_fields()
    {
        // Only what is not running is inspected, so a healthy row has nothing an inspect would add.
        using var json = JsonDocument.Parse(ContextPack.RenderJson(
            Facts([Container("shop-db-1", "running", "Up 4 minutes (healthy)", service: "db")])));

        var container = json.RootElement.GetProperty("containers")[0];
        Assert.Equal(JsonValueKind.Null, container.GetProperty("oomKilled").ValueKind);
        Assert.Equal(JsonValueKind.Null, container.GetProperty("memoryLimit").ValueKind);
    }

    [Fact]
    public void Nothing_renders_from_null()
    {
        Assert.Throws<ArgumentNullException>(() => ContextPack.Render(null!));
        Assert.Throws<ArgumentNullException>(() => ContextPack.RenderJson(null!));
    }

    private static string Cursor(string pack) =>
        pack.Split(Environment.NewLine).Single(l => l.StartsWith("cursor", StringComparison.Ordinal));
}
