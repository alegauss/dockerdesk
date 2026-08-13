using System.Net;
using FreeWilly.Core.Api;
using FreeWilly.Tray.Ui;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// Opening a shell is one process launch; everything worth testing is the decision in front of it.
/// Which shell the image has, which terminal gets it, and what happens when there is neither.
/// </summary>
public sealed class ContainerShellTests
{
    private static ContainerRow Row(string state, string name = "web") =>
        ContainerRow.From(new ContainerSummary
        {
            Id = "a1b2c3d4e5f60718293a",
            Names = [$"/{name}"],
            State = state,
        });

    /// <summary>A container in which only <paramref name="present"/> exists.</summary>
    private static Func<IReadOnlyList<string>, CancellationToken, Task<int>> Image(
        params string[] present) =>
        (command, _) => present.Contains(command[0])
            ? Task.FromResult(0)

            // What the daemon actually does for a binary that is not there: it refuses the start
            // rather than running something that exits non-zero.
            : Task.FromException<int>(new DockerApiException(
                $"the engine answered 500 for exec/start: stat {command[0]}: "
                + "no such file or directory",
                HttpStatusCode.InternalServerError));

    // ---- which shell -------------------------------------------------------------------------

    [Fact]
    public async Task Bash_is_taken_when_the_image_has_it()
    {
        // bash first, because it is what a person expects when they get a prompt.
        Assert.Equal("/bin/bash", await ContainerShell.FindAsync(Image("/bin/bash", "/bin/sh")));
    }

    [Fact]
    public async Task An_alpine_image_falls_back_to_sh()
    {
        Assert.Equal("/bin/sh", await ContainerShell.FindAsync(Image("/bin/sh")));
    }

    [Fact]
    public async Task A_distroless_image_has_none_and_that_is_an_answer_rather_than_a_crash()
    {
        // "the answer there is to say so rather than to open a window that immediately closes".
        Assert.Null(await ContainerShell.FindAsync(Image()));
    }

    [Fact]
    public async Task A_shell_that_exists_but_exits_non_zero_is_not_taken()
    {
        // A file at /bin/bash that is not a working shell — a broken symlink into a missing libc
        // is the real case. It runs and fails, and that is not a shell to open a window on.
        var image = (IReadOnlyList<string> command, CancellationToken _) =>
            Task.FromResult(command[0] == "/bin/bash" ? 126 : 0);

        Assert.Equal("/bin/sh", await ContainerShell.FindAsync(new(image)));
    }

    [Fact]
    public async Task The_probe_asks_the_shell_to_do_nothing_and_say_whether_it_could()
    {
        var asked = new List<IReadOnlyList<string>>();

        await ContainerShell.FindAsync((command, _) =>
        {
            asked.Add(command);
            return Task.FromResult(0);
        });

        Assert.Single(asked);
        Assert.Equal(["/bin/bash", "-c", "exit 0"], asked[0]);
    }

    [Fact]
    public void The_no_shell_message_names_the_container_and_both_shells_it_looked_for()
    {
        var message = ContainerShell.NoShellMessage("hopeful_wozniak");

        Assert.Contains("hopeful_wozniak", message, StringComparison.Ordinal);
        Assert.Contains("/bin/bash", message, StringComparison.Ordinal);
        Assert.Contains("/bin/sh", message, StringComparison.Ordinal);
    }

    // ---- what gets launched -------------------------------------------------------------------

    [Fact]
    public void The_launch_is_this_project_s_own_docker_cli_and_not_whatever_is_on_path()
    {
        // A machine with another Docker installed would otherwise get that client, pointed at
        // whichever engine it was configured for.
        var launch = ContainerShell.LaunchFor(
            @"C:\wt.exe", @"C:\Users\x\AppData\Local\FreeWilly\bin\docker.exe", "abc123", "/bin/sh");

        Assert.Equal(@"C:\wt.exe", launch.FileName);
        Assert.Equal(
            [@"C:\Users\x\AppData\Local\FreeWilly\bin\docker.exe", "exec", "-it", "abc123", "/bin/sh"],
            launch.Arguments);
    }

    [Fact]
    public void The_exec_is_interactive_because_a_shell_with_no_tty_is_not_a_shell()
    {
        var launch = ContainerShell.LaunchFor("wt.exe", "docker.exe", "abc123", "/bin/bash");

        Assert.Contains("-it", launch.Arguments);
    }

    [Fact]
    public void Windows_terminal_is_preferred_and_the_console_host_is_the_fallback()
    {
        Assert.Equal(Terminals.WindowsTerminal, Terminals.Choose(_ => true));
        Assert.Equal(Terminals.ConsoleHost, Terminals.Choose(_ => false));
    }

    [Fact]
    public void The_terminal_is_named_by_full_path_rather_than_left_to_path_resolution()
    {
        // wt.exe on PATH is an app-execution alias a user can switch off in Settings.
        Assert.Contains("WindowsApps", Terminals.WindowsTerminal, StringComparison.Ordinal);
        Assert.EndsWith("wt.exe", Terminals.WindowsTerminal, StringComparison.Ordinal);
        Assert.EndsWith("conhost.exe", Terminals.ConsoleHost, StringComparison.Ordinal);
    }

    // ---- when the button is off, and why ------------------------------------------------------

    [Fact]
    public void Only_a_running_container_offers_a_shell()
    {
        Assert.True(Row("running").CanShell);
        Assert.False(Row("exited").CanShell);
        Assert.False(Row("created").CanShell);
    }

    [Fact]
    public void A_paused_container_does_not_offer_one_even_though_it_is_live()
    {
        // docker exec against a paused container is accepted and then hangs until somebody
        // unpauses it, with no window and no error.
        var paused = Row("paused");

        Assert.True(paused.IsLive);
        Assert.False(paused.CanShell);
    }

    [Fact]
    public void A_row_with_an_action_in_flight_does_not_offer_one()
    {
        Assert.False((Row("running") with { Pending = "Stopping…" }).CanShell);
    }

    [Theory]
    [InlineData("running")]
    [InlineData("paused")]
    [InlineData("restarting")]
    [InlineData("exited")]
    public void Every_state_has_a_reason_on_the_tooltip(string state)
    {
        // A disabled control that says why is a limitation; one that does not is a bug the user is
        // left to guess at.
        var reason = Row(state).ShellReason;

        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.EndsWith(".", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void The_reason_a_paused_row_gives_is_not_the_reason_a_stopped_one_gives()
    {
        Assert.NotEqual(Row("paused").ShellReason, Row("exited").ShellReason);
        Assert.Contains("Unpause", Row("paused").ShellReason, StringComparison.Ordinal);
        Assert.Contains("Start it", Row("exited").ShellReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Opening_a_shell_is_a_pending_state_like_any_other_because_the_probe_takes_two_calls()
    {
        var activity = new RowActivity();
        var row = Row("running");

        activity.Began(row.Id, ContainerVerb.Shell);

        Assert.Equal("Opening…", activity.Dress(row).Pending);
    }
}
