using System.Globalization;

namespace FreeWilly.Core.Engine;

/// <summary>
/// What the engine host saw, written where it outlives the window nobody was reading (DD137).
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
/// <para><b>What this is not.</b> Not a trace of every poll — the supervisor wakes every two
/// seconds, and a line each time is a file that says nothing in a great many words. A quiet engine
/// writes nothing at all, so anything in here is something that happened.</para>
/// </remarks>
public sealed class EngineHostLog
{
    /// <summary>
    /// How much of the file is kept.
    /// </summary>
    /// <remarks>
    /// A file that grows without bound is its own defect, and this one is written by a process that
    /// can run for weeks. 64 KB is a few thousand of these lines — far more than any single
    /// investigation reads, and small enough that nobody has to think about it.
    /// </remarks>
    public const int KeptBytes = 64 * 1024;

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
    /// the file is worth more than the console was — a sixty-second gap is only visible if the lines
    /// either side of it carry a clock.
    /// </remarks>
    public void Say(string line)
    {
        var stamped = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {line}");

        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(_path, stamped + Environment.NewLine);
            TrimIfItHasGrown();
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
    }

    /// <summary>Keep the newest <see cref="KeptBytes"/> and drop what is older.</summary>
    /// <remarks>
    /// The tail rather than the head, because the reason anybody opens this file is that something
    /// just happened. Trimmed on a whole line, so the first line of a trimmed file is a line and not
    /// the end of one — a half-written stamp reads as corruption to whoever finds it.
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
        // a detail this log quotes back — a path, a distribution name, the em dash the status lines
        // are written with — is more bytes than characters in UTF-8. Trimming to a character count
        // left the file over its own cap, which is the whole failure this bound exists to prevent.
        var all = File.ReadAllBytes(_path);
        var from = all.Length - Math.Min(_kept, all.Length);

        // Forward past the first line break. A cut at an arbitrary offset lands mid-line almost
        // every time, and the offcut is a fragment of a stamp that reads as corruption to whoever
        // finds it — which is somebody already suspicious that something is wrong.
        //
        // It is also what makes the byte cut safe: 0x0A cannot occur inside a multi-byte UTF-8
        // sequence, so resuming after one is always a character boundary.
        var start = Array.IndexOf(all, (byte)'\n', from);
        from = start >= 0 && start + 1 < all.Length ? start + 1 : from;

        File.WriteAllBytes(_path, all[from..]);
    }
}
