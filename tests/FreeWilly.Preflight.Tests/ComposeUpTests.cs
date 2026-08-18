using FreeWilly.Core.Agent;
using FreeWilly.Core.Api;
using FreeWilly.Core.Engine;
using FreeWilly.Tray.Cli;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>A compose CLI that answers from a script rather than running anything.</summary>
internal sealed class FakeComposeCli(params ComposeResult[] answers) : IComposeCli
{
    private int _next;

    internal List<string[]> Ran { get; } = [];

    internal string? WorkingDirectory { get; private set; }

    public ComposeResult Run(string workingDirectory, params string[] arguments)
    {
        WorkingDirectory = workingDirectory;
        Ran.Add(arguments);
        return _next < answers.Length
            ? answers[_next++]
            : new ComposeResult(0, "", null);
    }
}

/// <summary>An engine that answers a container list and nothing else.</summary>
/// <remarks>
/// Only <c>ContainersAsync</c> is reached: the verb reads back what now carries the label, which is
/// the proof the stamp landed. Everything else throwing is what makes "it asked for nothing more"
/// an assertion rather than a hope.
/// </remarks>
internal sealed class ListingEngine(params ContainerSummary[] containers) : IEngineReads
{
    private static InvalidOperationException Unexpected() =>
        new("the engine was asked for something this verb does not need");

    public Task<bool> PingAsync(CancellationToken cancellation = default) => throw Unexpected();

    public Task<EngineVersion> VersionAsync(CancellationToken cancellation = default) =>
        throw Unexpected();

    public Task<IReadOnlyList<ContainerSummary>> ContainersAsync(
        bool all = true, CancellationToken cancellation = default) =>
        Task.FromResult<IReadOnlyList<ContainerSummary>>(containers);

    public Task<ContainerInspect> InspectAsync(string id, CancellationToken cancellation = default) =>
        throw Unexpected();

    public Task<IReadOnlyList<ImageSummary>> ImagesAsync(CancellationToken cancellation = default) =>
        throw Unexpected();

    public Task<IReadOnlyList<VolumeSummary>> VolumesAsync(CancellationToken cancellation = default) =>
        throw Unexpected();

    public Task<IReadOnlyList<DockerEvent>> EventsAsync(
        DateTimeOffset since, DateTimeOffset until, CancellationToken cancellation = default) =>
        throw Unexpected();

    public Task<Stream> LogsAsync(
        string id,
        int tail = 2000,
        bool follow = true,
        bool timestamps = false,
        DateTimeOffset? since = null,
        CancellationToken cancellation = default) =>
        throw Unexpected();
}

/// <summary>
/// The first verb on this surface that creates, and the stamp that makes it undoable (DD63).
/// </summary>
public sealed class ComposeUpTests
{
    private static ComposeResult Ok(string output = "") => new(0, output, null);

    /// <summary>What `compose config --format json` answers for a one-service project.</summary>
    private static ComposeResult Configured(string service = "api") =>
        Ok("{\"name\":\"shop\",\"services\":{\"" + service + "\":{\"image\":\"nginx\"}}}");

    /// <summary>An engine holding one container already stamped for the session under test.</summary>
    private static ListingEngine Stamped() =>
        new(new ContainerSummary
        {
            Id = "aaaa",
            Names = ["/shop-api-1"],
            State = "running",
            Labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SessionLabel.Key] = "repro-17",
            },
        });

    // ---- the CLI that is actually run (DD73) ------------------------------------------------------

    [Fact]
    public void The_bundled_cli_points_docker_at_this_installs_own_config_directory()
    {
        // `compose` is a subcommand only where the CLI finds a plugin, and the CLI looks in
        // $DOCKER_CONFIG/cli-plugins. Driven through cmd.exe rather than through docker.exe, which
        // is not on this machine: what is being asserted is that the child inherits the variable,
        // and the child does not have to be docker to answer that.
        var scratch = Directory.CreateTempSubdirectory("freewilly-compose-env");
        try
        {
            var paths = new EnginePaths(scratch.FullName);
            var cmd = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            var cli = new BundledComposeCli(cmd, paths.ConfigDirectory);

            var result = cli.Run(
                scratch.FullName, "/c", $"echo %{BundledComposeCli.ConfigVariable}%");

            Assert.Null(result.Failure);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(paths.ConfigDirectory, result.Output, StringComparison.Ordinal);
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    [Fact]
    public void The_config_directory_is_this_installs_own_and_never_the_users()
    {
        // DD32's rule, held as an assertion: the user's %USERPROFILE%\.docker is where a rival
        // writes, and a context found there would bring the project up on somebody else's engine
        // with a session stamp whose reclaim then finds nothing (DD20).
        var paths = new EnginePaths(@"D:\somewhere\FreeWilly");

        Assert.Equal(paths.Root, paths.ConfigDirectory);
        Assert.Equal(Path.Combine(paths.Root, "cli-plugins"), paths.PluginsDirectory);
        Assert.DoesNotContain(".docker", paths.PluginsDirectory, StringComparison.Ordinal);
    }

    // ---- finding the project ----------------------------------------------------------------------

    [Fact]
    public void The_compose_file_is_found_in_the_order_compose_itself_prefers()
    {
        // Not this project's taste. A directory holding both is one where the CLI has already
        // decided, and picking differently here brings up a project the caller cannot see in the
        // file they are looking at.
        Assert.Equal(
            ["compose.yaml", "compose.yml", "docker-compose.yaml", "docker-compose.yml"],
            ComposeUp.FileNames);

        var both = ComposeUp.FileIn(
            @"C:\shop",
            path => path is @"C:\shop\compose.yaml" or @"C:\shop\docker-compose.yml");

        Assert.Equal(@"C:\shop\compose.yaml", both);
        Assert.Null(ComposeUp.FileIn(@"C:\shop", _ => false));
    }

    [Fact]
    public void The_override_compose_would_apply_is_part_of_the_project()
    {
        // DD143, and the defect is caused by the fix for something else. Compose reads a base file
        // and an optional override of its own accord, and it stops doing that the moment a caller
        // names files with -f — which this verb has to, because it injects one. So a two-file
        // project silently became a one-file project.
        //
        // Reproduced in an empty directory: `compose up -d` started base and extra; `do compose up`
        // started base and reported "1 service(s)". Nothing said a file had been skipped.
        var files = ComposeUp.ProjectFiles(
            @"C:\shop",
            path => path is @"C:\shop\docker-compose.yml" or @"C:\shop\docker-compose.override.yml");

        Assert.Equal(
            [@"C:\shop\docker-compose.yml", @"C:\shop\docker-compose.override.yml"],
            files);

        // Base first, override second: compose merges in order, and its project directory comes
        // from the first file named.
        Assert.Equal(
            ["compose", "-f", @"C:\shop\docker-compose.yml",
             "-f", @"C:\shop\docker-compose.override.yml",
             "-f", @"C:\temp\stamp.yml", "up", "-d"],
            ComposeUp.UpArguments(files, @"C:\temp\stamp.yml"));

        // And the read that decides what gets stamped is the same project that gets brought up. A
        // narrower read here would stamp some of what it created and not the rest.
        Assert.Equal(
            ["compose", "-f", @"C:\shop\docker-compose.yml",
             "-f", @"C:\shop\docker-compose.override.yml",
             "config", "--format", "json"],
            ComposeUp.ConfigArguments(files));
    }

    [Fact]
    public void The_override_is_found_the_way_compose_finds_it_and_not_the_obvious_way()
    {
        // Measured against the real CLI, because the obvious derivation is wrong in two ways and
        // this test is the record of both.
        //
        // First: an override does NOT belong to the file it overrides. A directory holding
        // docker-compose.yml beside compose.override.yaml gets both applied — measured, `config
        // --services` answered base and extra — so deriving the name from the base would drop it.
        Assert.Equal(
            @"C:\shop\compose.override.yaml",
            ComposeUp.OverrideIn(
                @"C:\shop",
                path => path is @"C:\shop\docker-compose.yml" or @"C:\shop\compose.override.yaml"));

        // Second: the preference is not FileNames'. That list prefers .yaml; this one prefers .yml.
        // With all four present compose warns "Found multiple override files with supported names"
        // and then "Using compose.override.yml", which is where this order comes from.
        Assert.Equal(
            ["compose.override.yml", "compose.override.yaml",
             "docker-compose.override.yml", "docker-compose.override.yaml"],
            ComposeUp.OverrideFileNames);

        Assert.Equal(
            @"C:\shop\compose.override.yml",
            ComposeUp.OverrideIn(@"C:\shop", _ => true));

        Assert.Null(ComposeUp.OverrideIn(@"C:\shop", _ => false));

        // A one-file project stays one file.
        Assert.Equal(
            [@"C:\shop\compose.yaml"],
            ComposeUp.ProjectFiles(@"C:\shop", path => path is @"C:\shop\compose.yaml"));
    }

    /// <summary>A service with the binds named, which is all these tests need of one.</summary>
    private static ComposeUp.ComposeService Service(
        string name, params ComposeUp.ComposeBind[] binds) => new(name, binds);

    // ---- the stamp ---------------------------------------------------------------------------------

    [Fact]
    public void The_override_stamps_every_service_with_the_session_label()
    {
        var yaml = ComposeUp.Override([Service("api"), Service("db")], "repro-17");

        Assert.Contains("services:", yaml, StringComparison.Ordinal);
        Assert.Contains("  api:", yaml, StringComparison.Ordinal);
        Assert.Contains("  db:", yaml, StringComparison.Ordinal);
        Assert.Equal(2, yaml.Split("    labels:").Length - 1);
        Assert.Equal(2, yaml.Split($"      {SessionLabel.Key}: \"repro-17\"").Length - 1);
    }

    [Fact]
    public void Every_label_the_session_carries_reaches_the_file_and_not_just_the_one_named_here()
    {
        // The guard DD79 exists for. Before it, this file spelled `SessionLabel.Key` into the YAML
        // by hand, so `For` was the documented write point with no caller and a second label added
        // there would have stamped nothing. Driven off `For` rather than off a literal: a test that
        // named the key would pass just as happily against a writer that had gone back to naming it.
        var yaml = ComposeUp.Override([Service("api"), Service("db")], "repro-17");

        var labels = SessionLabel.For("repro-17");
        Assert.NotEmpty(labels);
        foreach (var label in labels)
        {
            Assert.Equal(
                2,
                yaml.Split($"      {label.Key}: \"{label.Value}\"").Length - 1);
        }
    }

    [Fact]
    public void The_same_session_generates_the_same_bytes()
    {
        // A dictionary promises no order, and this is a file somebody diffs. Cheap to assert and
        // the only thing standing between a second label and an override that reshuffles per run.
        Assert.Equal(
            ComposeUp.Override([Service("api"), Service("db")], "repro-17"),
            ComposeUp.Override([Service("api"), Service("db")], "repro-17"),
            StringComparer.Ordinal);
    }

    [Fact]
    public void A_derived_session_is_quoted_because_a_colon_is_a_mapping_in_yaml()
    {
        // `dir:8f21a0` unquoted parses as a nested key, so the label a reclaim looks for would
        // silently not be there — a create that forgot the stamp, which is the exact symptom DD29
        // exists to remove.
        var derived = SessionLabel.Resolve(null, @"D:\shop");
        Assert.StartsWith("dir:", derived, StringComparison.Ordinal);

        Assert.Contains(
            $"{SessionLabel.Key}: \"{derived}\"",
            ComposeUp.Override([Service("api")], derived),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_user_s_file_comes_first_so_it_stays_the_project()
    {
        // Compose takes its project directory from the FIRST -f, and every relative build context,
        // bind mount and env_file in the user's file resolves against it. Put the generated stamp
        // first and all of them resolve against TEMP.
        var arguments = ComposeUp.UpArguments([@"C:\shop\compose.yaml"], @"C:\temp\stamp.yml");

        Assert.Equal(
            ["compose", "-f", @"C:\shop\compose.yaml", "-f", @"C:\temp\stamp.yml", "up", "-d"],
            arguments);
    }

    [Fact]
    public void The_project_comes_from_the_CLI_rather_than_from_a_parser_here()
    {
        // The CLI is already the authority on what the merged project contains, and a YAML parser
        // of our own would be a second opinion about somebody's file. JSON rather than the older
        // `config --services`, because DD75 needs the resolved bind sources from the same read.
        Assert.Equal(
            ["compose", "-f", @"C:\shop\compose.yaml", "config", "--format", "json"],
            ComposeUp.ConfigArguments([@"C:\shop\compose.yaml"]));

        // The shape compose emits, taken from a real `config --format json` run.
        const string Json = """
            {"name":"shop","services":{
              "api":{"image":"shop/api","volumes":[
                {"type":"bind","source":"D:\\shop\\data","target":"/data","bind":{}},
                {"type":"volume","source":"pgdata","target":"/var/lib/postgresql/data"}]},
              "db":{"image":"postgres:16"}}}
            """;

        var project = ComposeUp.Project(Json);
        Assert.Equal(["api", "db"], project.Select(s => s.Name));

        // The named volume is not a bind and is left out: translating one would turn a managed
        // volume into a path.
        var bind = Assert.Single(project[0].Binds);
        Assert.Equal(@"D:\shop\data", bind.Source);
        Assert.Equal("/data", bind.Target);
        Assert.False(bind.ReadOnly);
        Assert.Empty(project[1].Binds);

        Assert.Empty(ComposeUp.Project(""));
        Assert.Empty(ComposeUp.Project(null));
        Assert.Throws<FormatException>(() => ComposeUp.Project("not json at all"));
    }

    // ---- the bind sources a Linux daemon could not resolve (DD75) ----------------------------------

    [Fact]
    public void A_windows_bind_source_is_respelled_the_distribution_s_way()
    {
        // Measured against an upstream daemon: `D:\shop\data:/data` is refused with
        // `invalid mode: /data`, because the daemon splits the spec on `:` and the drive letter's
        // colon lands in the middle of it. `/mnt/d/shop/data` resolves, because WSL mounts the
        // drives and this install writes no [automount] section turning that off.
        var yaml = ComposeUp.Override(
            [Service("api", new ComposeUp.ComposeBind(@"D:\shop\data", "/data", ReadOnly: false))],
            "repro-17");

        Assert.Contains("    volumes:", yaml, StringComparison.Ordinal);
        Assert.Contains("      - \"/mnt/d/shop/data:/data\"", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_read_only_bind_keeps_its_mode()
    {
        var yaml = ComposeUp.Override(
            [Service("api", new ComposeUp.ComposeBind(@"C:\src", "/src", ReadOnly: true))],
            "repro-17");

        Assert.Contains("      - \"/mnt/c/src:/src:ro\"", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_source_the_daemon_can_already_resolve_is_left_alone()
    {
        // Somebody who already spelled it the distribution's way, and a named volume: neither needs
        // an entry, and writing one would be this tool rewriting a mount that was correct.
        Assert.False(ComposeUp.NeedsTranslating("/mnt/d/shop/data"));
        Assert.False(ComposeUp.NeedsTranslating("pgdata"));
        Assert.True(ComposeUp.NeedsTranslating(@"D:\shop\data"));

        var yaml = ComposeUp.Override(
            [Service("api", new ComposeUp.ComposeBind("/mnt/d/shop/data", "/data", ReadOnly: false))],
            "repro-17");

        Assert.DoesNotContain("volumes:", yaml, StringComparison.Ordinal);
    }

    // ---- the verb ----------------------------------------------------------------------------------

    [Fact]
    public void A_directory_with_no_compose_file_is_refused_before_anything_runs()
    {
        var cli = new FakeComposeCli();
        var scratch = Directory.CreateTempSubdirectory("freewilly-compose-none");
        try
        {
            var output = new StringWriter();
            var code = AgentSurface.DoCompose(
                Stamped(), cli, ["up"], scratch.FullName, "repro-17", output);

            Assert.Equal(2, code);
            Assert.Empty(cli.Ran);
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    [Fact]
    public void Anything_other_than_up_is_refused_and_named()
    {
        var cli = new FakeComposeCli();
        var output = new StringWriter();

        Assert.Equal(2, AgentSurface.DoCompose(
            Stamped(), cli, [], @"C:\shop", "repro-17", output));
        Assert.Equal(2, AgentSurface.DoCompose(
            Stamped(), cli, ["down"], @"C:\shop", "repro-17", output));
        Assert.Equal(2, AgentSurface.DoCompose(
            Stamped(), cli, ["up", "--build"], @"C:\shop", "repro-17", output));

        // Nothing reached the CLI: a verb that creates refuses before it acts, not after.
        Assert.Empty(cli.Ran);
    }

    [Fact]
    public void An_up_lists_the_services_then_brings_them_up_with_the_stamp()
    {
        var scratch = Directory.CreateTempSubdirectory("freewilly-compose-up");
        try
        {
            var composeFile = Path.Combine(scratch.FullName, "compose.yaml");
            File.WriteAllText(composeFile, "services:\n  api:\n    image: nginx\n");

            var cli = new FakeComposeCli(Configured(), Ok());
            var output = new StringWriter();

            var code = AgentSurface.DoCompose(
                Stamped(), cli, ["up"], scratch.FullName, "repro-17", output);

            Assert.Equal(0, code);
            Assert.Equal(2, cli.Ran.Count);
            Assert.Equal("config", cli.Ran[0][3]);
            Assert.Equal("up", cli.Ran[1][5]);

            // Run where the caller is, because that is where the project is.
            Assert.Equal(scratch.FullName, cli.WorkingDirectory);

            // And the stamp was written outside the project: a generated file left in a working
            // directory is the file that gets committed by accident.
            var stamped = cli.Ran[1][4];
            Assert.DoesNotContain(scratch.FullName, stamped, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(SessionLabel.Key, File.ReadAllText(stamped), StringComparison.Ordinal);

            var said = output.ToString();
            Assert.Contains("compose  up", said, StringComparison.Ordinal);
            Assert.Contains("do reclaim --session repro-17", said, StringComparison.Ordinal);
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_failed_up_says_what_the_CLI_said_rather_than_a_summary_of_it()
    {
        var scratch = Directory.CreateTempSubdirectory("freewilly-compose-fail");
        try
        {
            File.WriteAllText(Path.Combine(scratch.FullName, "compose.yaml"), "services:\n  api:\n");

            var cli = new FakeComposeCli(
                Configured(),
                new ComposeResult(1, "Error response from daemon: port is already allocated", null));
            var output = new StringWriter();

            var code = AgentSurface.DoCompose(
                Stamped(), cli, ["up"], scratch.FullName, "repro-17", output);

            Assert.Equal(1, code);

            // A compose failure is about the caller's own file — a port taken, an image that will
            // not build — and this surface has nothing to add to it except where it happened.
            Assert.Contains("port is already allocated", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }
}
