using FreeWilly.Core.Engine;

namespace FreeWilly.Core.Builds;

/// <summary>
/// The ref one launch hands to the window another launch is already showing (DD126).
/// </summary>
/// <remarks>
/// <b>Because the signal carries nothing.</b> One tray owns the window (DD81), so a
/// <c>docker-desktop://</c> link clicked while it is running starts a second process whose whole job
/// is to tell the first one what to open. The object that already does that telling is a named
/// event, and an event has no payload — so the ref travels beside it, in a file.
///
/// <para><b>Taken rather than read.</b> The reader deletes it, so a stale ref cannot make the next
/// ordinary launch open a build nobody asked for. The write is last-one-wins for the same reason:
/// two links clicked in a row should open the second, which is what the user just did.</para>
///
/// <para><b>Every failure is silent and answers null.</b> This is a convenience on the way to a
/// window that is opening regardless — a locked file or a full disk must not be the reason a link
/// does nothing visible at all.</para>
/// </remarks>
public sealed class BuildHandoff
{
    private readonly string _path;

    /// <summary>Construct against the install this machine has.</summary>
    public BuildHandoff()
        : this(new EnginePaths().PendingBuild)
    {
    }

    /// <summary>Construct against an explicit file, which is what a test hands it.</summary>
    /// <param name="path">Where the ref is left.</param>
    public BuildHandoff(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <summary>Leave a ref for the running window to pick up.</summary>
    /// <param name="reference">The ref, already validated by <see cref="BuildAddress.RefIn"/>.</param>
    /// <returns><see langword="true"/> where it was written.</returns>
    public bool Leave(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, reference);
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Take whatever was left, removing it.</summary>
    /// <returns>The ref, or <see langword="null"/> where none was waiting.</returns>
    /// <remarks>
    /// Validated on the way out and not only on the way in. The file is on disk between two
    /// processes, so what is read here is not necessarily what this wrote — and the ref reaches a
    /// subprocess argument, which is not a place to pass an unchecked string.
    /// </remarks>
    public string? Take()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var text = File.ReadAllText(_path);
            File.Delete(_path);
            return BuildAddress.RefIn(text);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
