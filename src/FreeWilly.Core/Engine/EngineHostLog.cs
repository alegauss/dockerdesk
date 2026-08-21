using System.Globalization;

namespace FreeWilly.Core.Engine;

/// <summary>
/// What this machine saw about the engine, written where it outlives the window nobody was
/// reading (DD137, DD163).
/// </summary>
/// <remarks>
/// The host is launched detached and hidden, which is right. The cost is that its whole account of
/// itself goes nowhere: when it stops, the line naming what it saw is written to a console that was
/// never readable and is gone by the time anybody asks.
///
/// <para>That was the expensive part of the failure DD134 repairs. The daemon's own log survives
/// inside the distribution and was decisive; the host's account of why it walked away was not
/// recoverable at all, so "the host decided the engine was dead" and "something killed the host"
/// had to be argued from Hyper-V events and a sixty-second gap rather than read.</para>
///
/// <para><b>What this is not.</b> Not a trace of every poll â€” the supervisor wakes every two
/// seconds, and a line each time is a file that says nothing in a great many words. A quiet engine
/// writes nothing at all, so anything in here is something that happened.</para>
///
/// <para><b>Two processes write it since DD163</b>, and that is what the pen below is for. The host
/// knows what the engine did and the tray knows what the user and Windows did, and those are the
/// two halves of every story worth reading here â€” the run of 21 August 2026 is the engine going
/// quiet, a gap, and a human clicking Start, and only the middle of that was ever written down.
/// Interleaving them into one file rather than keeping two is the whole value: a reader with two
/// files is a reader deciding which one to open, and correlating them by hand.</para>
/// </remarks>
public sealed class EngineHostLog
{
    /// <summary>
    /// How much of the file is kept.
    /// </summary>
    /// <remarks>
    /// A file that grows without bound is its own defect, and this one is written by a process that
    /// can run for weeks. 64 KB is a few thousand of these lines â€” far more than any single
    /// investigation reads, and small enough that nobody has to think about it.
    /// </remarks>
    public const int KeptBytes = 64 * 1024;

    /// <summary>
    /// The name every process appending to this file agrees on (DD163).
    /// </summary>
    /// <remarks>
    /// Unprefixed, so it is this session's, for the reason <c>SingleEngine</c> gives: a global name
    /// needs a privilege a standard user does not have, and the writers are a tray and a host under
    /// one login.
    ///
    /// <para>It guards the trim rather than the append. An append is one call against a handle
    /// opened for append only, which Windows serialises against the end of the file; the trim reads
    /// the whole file and writes back what it kept, and a second process appending in the middle of
    /// that loses its line to the write that follows.</para>
    /// </remarks>
    private const string PenName = "FreeWilly.enginelog";

    /// <summary>
    /// How long a writer waits for the pen before writing without it.
    /// </summary>
    /// <remarks>
    /// Long enough for the only thing the pen is ever held across, and short enough that a stuck
    /// holder cannot delay the host's own supervision. What happens on a timeout is the whole
    /// reason it can be this short: the line is still appended, and only the trim is skipped â€”
    /// a file that is briefly over its cap is a smaller failure than a line that was never written.
    /// </remarks>
    private static readonly TimeSpan PenWait = TimeSpan.FromMilliseconds(250);

    private readonly string _path;
    private readonly int _kept;

    /// <summary>Open the log at this path.</summary>
    /// <param name="path">Where to write. Its directory is created if it is missing.</param>
    /// <param name="kept">How much to keep; defaults to <see cref="KeptBytes"/>.</param>
    public EngineHostLog(string path, int kept = KeptBytes)
    {
        _path = path;
        _kept = kept;
    }

    /// <summary>The log the install's own root holds.</summary>
    /// <returns>The log.</returns>
    public static EngineHostLog BesideTheInstall() => new(new EnginePaths().HostLog);

    /// <summary>Write one line, stamped.</summary>
    /// <param name="line">What happened.</param>
    /// <remarks>
    /// Local time and not UTC, deliberately: the reader is correlating this against Event Viewer and
    /// against when they closed the lid, and both of those are local. The stamp is the whole reason
    /// the file is worth more than the console was â€” a sixty-second gap is only visible if the lines
    /// either side of it carry a clock.
    /// </remarks>
    public void Say(string line)
    {
        var stamped = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {line}");

        using var pen = Pen();
        var held = Hold(pen);
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Append(stamped + Environment.NewLine);

            // Only under the pen. Without it this reads the whole file and writes back the tail,
            // and an append from the other process landing between those two loses its line.
            if (held)
            {
                TrimIfItHasGrown();
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Swallowed, and this is the one place in this file worth arguing about. The host's job
            // is to keep a container engine up; a full disk, a file somebody has open in an editor,
            // or a root the user has locked down must not be what stops it. A log that cannot be
            // written is the failure this class exists to record, and taking the engine down to
            // announce it would be a worse outcome than the silence it replaces.
        }
        finally
        {
            if (held)
            {
                pen?.ReleaseMutex();
            }
        }
    }

    /// <summary>Append one already-stamped line, sharing the file with everyone (DD163).</summary>
    /// <param name="text">The line, with its terminator.</param>
    /// <remarks>
    /// <see cref="FileShare.ReadWrite"/> rather than <c>File.AppendAllText</c>'s own share mode,
    /// and both halves of it matter now. Write, because the tray and the host both append and
    /// the default refuses the second one â€” silently, since the exception is swallowed above, which
    /// would make this whole task look like it had been done. Read, because the window follows this
    /// file while it is being written to.
    ///
    /// <para><see cref="FileMode.Append"/> with write-only access is what makes the interleaving
    /// safe: Windows resolves the offset at the moment of the write rather than when the handle was
    /// opened, so two appenders cannot land on top of each other.</para>
    /// </remarks>
    private void Append(string text)
    {
        using var file = new FileStream(
            _path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(file);
        writer.Write(text);
    }

    /// <summary>The pen, or <see langword="null"/> where this machine would not give one out.</summary>
    /// <returns>The mutex.</returns>
    private static Mutex? Pen()
    {
        try
        {
            return new Mutex(initiallyOwned: false, PenName);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or IOException or System.Security.SecurityException)
        {
            // A machine that will not name a mutex still gets its lines; it just does not get the
            // trim. See Say â€” every caller here treats a missing pen as "write anyway".
            return null;
        }
    }

    /// <summary>Take the pen, briefly.</summary>
    /// <param name="pen">The mutex, or null.</param>
    /// <returns><see langword="true"/> where it is held and must be released.</returns>
    private static bool Hold(Mutex? pen)
    {
        if (pen is null)
        {
            return false;
        }

        try
        {
            return pen.WaitOne(PenWait);
        }
        catch (AbandonedMutexException)
        {
            // The previous holder died mid-trim. The wait still succeeded and this process owns it;
            // the worst the abandoned trim can have left is a file over its cap, which the trim
            // about to run fixes.
            return true;
        }
        catch (Exception exception) when (exception is ObjectDisposedException
            or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Keep the newest <see cref="KeptBytes"/> and drop what is older.</summary>
    /// <remarks>
    /// The tail rather than the head, because the reason anybody opens this file is that something
    /// just happened. Trimmed on a whole line, so the first line of a trimmed file is a line and not
    /// the end of one â€” a half-written stamp reads as corruption to whoever finds it.
    ///
    /// <para>Rewritten in place rather than rotated into a second file. Two files is a reader
    /// deciding which one to open, and a rotation on a machine that suspends is two files whose
    /// order is a guess.</para>
    /// </remarks>
    private void TrimIfItHasGrown()
    {
        var file = new FileInfo(_path);
        if (!file.Exists || file.Length <= _kept)
        {
            return;
        }

        // Bytes and not characters, and the difference is not pedantic: the cap is a file size, and
        // a detail this log quotes back â€” a path, a distribution name, the em dash the status lines
        // are written with â€” is more bytes than characters in UTF-8. Trimming to a character count
        // left the file over its own cap, which is the whole failure this bound exists to prevent.
        var all = File.ReadAllBytes(_path);
        var from = all.Length - Math.Min(_kept, all.Length);

        // Forward past the first line break. A cut at an arbitrary offset lands mid-line almost
        // every time, and the offcut is a fragment of a stamp that reads as corruption to whoever
        // finds it â€” which is somebody already suspicious that something is wrong.
        //
        // It is also what makes the byte cut safe: 0x0A cannot occur inside a multi-byte UTF-8
        // sequence, so resuming after one is always a character boundary.
        var start = Array.IndexOf(all, (byte)'\n', from);
        from = start >= 0 && start + 1 < all.Length ? start + 1 : from;

        File.WriteAllBytes(_path, all[from..]);
    }
}
