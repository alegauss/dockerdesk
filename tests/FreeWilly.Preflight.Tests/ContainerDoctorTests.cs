using FreeWilly.Core.Agent;
using FreeWilly.Core.Api;
using FreeWilly.Core.Engine;
using FreeWilly.Core.Preflight;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The diagnostic join, and the rule that the verdict is the deliverable (DD26).
/// </summary>
/// <remarks>
/// A command that returns forty facts and no conclusion has moved the join rather than closed it, so
/// every test here asks what the row concluded rather than which fields it carried. The rows are the
/// preflight's own <see cref="PreflightCheck"/>, which is the point: the vocabulary was already paid
/// for.
/// </remarks>
public sealed class ContainerDoctorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The ceiling agent-budget.json records for this shape, read from the file itself.</summary>
    private static int DoctorCeiling
    {
        get
        {
            var here = new DirectoryInfo(AppContext.BaseDirectory);
            while (here is not null)
            {
                var candidate = System.IO.Path.Combine(here.FullName, "agent-budget.json");
                if (File.Exists(candidate))
                {
                    using var budget = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(candidate));
                    return budget.RootElement.GetProperty("surface").GetProperty("shapes")
                        .GetProperty("read doctor").GetInt32();
                }

                here = here.Parent;
            }

            throw new InvalidOperationException("agent-budget.json was not found");
        }
    }

    private static ContainerSummary Summary(string name = "shop-api-1", string state = "exited") => new()
    {
        Id = "aaaaaaaaaaaa0000",
        Names = [$"/{name}"],
        Image = "shop/api:latest",
        State = state,
        Status = state == "running" ? "Up 4 minutes" : "Exited (137) 12 seconds ago",
    };

    private static ContainerInspect Inspect(
        string status = "exited",
        int exitCode = 137,
        bool oom = false,
        long memory = 0,
        int restarts = 0,
        DateTimeOffset? startedAt = null,
        string? health = null,
        int failingStreak = 0,
        IReadOnlyDictionary<string, IReadOnlyList<PortPublish>?>? ports = null,
        IReadOnlyList<ContainerMount>? mounts = null) => new()
    {
        Id = "aaaaaaaaaaaa0000",
        Name = "/shop-api-1",
        RestartCount = restarts,
        State = new ContainerState
        {
            Status = status,
            ExitCode = exitCode,
            OomKilled = oom,
            StartedAt = (startedAt ?? Now.AddMinutes(-2)).ToString("O"),
            Health = health is null
                ? null
                : new ContainerHealth { Status = health, FailingStreak = failingStreak },
        },
        HostConfig = new ContainerHostConfig { Memory = memory, PortBindings = ports },
        Mounts = mounts ?? [],
    };

    private static DoctorFacts Facts(
        ContainerSummary? summary = null,
        ContainerInspect? inspect = null,
        IEnumerable<int>? listening = null,
        IReadOnlyList<string>? stderr = null,
        IReadOnlyDictionary<string, BindSource>? sources = null) => new(
            Address: Address.Parse("shop-api-1"),
            Summary: summary,
            Inspect: inspect,
            ListeningHostPorts: (listening ?? []).ToHashSet(),
            StandardError: stderr ?? [],
            Now: Now,
            BindSources: sources);

    private static IReadOnlyDictionary<string, BindSource> Asked(string source, BindSource answer) =>
        new Dictionary<string, BindSource>(StringComparer.Ordinal) { [source] = answer };

    private static PreflightCheck? Row(PreflightReport report, string id) => report[id];

    private static IReadOnlyDictionary<string, IReadOnlyList<PortPublish>?> Published(
        string containerPort, string hostPort) =>
        new Dictionary<string, IReadOnlyList<PortPublish>?>(StringComparer.Ordinal)
        {
            [containerPort] = [new PortPublish { HostIp = "0.0.0.0", HostPort = hostPort }],
        };

    // ---- the verdict is the deliverable ---------------------------------------------------------

    [Fact]
    public void Every_row_that_is_not_green_names_an_action()
    {
        // The whole rule: a row that reports a problem and no remedy has moved the join rather than
        // closing it. stderr is the one exception and it is content rather than a finding.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(oom: true, memory: 512L * 1024 * 1024, restarts: 4, ports: Published("8080/tcp", "8080")),
            stderr: ["java.net.BindException: Address already in use"]));

        foreach (var row in report.Checks.Where(c =>
            c.Verdict is not Verdict.Pass && c.Id != ContainerDoctor.Rows.StandardError))
        {
            Assert.False(
                string.IsNullOrWhiteSpace(row.Remedy),
                $"{row.Id} concluded {row.Verdict} and offers no action");
        }
    }

    [Fact]
    public void A_container_that_does_not_exist_is_one_row_and_a_remedy()
    {
        var report = ContainerDoctor.Diagnose(Facts());

        var row = Assert.Single(report.Checks);
        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.Contains("no such container", row.Detail, StringComparison.Ordinal);
        Assert.Contains("read context", row.Remedy!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_healthy_container_says_so_without_listing_configuration()
    {
        // Where there is no conclusion to draw, saying so is also a conclusion and it costs less than
        // the fields would have. A memory limit that has not been hit is configuration, not a finding.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(state: "running"),
            Inspect(status: "running", exitCode: 0, memory: 512L * 1024 * 1024,
                health: "healthy", ports: Published("8080/tcp", "8080")),
            listening: [8080]));

        Assert.True(report.CanHostEngine);
        Assert.Null(Row(report, ContainerDoctor.Rows.Memory));
        Assert.Null(Row(report, ContainerDoctor.Rows.Restarts));
        Assert.All(report.Checks, row => Assert.Null(row.Remedy));
    }

    // ---- the row that closes the canonical task -------------------------------------------------

    [Fact]
    public void An_OOM_kill_names_the_limit_it_was_killed_against()
    {
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(), Inspect(oom: true, memory: 512L * 1024 * 1024)));

        var row = Row(report, ContainerDoctor.Rows.Memory);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.Contains("512M", row.Detail, StringComparison.Ordinal);
        Assert.Contains("kernel killed it", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Exit_137_without_an_OOM_flag_says_something_else_sent_the_signal()
    {
        // 137 is SIGKILL and says nothing about who sent it. Reporting it as an OOM would be a
        // confident wrong answer, which is the failure mode a doctor has to avoid most.
        var report = ContainerDoctor.Diagnose(Facts(Summary(), Inspect(oom: false, exitCode: 137)));

        var row = Row(report, ContainerDoctor.Rows.Memory);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Warn, row.Verdict);
        Assert.Contains("SIGKILL", row.Detail, StringComparison.Ordinal);
        Assert.Contains("did not report an OOM", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_ordinary_exit_produces_no_memory_row_at_all() =>
        Assert.Null(Row(
            ContainerDoctor.Diagnose(Facts(Summary(), Inspect(exitCode: 0))),
            ContainerDoctor.Rows.Memory));

    // ---- restarts over a window -----------------------------------------------------------------

    [Fact]
    public void Three_restarts_inside_the_window_is_a_loop()
    {
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(), Inspect(restarts: 3, startedAt: Now.AddMinutes(-2))));

        var row = Row(report, ContainerDoctor.Rows.Restarts);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.Contains("×3 in 2m", row.Detail, StringComparison.Ordinal);
        Assert.Contains("loop", row.Remedy!, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_count_over_a_long_window_is_not()
    {
        // A count on its own is not a story: three restarts over a month is a service that was
        // redeployed three times.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(), Inspect(restarts: 3, startedAt: Now.AddDays(-30))));

        var row = Row(report, ContainerDoctor.Rows.Restarts);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Warn, row.Verdict);
        Assert.False(row.Blocks);
    }

    [Fact]
    public void No_restarts_is_no_row() =>
        Assert.Null(Row(
            ContainerDoctor.Diagnose(Facts(Summary(), Inspect(restarts: 0))),
            ContainerDoctor.Rows.Restarts));

    // ---- health ---------------------------------------------------------------------------------

    [Theory]
    [InlineData("healthy", Verdict.Pass)]
    [InlineData("unhealthy", Verdict.Fail)]
    [InlineData("starting", Verdict.Warn)]
    public void The_containers_own_healthcheck_decides_the_health_row(string status, Verdict expected)
    {
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(state: "running"), Inspect(status: "running", exitCode: 0, health: status)));

        Assert.Equal(expected, Row(report, ContainerDoctor.Rows.Health)!.Verdict);
    }

    [Fact]
    public void No_healthcheck_declared_is_not_a_finding() =>
        // A row that says "none" on every container never changes and always costs.
        Assert.Null(Row(
            ContainerDoctor.Diagnose(Facts(Summary(state: "running"), Inspect(status: "running"))),
            ContainerDoctor.Rows.Health));

    [Fact]
    public void A_failing_streak_is_carried_because_one_failure_and_nine_are_different()
    {
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(state: "running"),
            Inspect(status: "running", exitCode: 0, health: "unhealthy", failingStreak: 3)));

        Assert.Contains("3 failing in a row", Row(report, ContainerDoctor.Rows.Health)!.Detail,
            StringComparison.Ordinal);
    }

    // ---- the row Docker structurally cannot answer ----------------------------------------------

    [Fact]
    public void A_published_port_with_nothing_listening_is_the_finding()
    {
        // Half of this row is not in the daemon: the daemon knows what was published and only Windows
        // knows whether anything holds the socket. A binding with nothing behind it is the exact
        // confusion the row removes.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(), Inspect(ports: Published("8080/tcp", "8080")), listening: []));

        var row = Row(report, ContainerDoctor.Rows.Ports);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.Contains("nothing listening", row.Detail, StringComparison.Ordinal);
        Assert.Contains("8080", row.Remedy!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_published_port_something_is_listening_on_passes()
    {
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(state: "running"),
            Inspect(status: "running", exitCode: 0, ports: Published("8080/tcp", "8080")),
            listening: [8080]));

        var row = Row(report, ContainerDoctor.Rows.Ports);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Pass, row.Verdict);
        Assert.Contains("listening", row.Detail, StringComparison.Ordinal);
        Assert.Null(row.Remedy);
    }

    [Fact]
    public void The_row_says_listening_and_never_answering()
    {
        // DD30 owns whether the service replies, which needs a request rather than a socket table. A
        // stronger word here would make that task mean less.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(state: "running"),
            Inspect(status: "running", exitCode: 0, ports: Published("8080/tcp", "8080")),
            listening: [8080]));

        var detail = Row(report, ContainerDoctor.Rows.Ports)!.Detail;
        Assert.DoesNotContain("answering", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("reachable", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_container_that_publishes_nothing_has_no_port_row() =>
        Assert.Null(Row(
            ContainerDoctor.Diagnose(Facts(Summary(), Inspect())),
            ContainerDoctor.Rows.Ports));

    // ---- mounts, and what this tool may not judge -----------------------------------------------

    [Fact]
    public void A_bind_whose_windows_source_is_gone_is_the_finding()
    {
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts: [new ContainerMount
            {
                Type = "bind",
                Source = "/mnt/c/definitely/not/here/" + Guid.NewGuid().ToString("N"),
                Destination = "/app",
                ReadWrite = true,
            }])));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.Contains("MISSING", row.Detail, StringComparison.Ordinal);
        // The reason this matters: the container gets an empty directory rather than an error.
        Assert.Contains("empty directory", row.Remedy!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_bind_that_resolves_passes()
    {
        var here = Wsl.ToDistributionPath(AppContext.BaseDirectory);
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts: [new ContainerMount
            {
                Type = "bind", Source = here, Destination = "/app", ReadWrite = true,
            }])));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Pass, row.Verdict);
        Assert.DoesNotContain("MISSING", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_volume_and_another_engines_path_are_unchecked_rather_than_broken()
    {
        // A false "does not resolve" is worse than no answer: a volume lives inside the distribution
        // and Docker Desktop's own host mapping is not this tool's to judge.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts:
            [
                new ContainerMount { Type = "volume", Name = "shop_data", Destination = "/data" },
                new ContainerMount
                {
                    Type = "bind",
                    Source = "/run/desktop/mnt/host/c/Users/dev/shop",
                    Destination = "/app",
                },
            ])));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Unknown, row.Verdict);
        Assert.Contains("unchecked", row.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("MISSING", row.Detail, StringComparison.Ordinal);
    }

    // ---- the source only `do compose up` respells (DD96) -----------------------------------------

    [Fact]
    public void A_source_spelled_for_another_engine_is_told_how_it_would_be_spelled_here()
    {
        // The verdict stays Unknown and that is deliberate: the default pipe is one Docker Desktop
        // also serves, so a container carrying its host mapping may be a container of its own and
        // calling it broken would be the false diagnosis DD26 puts above everything. What can be
        // said without judging is the spelling, and that is what turns the row into an action.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts: [new ContainerMount
            {
                Type = "bind",
                Source = "/run/desktop/mnt/host/c/Users/dev/shop",
                Destination = "/app",
            }])));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Unknown, row.Verdict);
        Assert.Contains("/mnt/c/Users/dev/shop", row.Detail, StringComparison.Ordinal);
        Assert.False(row.Blocking, "a spelling is not a finding");
    }

    [Fact]
    public void A_windows_path_that_reached_the_daemon_is_shown_the_spelling_it_needed()
    {
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts: [new ContainerMount
            {
                Type = "bind", Source = @"D:\shop\data", Destination = "/data",
            }])));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.Contains("/mnt/d/shop/data", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreachable_bind_says_the_daemon_creates_it_rather_than_refusing()
    {
        // The whole of DD96 in one sentence. `do compose up` respells a Windows source into the
        // override it generates and nothing else does, so every other route — a prompt, an IDE,
        // the user's own compose — reaches the daemon as written. The daemon does not refuse it: it
        // creates the directory, and the container gets an empty one that reads as missing code.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts: [new ContainerMount
            {
                Type = "bind", Source = "/home/you/project", Destination = "/app",
            }])));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.NotNull(row.Remedy);
        Assert.Contains("created empty", row.Remedy, StringComparison.Ordinal);
        Assert.Contains("compose up", row.Remedy, StringComparison.Ordinal);
    }

    [Fact]
    public void A_volume_alone_is_not_told_about_bind_sources()
    {
        // The remedy is about binds, so a container with none must not carry it. Otherwise every
        // `read doctor` of a container using named volumes pays for advice that cannot apply.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts:
                [new ContainerMount { Type = "volume", Name = "shop_data", Destination = "/data" }])));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.Null(row.Remedy);
    }

    [Fact]
    public void A_bind_that_resolves_is_told_nothing_about_spellings()
    {
        // The remedy fires on unchecked binds only. A container whose sources all resolve has
        // nothing to fix, and a paragraph explaining a failure it does not have is the payload
        // bloat agent-budget.json exists to refuse.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts: [new ContainerMount
            {
                Type = "bind",
                Source = Wsl.ToDistributionPath(AppContext.BaseDirectory),
                Destination = "/app",
            }])));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Pass, row.Verdict);
        Assert.Null(row.Remedy);
    }

    // ---- the case only the distribution can settle (DD101) ---------------------------------------

    [Theory]
    [InlineData(BindSource.Missing, "not in this distribution")]
    [InlineData(BindSource.Empty, "empty in this distribution")]
    public void A_source_the_distribution_finds_nothing_behind_is_a_warning_and_not_a_failure(
        BindSource answer, string said)
    {
        // The whole restraint of DD101. `/home/you/project` typed in a WSL shell where $(pwd) is a
        // path this distribution does not have is the case DD96 named as the one that costs an
        // afternoon — and a source that is genuinely empty is somebody's output directory, so the
        // row states what it found and never which of the two it was.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts: [new ContainerMount
            {
                Type = "bind", Source = "/home/you/project", Destination = "/app",
            }]),
            sources: Asked("/home/you/project", answer)));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Warn, row.Verdict);
        Assert.False(row.Blocking, "an empty source may be somebody's output directory");
        Assert.Contains(said, row.Detail, StringComparison.Ordinal);
        Assert.Contains("cannot see", row.Remedy!, StringComparison.Ordinal);
        Assert.Contains("genuinely empty", row.Remedy!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_source_the_distribution_does_hold_stops_being_unchecked()
    {
        // The answer worth having most often: before this the row said "unchecked" about a source
        // that was there all along, so the one row a reader consults about mounts said nothing.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts: [new ContainerMount
            {
                Type = "bind", Source = "/srv/app", Destination = "/app",
            }]),
            sources: Asked("/srv/app", BindSource.Holds)));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Pass, row.Verdict);
        Assert.Contains("in this distribution", row.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("unchecked", row.Detail, StringComparison.Ordinal);
        Assert.Null(row.Remedy);
    }

    [Theory]
    [InlineData(BindSource.Unasked)]
    [InlineData(null)]
    public void A_question_nobody_managed_to_ask_reads_exactly_as_it_did_before(BindSource? answer)
    {
        // A stopped distribution, a missing wsl.exe and a command that timed out all land here, and
        // so does a caller with no distribution to reach at all. None of them is evidence about the
        // user's path, so none of them may move the row off Unknown (Verdict.Unknown's own rule).
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts: [new ContainerMount
            {
                Type = "bind", Source = "/home/you/project", Destination = "/app",
            }]),
            sources: answer is { } known ? Asked("/home/you/project", known) : null));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Unknown, row.Verdict);
        Assert.Contains("unchecked", row.Detail, StringComparison.Ordinal);
        Assert.Contains("created empty", row.Remedy!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_windows_source_still_outranks_a_source_the_distribution_doubts()
    {
        // Precedence, and it is not arbitrary: a mapped drive that is not there was read from
        // Windows and is certain, while the distribution's answer cannot separate "somewhere the
        // engine cannot see" from "empty". The certain finding is the one the row leads with.
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(),
            Inspect(mounts:
            [
                new ContainerMount
                {
                    Type = "bind",
                    Source = "/mnt/c/definitely/not/here/" + Guid.NewGuid().ToString("N"),
                    Destination = "/gone",
                },
                new ContainerMount
                {
                    Type = "bind", Source = "/home/you/project", Destination = "/app",
                },
            ]),
            sources: Asked("/home/you/project", BindSource.Missing)));

        var row = Row(report, ContainerDoctor.Rows.Mounts);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Fail, row.Verdict);
        Assert.True(row.Blocking);
    }

    [Fact]
    public void Only_the_sources_nothing_else_could_settle_are_worth_a_subprocess()
    {
        // The rule lives beside the row that uses it, because the caller gathers: a mapped drive is
        // answered from Windows, another engine's spelling is not ours to judge, and a volume lives
        // inside the distribution under a name rather than a path. One shell each for the rest.
        var sources = ContainerDoctor.SourcesOnlyTheDistributionCanSettle(Inspect(mounts:
        [
            new ContainerMount { Type = "volume", Name = "shop_data", Destination = "/data" },
            new ContainerMount
            {
                Type = "bind", Source = "/mnt/c/Users/dev/shop", Destination = "/win",
            },
            new ContainerMount
            {
                Type = "bind",
                Source = "/run/desktop/mnt/host/c/Users/dev/shop",
                Destination = "/rival",
            },
            new ContainerMount { Type = "bind", Source = @"D:\shop\data", Destination = "/drive" },
            new ContainerMount { Type = "bind", Source = "/home/you/project", Destination = "/app" },
            new ContainerMount { Type = "bind", Source = "/home/you/project", Destination = "/again" },
        ]));

        Assert.Equal(["/home/you/project"], sources);
    }

    [Fact]
    public void Nothing_is_asked_about_a_container_that_could_not_be_inspected() =>
        // The doctor still answers for a container the list knew and the inspect lost, and a
        // gathering loop over a null tree must be no loop rather than a null reference.
        Assert.Empty(ContainerDoctor.SourcesOnlyTheDistributionCanSettle(null));

    // ---- the reverse path mapping ---------------------------------------------------------------

    [Theory]
    [InlineData("/mnt/c/Users/dev/shop", @"C:\Users\dev\shop")]
    [InlineData("/mnt/d/Git", @"D:\Git")]
    [InlineData("/mnt/c/", @"C:\")]
    public void A_mapped_drive_maps_back(string distribution, string windows) =>
        Assert.Equal(windows, Wsl.ToWindowsPath(distribution));

    [Theory]
    [InlineData("/var/lib/docker/volumes/shop_data/_data")]
    [InlineData("/run/desktop/mnt/host/c/Users/dev/shop")]
    [InlineData("/mnt/certificates")]
    [InlineData("/mnt/")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_answers_null_rather_than_guessing(string? distribution) =>
        Assert.Null(Wsl.ToWindowsPath(distribution));

    [Fact]
    public void The_two_directions_agree()
    {
        var windows = @"D:\Git\alegauss\dockerdesk";
        Assert.Equal(windows, Wsl.ToWindowsPath(Wsl.ToDistributionPath(windows)));
    }

    // ---- the same folder, spelled three ways (DD96) ----------------------------------------------

    [Theory]
    [InlineData(@"D:\shop\data", "/mnt/d/shop/data")]
    [InlineData(@"C:\Users\dev\shop", "/mnt/c/Users/dev/shop")]
    [InlineData("D:/shop/data", "/mnt/d/shop/data")]
    [InlineData("/run/desktop/mnt/host/c/Users/dev/shop", "/mnt/c/Users/dev/shop")]
    [InlineData("/host_mnt/c/Users/dev/shop", "/mnt/c/Users/dev/shop")]
    public void A_windows_folder_named_another_way_is_given_this_engines_spelling(
        string source, string here) =>
        Assert.Equal(here, Wsl.WindowsFolderSpelledElsewhere(source));

    [Theory]
    [InlineData("/mnt/c/Users/dev/shop")]        // already this engine's spelling
    [InlineData("/var/lib/docker/volumes/shop_data/_data")]
    [InlineData("/home/you/project")]            // may be inside the distribution
    [InlineData("/host_mnt/certificates")]       // a directory, not drive C with a long name
    [InlineData("/run/desktop/mnt/host/")]
    [InlineData("shop_data")]
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_answers_nothing_rather_than_inventing_a_path(string? source) =>
        // The rule the whole diagnosis rests on: this states a spelling only where the source
        // certainly names a Windows folder. `/home/you/project` is the silent failure DD96 is
        // about and it is STILL not answered here — it could equally be a path inside the
        // distribution, and a confident wrong answer costs more than no answer (DD26).
        Assert.Null(Wsl.WindowsFolderSpelledElsewhere(source));

    // ---- stderr rather than the whole log -------------------------------------------------------

    [Fact]
    public void The_stderr_row_carries_what_was_given_and_is_not_a_finding()
    {
        var report = ContainerDoctor.Diagnose(Facts(
            Summary(), Inspect(), stderr: ["BindException: Address already in use", "at Server.start"]));

        var row = Row(report, ContainerDoctor.Rows.StandardError);
        Assert.NotNull(row);
        Assert.Equal(Verdict.Warn, row.Verdict);
        Assert.Contains("BindException", row.Detail, StringComparison.Ordinal);
        // Content, not a conclusion, so it does not stop anything.
        Assert.False(row.Blocks);
    }

    [Fact]
    public void No_stderr_is_no_row() =>
        Assert.Null(Row(
            ContainerDoctor.Diagnose(Facts(Summary(), Inspect())),
            ContainerDoctor.Rows.StandardError));

    // ---- the model it reuses --------------------------------------------------------------------

    [Fact]
    public void A_diagnosis_stays_under_the_ceiling_recorded_for_it()
    {
        // Found by reading a capture rather than by a test: the first rendering of a container with
        // every row failing came to 397 estimated tokens against the 260 recorded for `read doctor`,
        // because the remedies were explaining themselves. The ceiling is what the payload is for.
        //
        // Two worst cases and not one, since DD96: the mounts row has two remedies and they are
        // mutually exclusive, so a worst case carrying a missing bind never renders the other. The
        // longer of the two is what the ceiling has to hold.
        var missing = new ContainerMount { Type = "bind", Source = "/mnt/c/gone/api", Destination = "/app" };
        var elsewhere = new ContainerMount
        {
            Type = "bind",
            Source = "/run/desktop/mnt/host/c/Users/dev/shop/api",
            Destination = "/app",
        };

        foreach (var (mount, what) in new[] { (missing, "a missing bind"), (elsewhere, "an unreachable bind") })
        {
            var worst = ReportText.Render(
                ContainerDoctor.Diagnose(Facts(
                    Summary(),
                    Inspect(
                        oom: true, memory: 512L * 1024 * 1024, restarts: 3, startedAt: Now.AddMinutes(-2),
                        health: "unhealthy", failingStreak: 3, ports: Published("8080/tcp", "8080"),
                        mounts:
                        [
                            mount,
                            new ContainerMount { Type = "volume", Name = "shop_data", Destination = "/data" },
                        ]),
                    listening: [],
                    stderr:
                    [
                        "ERROR shop.api.Bootstrap - failed to bind :8080",
                        "java.net.BindException: Address already in use",
                    ])),
                heading: "freewilly read doctor shop-api-1",
                summary: "6 finding(s). The remedy on each row is the action.");

            Assert.True(
                TokenEstimate.Of(worst) <= DoctorCeiling,
                $"a diagnosis with every row failing and {what} is {TokenEstimate.Of(worst)} estimated "
                + $"tokens against the {DoctorCeiling} recorded in agent-budget.json. Tighten the "
                + "remedies or raise the ceiling and say in the commit what the tokens bought.");
        }
    }

    [Fact]
    public void The_rendering_says_what_it_is_about_rather_than_inheriting_the_machines_framing()
    {
        // Read off a real capture: pointing the preflight's renderer at a container inherited its
        // heading and its closing line, so a container diagnosis was titled "what this machine can
        // host" and ended "Nothing has been copied to disk" - a report describing the wrong thing.
        var report = ContainerDoctor.Diagnose(Facts(Summary(), Inspect()));

        var wrong = ReportText.Render(report);
        Assert.Contains("this machine can host", wrong, StringComparison.Ordinal);
        Assert.Contains("copied to disk", wrong, StringComparison.Ordinal);

        var right = ReportText.Render(
            report, heading: "freewilly read doctor shop-api-1", summary: "1 finding(s).");
        Assert.StartsWith("freewilly read doctor shop-api-1", right, StringComparison.Ordinal);
        Assert.DoesNotContain("this machine can host", right, StringComparison.Ordinal);
        Assert.DoesNotContain("copied to disk", right, StringComparison.Ordinal);
    }

    [Fact]
    public void The_report_renders_through_the_preflights_own_renderer()
    {
        // Not a new framework: the rows are PreflightCheck and the renderer is the one the preflight
        // already has, so a caller who has read one can read this.
        var text = ReportText.Render(ContainerDoctor.Diagnose(Facts(
            Summary(), Inspect(oom: true, memory: 512L * 1024 * 1024))));

        Assert.Contains("[FAIL]", text, StringComparison.Ordinal);
        Assert.Contains("memory", text, StringComparison.Ordinal);
        Assert.Contains("512M", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_finding_makes_the_report_refuse_so_an_exit_code_carries_it()
    {
        Assert.False(ContainerDoctor
            .Diagnose(Facts(Summary(), Inspect(oom: true, memory: 1024))).CanHostEngine);
        Assert.True(ContainerDoctor
            .Diagnose(Facts(Summary(state: "running"), Inspect(status: "running", exitCode: 0)))
            .CanHostEngine);
    }

    [Fact]
    public void Nothing_is_diagnosed_from_null() =>
        Assert.Throws<ArgumentNullException>(() => ContainerDoctor.Diagnose(null!));
}
