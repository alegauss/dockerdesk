using FreeWilly.Core.Engine;
using Xunit;

namespace FreeWilly.Preflight.Tests;

/// <summary>
/// The host's account of itself, kept where it outlives the window nobody was reading (DD137).
/// </summary>
public sealed class EngineHostLogTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"freewilly-hostlog-{Guid.NewGuid():N}");

    private string Path_ => System.IO.Path.Combine(_root, "engine.log");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void The_log_sits_beside_the_report_the_install_already_leaves()
    {
        // Both are the same kind of file — written by something that has since closed, and opened by
        // somebody asking what happened — so they belong in one place a person can be told about.
        // The installer writes provision.log into this root and takes it back on uninstall.
        var paths = new EnginePaths(@"C:\somewhere\FreeWilly");

        Assert.Equal(@"C:\somewhere\FreeWilly\engine.log", paths.HostLog);
        Assert.Equal(paths.Root, System.IO.Path.GetDirectoryName(paths.HostLog));
    }

    [Fact]
    public void A_quiet_engine_leaves_no_file_at_all()
    {
        // The whole design rests on this. The supervisor wakes every two seconds, and a host that
        // wrote a line each time would produce a file that says nothing in a great many words —
        // so nothing in this file is written unless something happened, and a machine whose engine
        // has simply been up has no file to read.
        _ = new EngineHostLog(Path_);

        Assert.False(
            File.Exists(Path_),
            "constructing the log created a file, so an untroubled host now leaves evidence of "
            + "nothing having gone wrong");
    }

    [Fact]
    public void A_line_carries_a_clock_because_that_is_what_the_console_could_not_give_anybody()
    {
        // The stamp is the reason the file is worth more than the console was. DD134 had to be
        // argued from Hyper-V events and a sixty-second gap, and a gap is only visible if the lines
        // either side of it are placed in time.
        var log = new EngineHostLog(Path_);
        log.Say("stopped  the daemon is not answering");

        var line = Assert.Single(File.ReadAllLines(Path_));

        Assert.EndsWith("stopped  the daemon is not answering", line, StringComparison.Ordinal);
        Assert.True(
            DateTime.TryParse(
                line[..19],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _),
            $"the line does not open with a timestamp anything can read: {line}");
    }

    [Fact]
    public void What_is_kept_is_the_newest_of_it()
    {
        // A file that grows without bound is its own defect, and this one is written by a process
        // that can run for weeks. The tail rather than the head, because the reason anybody opens
        // this file is that something just happened.
        var log = new EngineHostLog(Path_, kept: 2048);
        for (var i = 0; i < 400; i++)
        {
            log.Say($"line {i} — {new string('x', 40)}");
        }

        var kept = File.ReadAllLines(Path_);

        Assert.True(
            new FileInfo(Path_).Length <= 2048,
            $"the log grew to {new FileInfo(Path_).Length} bytes past a cap of 2048");
        Assert.Contains("line 399", kept[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(kept, line => line.Contains("line 0 ", StringComparison.Ordinal));
    }

    [Fact]
    public void A_trimmed_file_opens_on_a_whole_line()
    {
        // Trimming on a byte boundary leaves a fragment of a stamp at the top, which reads as
        // corruption to whoever finds it — and this file is only ever found by somebody already
        // suspicious that something is wrong.
        var log = new EngineHostLog(Path_, kept: 1024);
        for (var i = 0; i < 200; i++)
        {
            log.Say($"restart {i} — brought the engine back");
        }

        var first = File.ReadAllLines(Path_)[0];

        Assert.True(
            DateTime.TryParse(
                first[..19],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _),
            $"the trimmed file opens mid-line: {first}");
    }

    [Fact]
    public void A_log_that_cannot_be_written_does_not_take_the_engine_down_with_it()
    {
        // The one judgement in this class worth arguing about, so it is asserted rather than left in
        // a comment. This host's job is to keep a container engine up; a full disk, a file somebody
        // has open, or a root the user has locked down must not be what stops it. A log that cannot
        // be written is precisely the failure this class exists to record, and throwing here would
        // trade the silence for something worse.
        Directory.CreateDirectory(_root);

        // A directory where the file should be: every write to it fails, and nothing about that is
        // this process's to fix.
        Directory.CreateDirectory(Path_);

        var log = new EngineHostLog(Path_);

        log.Say("the engine stopped");
    }
}
