using FreeWilly.Core.Preflight;
using Xunit;

namespace FreeWilly.Preflight.Tests;

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

    /// <summary>
    /// The remedy block of a rendered report: the arrow line and everything wrapped under it.
    /// </summary>
    /// <remarks>
    /// Found by the arrow rather than by the indent, because a remedy continuation and an evidence
    /// line sit in the same column on purpose. Evidence is rendered above the remedy within a check
    /// and the next check starts back at the left margin, so what follows the arrow at that column
    /// is the remedy and nothing else.
    /// </remarks>
    private static IReadOnlyList<string> RemedyBlock(string[] lines)
    {
        var arrow = Array.FindIndex(
            lines, line => line.Contains(ReportText.RemedyArrow, StringComparison.Ordinal));
        Assert.True(arrow >= 0, "the report carries no remedy, so there is nothing to measure");

        var block = new List<string> { lines[arrow] };
        for (var i = arrow + 1;
            i < lines.Length && lines[i].StartsWith(
                new string(' ', ReportText.ContinuationColumn), StringComparison.Ordinal);
            i++)
        {
            block.Add(lines[i]);
        }

        return block;
    }

    [Fact]
    public void A_long_remedy_wraps_rather_than_running_off_the_console()
    {
        // The path is the one this was measured against on a real machine, at 113 characters. The
        // fixture used to be `C:\Program Files\Docker\x.exe` at 58, and that shortness was the only
        // thing keeping this test from contradicting DD52 (DD68): the assertion was about every line
        // the renderer emits, so making the fixture realistic turned it red and argued for wrapping
        // the evidence — which is the defect DD52's first attempt was reverted over.
        const string Resolved =
            @"docker resolves to C:\Users\someone\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe";

        var text = ReportText.Render(PreflightInspection.Run(new FakeMachine
        {
            RivalEngines = [new RivalEngine("Docker Desktop", Resolved)],
        }));

        var lines = text.Split(Environment.NewLine);

        // One remedy, one arrow, however many lines it takes to say.
        Assert.Equal(
            1, lines.Count(line => line.Contains(ReportText.RemedyArrow, StringComparison.Ordinal)));

        var remedy = RemedyBlock(lines);
        Assert.True(remedy.Count > 1, "the remedy did not wrap, so this measures nothing");
        Assert.All(remedy, line => Assert.True(
            line.Length <= ReportText.RemedyLineLimit,
            $"remedy line is {line.Length} characters against a limit of "
            + $"{ReportText.RemedyLineLimit}: {line}"));
    }

    [Fact]
    public void The_length_rule_is_the_remedys_alone_and_an_evidence_line_may_break_it()
    {
        // The other half of the same decision, stated out loud rather than left as an exception
        // somebody rediscovers (DD68). A guard that asked every line to be short could be satisfied
        // by wrapping a path, so this is what makes that repair fail instead of pass.
        const string Resolved =
            @"docker resolves to C:\Users\someone\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe";

        var lines = ReportText.Render(PreflightInspection.Run(new FakeMachine
        {
            RivalEngines = [new RivalEngine("Docker Desktop", Resolved)],
        })).Split(Environment.NewLine);

        var evidence = Assert.Single(
            lines, line => line.EndsWith(Resolved, StringComparison.Ordinal));

        Assert.True(
            evidence.Length > ReportText.RemedyLineLimit,
            $"the evidence line is {evidence.Length} characters, which is inside the remedy's "
            + $"{ReportText.RemedyLineLimit} — so this fixture no longer proves that an evidence "
            + "line is allowed to be as long as the path it names. Lengthen the path.");
    }

    [Fact]
    public void A_path_in_the_evidence_is_never_broken_across_two_lines()
    {
        // DD52, and the reason the obvious fix was reverted the first time: wrapping on spaces is
        // right for the remedy and wrong here. It put `…\Programs\DockerDesktop\Docker` on one line
        // and `Desktop.exe)` on the next, and a path split at a space is one nobody can copy into a
        // shell or grep for. One item per line instead, however long the line ends up.
        const string Resolved =
            @"docker resolves to C:\Users\someone\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe";
        const string Installer =
            @"C:\Users\someone\AppData\Local\Programs\DockerDesktop\Docker Desktop.exe";

        var text = ReportText.Render(PreflightInspection.Run(new FakeMachine
        {
            RivalEngines = [new RivalEngine("Docker Desktop", Resolved, Installer)],
        }));

        var lines = text.Split(Environment.NewLine);

        Assert.Contains(lines, line => line.EndsWith(Resolved, StringComparison.Ordinal));
        Assert.Contains(lines, line => line.EndsWith(Installer, StringComparison.Ordinal));

        // And the row itself stays short: it was 254 characters as one joined line, which is what
        // sent anyone reading it to a wrap in the first place.
        var row = lines.Single(line => line.Contains("Container engine", StringComparison.Ordinal));
        Assert.True(row.Length <= 100, $"the rival row is {row.Length} characters: {row}");
    }

    [Fact]
    public void Render_refuses_a_null_report() =>
        Assert.Throws<ArgumentNullException>(() => ReportText.Render(null!));
}
