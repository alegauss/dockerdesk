using DockerDesk.Core.Preflight;
using DockerDesk.Core.Preflight.Windows;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// Where the user's own <c>docker</c> points, and whether the report says so (DD20).
/// </summary>
/// <remarks>
/// Measured on the development machine: <c>currentContext</c> was <c>desktop-linux</c>, the store
/// held <c>desktop-linux</c> → <c>npipe:////./pipe/dockerDesktopLinuxEngine</c> and
/// <c>desktop-windows</c>, and no <c>default</c> at all — the CLI synthesises that one. So
/// <c>docker version</c> reported the daemon absent while the engine was answering on
/// <c>docker_engine</c>.
///
/// Nothing in this file writes anything, which is the answer DD20 picked: the row says where the CLI
/// points and leaves a per-user setting to the person who owns it.
/// </remarks>
public sealed class DockerContextTests
{
    private const string OurPipe = "npipe:////./pipe/docker_engine";
    private const string TheirPipe = "npipe:////./pipe/dockerDesktopLinuxEngine";

    /// <summary>The store as it actually was on the machine this defect was measured on.</summary>
    private static readonly (string Name, string? Host)[] MeasuredStore =
    [
        ("desktop-linux", TheirPipe),
        ("desktop-windows", "npipe:////./pipe/dockerDesktopWindowsEngine"),
    ];

    // ---- resolving where the CLI points -------------------------------------------------------

    [Fact]
    public void The_measured_machine_resolves_to_the_rivals_pipe()
    {
        var target = DockerContextProbe.Resolve(null, "desktop-linux", MeasuredStore);

        Assert.Equal("desktop-linux", target.ContextName);
        Assert.Equal(TheirPipe, target.Host);
        Assert.False(target.FromEnvironment);
        Assert.Null(target.Unreadable);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("default")]
    public void No_context_and_the_default_context_both_mean_this_engines_pipe(string? current)
    {
        // `default` is never in the store — verified on the measured machine, whose store held two
        // entries and no default. Synthesising it is what the CLI itself does.
        var target = DockerContextProbe.Resolve(null, current, MeasuredStore);

        Assert.Equal("default", target.ContextName);
        Assert.True(DockerContextProbe.ReachesThisEngine(target.Host));
    }

    [Fact]
    public void DOCKER_HOST_outranks_the_active_context()
    {
        // The CLI's own precedence, and it decides the remedy: telling somebody to switch context
        // while this is set would be advice that changes nothing.
        var target = DockerContextProbe.Resolve("tcp://10.0.0.5:2375", "default", MeasuredStore);

        Assert.True(target.FromEnvironment);
        Assert.Equal("tcp://10.0.0.5:2375", target.Host);
        Assert.Null(target.ContextName);
    }

    [Fact]
    public void An_active_context_that_is_not_in_the_store_is_said_to_be_missing()
    {
        // A real leftover state: a rival's uninstall can take the store entry and leave the setting,
        // and the CLI then fails outright rather than going somewhere else.
        var target = DockerContextProbe.Resolve(null, "desktop-linux", []);

        Assert.Equal("desktop-linux", target.ContextName);
        Assert.Null(target.Host);
        Assert.NotNull(target.Unreadable);
        Assert.Contains("desktop-linux", target.Unreadable, StringComparison.Ordinal);
    }

    [Fact]
    public void A_context_name_is_matched_exactly_and_not_loosely() =>
        Assert.Null(DockerContextProbe
            .Resolve(null, "desktop", MeasuredStore).Host);

    [Fact]
    public void A_null_store_is_a_defect_here_rather_than_an_empty_one() =>
        Assert.Throws<ArgumentNullException>(() =>
            DockerContextProbe.Resolve(null, "default", null!));

    // ---- comparing endpoints ------------------------------------------------------------------

    [Theory]
    [InlineData("npipe:////./pipe/docker_engine")]
    [InlineData("npipe://./pipe/docker_engine")]
    [InlineData("NPIPE:////./PIPE/DOCKER_ENGINE")]
    [InlineData("npipe:////./pipe/docker_engine/")]
    public void The_same_pipe_spelled_differently_is_the_same_pipe(string host) =>
        // Both spellings reach it, so comparing endpoint strings would call a working setup broken.
        Assert.True(DockerContextProbe.ReachesThisEngine(host));

    [Theory]
    [InlineData("npipe:////./pipe/dockerDesktopLinuxEngine")]
    [InlineData("tcp://127.0.0.1:2375")]
    [InlineData("unix:///var/run/docker.sock")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_does_not_reach_this_engine(string? host) =>
        Assert.False(DockerContextProbe.ReachesThisEngine(host));

    [Fact]
    public void A_pipe_name_is_only_read_out_of_an_npipe_endpoint()
    {
        Assert.Equal("docker_engine", DockerContextProbe.PipeName(OurPipe));
        Assert.Null(DockerContextProbe.PipeName("tcp://host/pipe/docker_engine"));
    }

    // ---- what the row says --------------------------------------------------------------------

    [Fact]
    public void The_row_names_both_pipes_so_the_two_can_be_compared()
    {
        var row = ContextRow("desktop-linux", TheirPipe);

        Assert.Equal(Verdict.Warn, row.Verdict);
        Assert.Contains("desktop-linux", row.Detail, StringComparison.Ordinal);
        Assert.Contains("dockerDesktopLinuxEngine", row.Detail, StringComparison.Ordinal);
        Assert.Contains(@"\\.\pipe\docker_engine", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_row_never_blocks_an_install()
    {
        // A leftover context does not stop the engine working; it stops the CLI finding it. Stopping
        // an install for that would be refusing to install over a setting this tool will not change.
        var report = PreflightInspection.Run(new FakeMachine
        {
            DockerClient = new DockerClientTarget
            {
                ContextName = "desktop-linux",
                Host = TheirPipe,
            },
        });

        Assert.True(report.CanHostEngine);
        Assert.Empty(report.Blockers);
        Assert.Equal(Verdict.Warn, report[PreflightInspection.Rows.DockerContext]!.Verdict);
    }

    [Fact]
    public void The_remedy_is_a_command_the_user_runs_and_not_something_done_for_them()
    {
        var row = ContextRow("desktop-linux", TheirPipe);

        Assert.NotNull(row.Remedy);
        Assert.Contains("docker context use default", row.Remedy, StringComparison.Ordinal);
        Assert.Contains("Nothing here changes it for you", row.Remedy, StringComparison.Ordinal);
    }

    [Fact]
    public void A_machine_whose_docker_already_reaches_this_engine_is_green()
    {
        var row = ContextRow("default", OurPipe);

        Assert.Equal(Verdict.Pass, row.Verdict);
        Assert.Null(row.Remedy);
    }

    [Fact]
    public void When_DOCKER_HOST_decided_the_remedy_names_the_variable_and_not_the_context()
    {
        var row = PreflightInspection.Run(new FakeMachine
        {
            DockerClient = new DockerClientTarget
            {
                Host = "tcp://10.0.0.5:2375",
                FromEnvironment = true,
            },
        })[PreflightInspection.Rows.DockerContext]!;

        Assert.Equal(Verdict.Warn, row.Verdict);
        Assert.Contains("DOCKER_HOST", row.Detail, StringComparison.Ordinal);
        Assert.Contains("DOCKER_HOST", row.Remedy!, StringComparison.Ordinal);
        // `docker context use` cannot win against the variable, so it must not be what is offered.
        Assert.DoesNotContain("context use", row.Remedy!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_DOCKER_HOST_that_points_at_this_engine_is_still_green()
    {
        var row = PreflightInspection.Run(new FakeMachine
        {
            DockerClient = new DockerClientTarget { Host = OurPipe, FromEnvironment = true },
        })[PreflightInspection.Rows.DockerContext]!;

        Assert.Equal(Verdict.Pass, row.Verdict);
        Assert.Contains("DOCKER_HOST", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_store_entry_warns_and_says_which_context_is_dangling()
    {
        var row = PreflightInspection.Run(new FakeMachine
        {
            DockerClient = DockerContextProbe.Resolve(null, "desktop-linux", []),
        })[PreflightInspection.Rows.DockerContext]!;

        Assert.Equal(Verdict.Warn, row.Verdict);
        Assert.Contains("desktop-linux", row.Detail, StringComparison.Ordinal);
        Assert.NotNull(row.Remedy);
    }

    // ---- what this machine actually says ------------------------------------------------------

    [Fact]
    public void Reading_this_machine_answers_without_throwing()
    {
        // Not asserted on content: whoever runs the tests may have any context active, or none. What
        // is asserted is that the read is safe and self-consistent, since it parses two JSON files
        // written by another tool.
        var target = DockerContextProbe.Read();

        Assert.NotNull(target);
        if (target.Host is null)
        {
            Assert.NotNull(target.Unreadable);
        }
    }

    private static PreflightCheck ContextRow(string? context, string? host) =>
        PreflightInspection.Run(new FakeMachine
        {
            DockerClient = new DockerClientTarget { ContextName = context, Host = host },
        })[PreflightInspection.Rows.DockerContext]!;
}
