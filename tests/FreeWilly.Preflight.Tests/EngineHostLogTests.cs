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

    /// <summary>
    /// Two writers on one file lose nothing, which is what DD163 asks this class to survive.
    /// </summary>
    /// <remarks>
    /// The tray and the host both append now, and a dropped line here is the dangerous kind of
    /// wrong: the exception is swallowed, so the feature looks done, the file looks healthy, and
    /// the lines that mattered are the ones that went missing. Two logs over one path is exactly
    /// what two processes are, so this asserts on the count and never on the order.
    ///
    /// <para>What it proves is the pen — every <c>Say</c> is serialised, so the appends never
    /// actually overlap. The share mode that covers the case where they do is a separate question
    /// and has its own test below, because this one passes without it.</para>
    /// </remarks>
    [Fact]
    public async Task Two_writers_on_one_file_lose_no_lines_between_them()
    {
        var tray = new EngineHostLog(Path_);
        var host = new EngineHostLog(Path_);
        const int Each = 150;

        await Task.WhenAll(
            Task.Run(() =>
            {
                for (var i = 0; i < Each; i++)
                {
                    tray.Say($"tray  line {i}");
                }
            }),
            Task.Run(() =>
            {
                for (var i = 0; i < Each; i++)
                {
                    host.Say($"host  line {i}");
                }
            }));

        var written = File.ReadAllLines(Path_);

        Assert.Equal(Each, written.Count(l => l.Contains("tray  line", StringComparison.Ordinal)));
        Assert.Equal(Each, written.Count(l => l.Contains("host  line", StringComparison.Ordinal)));

        // Every line whole, which is the other half of an interleave going wrong: an append that
        // resolved its offset when the handle opened rather than when it wrote would leave two
        // lines on top of each other.
        Assert.All(written, line => Assert.True(
            DateTime.TryParse(
                line[..19],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _),
            $"a line was written over another: {line}"));
    }

    /// <summary>
    /// A line still lands while another process holds the file open to write (DD163).
    /// </summary>
    /// <remarks>
    /// The case the pen does not cover. A writer that could not take the pen within its wait writes
    /// anyway — deliberately, because a file briefly over its cap is a smaller failure than a line
    /// that was never written — and at that moment two handles are open for write at once. Opened
    /// the way this used to open, the second one is refused, the <see cref="IOException"/> is
    /// swallowed, and the line is gone with nothing anywhere saying so.
    ///
    /// <para>The held handle stands in for the other process, which is what a test in one process
    /// can honestly do: the sharing rules Windows applies are per-handle, not per-process, so a
    /// handle held here refuses exactly what a handle held in the tray would.</para>
    /// </remarks>
    [Fact]
    public void A_line_lands_while_another_handle_is_open_to_write()
    {
        Directory.CreateDirectory(_root);
        var log = new EngineHostLog(Path_);
        log.Say("the first line, so the file exists");

        // Somebody else, mid-append.
        using (var theirs = new FileStream(
            Path_, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            log.Say("the engine stopped answering");
        }

        Assert.Contains(
            File.ReadAllLines(Path_),
            line => line.EndsWith("the engine stopped answering", StringComparison.Ordinal));
    }

    /// <summary>The trim stays correct while a second writer is appending (DD163).</summary>
    /// <remarks>
    /// The trim is the one operation the pen exists for: it reads the whole file and writes back
    /// the tail, so an append landing between those two is lost to the write that follows. The cap
    /// is small enough here that trimming happens continuously throughout the run.
    /// </remarks>
    [Fact]
    public async Task A_trim_under_a_second_writer_leaves_a_file_of_whole_lines()
    {
        var tray = new EngineHostLog(Path_, kept: 4096);
        var host = new EngineHostLog(Path_, kept: 4096);

        await Task.WhenAll(
            Task.Run(() =>
            {
                for (var i = 0; i < 300; i++)
                {
                    tray.Say($"tray  {i} — {new string('t', 60)}");
                }
            }),
            Task.Run(() =>
            {
                for (var i = 0; i < 300; i++)
                {
                    host.Say($"host  {i} — {new string('h', 60)}");
                }
            }));

        Assert.True(
            new FileInfo(Path_).Length <= 4096 + 256,
            $"the log grew to {new FileInfo(Path_).Length} bytes past a cap of 4096");

        Assert.All(File.ReadAllLines(Path_), line => Assert.True(
            DateTime.TryParse(
                line[..19],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _),
            $"a trim left a fragment behind: {line}"));
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
