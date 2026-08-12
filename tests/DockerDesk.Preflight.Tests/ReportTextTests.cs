using DockerDesk.Core.Preflight;
using Xunit;

namespace DockerDesk.Preflight.Tests;

/// <summary>
/// What the user actually reads. Rendered on a healthy machine only, the failing branch of this
/// code would ship unexecuted — which is where a null remedy crashes the one run that matters.
/// </summary>
public sealed class ReportTextTests
{
    [Fact]
    public void A_healthy_machine_renders_every_row_and_no_arrows()
    {
        var text = ReportText.Render(PreflightInspection.Run(FakeMachine.Healthy));

        Assert.Contains("Windows build", text, StringComparison.Ordinal);
        Assert.Contains("Hardware virtualization", text, StringComparison.Ordinal);
        Assert.Contains("WSL2", text, StringComparison.Ordinal);
        Assert.Contains("Container engine", text, StringComparison.Ordinal);
        Assert.Contains("[ok  ]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("->", text, StringComparison.Ordinal);
        Assert.Contains("can host a container engine", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failing_row_renders_its_remedy_under_it()
    {
        var text = ReportText.Render(PreflightInspection.Run(new FakeMachine
        {
            VirtualizationFirmwareEnabled = false,
        }));

        Assert.Contains("[FAIL]", text, StringComparison.Ordinal);
        Assert.Contains("->", text, StringComparison.Ordinal);
        Assert.Contains("UEFI", text, StringComparison.Ordinal);
        Assert.Contains("1 row blocks an install", text, StringComparison.Ordinal);
        Assert.Contains("Nothing has been copied to disk", text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_fact_renders_as_a_question_and_not_as_ok()
    {
        var text = ReportText.Render(PreflightInspection.Run(new FakeMachine
        {
            VirtualizationFirmwareEnabled = null,
        }));

        Assert.Contains("[?   ]", text, StringComparison.Ordinal);
        Assert.Contains("1 row blocks an install", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Several_blockers_are_counted_in_the_plural()
    {
        var report = PreflightInspection.Run(new FakeMachine
        {
            OperatingSystem = new Version(10, 0, 17763, 0),
            VirtualizationFirmwareEnabled = false,
        });

        Assert.Equal(
            "2 rows block an install. Nothing has been copied to disk.",
            ReportText.Summary(report));
    }

    [Fact]
    public void A_warning_does_not_change_the_summary()
    {
        var report = PreflightInspection.Run(new FakeMachine
        {
            Wsl = new WslInstallation
            {
                CommandPresent = true,
                KernelVersion = "6.6.87.2",
                DefaultVersion = 1,
            },
        });

        Assert.Equal("This machine can host a container engine.", ReportText.Summary(report));
        Assert.Contains("[warn]", ReportText.Render(report), StringComparison.Ordinal);
    }

    [Fact]
    public void A_long_remedy_wraps_rather_than_running_off_the_console()
    {
        var text = ReportText.Render(PreflightInspection.Run(new FakeMachine
        {
            RivalEngines = [new RivalEngine("Docker Desktop", @"C:\Program Files\Docker\x.exe")],
        }));

        var lines = text.Split(Environment.NewLine);

        // One remedy, one arrow, however many lines it takes to say.
        Assert.Equal(1, lines.Count(line => line.Contains("->", StringComparison.Ordinal)));
        Assert.Contains(lines, line =>
            line.StartsWith("              ", StringComparison.Ordinal)
            && line.TrimStart().StartsWith("leaves neither", StringComparison.Ordinal));
        Assert.All(lines, line => Assert.True(
            line.Length <= 100, $"line is {line.Length} characters: {line}"));
    }

    [Fact]
    public void Render_refuses_a_null_report() =>
        Assert.Throws<ArgumentNullException>(() => ReportText.Render(null!));
}
