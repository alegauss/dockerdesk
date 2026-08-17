using System.Text.Json;

namespace FreeWilly.Core.Engine;

/// <summary>
/// Whether the engine comes up with the tray, and the file that remembers the answer (DD135).
/// </summary>
/// <remarks>
/// Starting the app and starting the engine were two acts, and the second one was a menu item the
/// user pressed every session with the same answer. So the engine now comes up with the tray.
///
/// <para><b>This is not <see cref="Autostart"/>, and the difference is the whole reason both
/// exist.</b> That one writes the Run key, which puts an engine host on the machine at logon whether
/// or not anybody opens FreeWilly — it is the thing the project's complaint about Docker Desktop is
/// actually about, and it stays off unless asked. This one only decides what happens once the user
/// has already chosen to open the tray, and outside a running tray it decides nothing at all.</para>
///
/// <para><b>A setting rather than a hard-coded start</b>, because the non-goal it sits next to says
/// both the app and the engine run when asked. Shipping it on is a claim about what opening this
/// tool usually means, not a claim that the user cannot mean something else — and turning it off has
/// to restore the old behaviour exactly, or the setting is decoration.</para>
///
/// <para>Not a settings system. One value in one small file beside everything else this tool owns,
/// read the way <c>WindowMemory</c> is read: every failure answers with the default, because a
/// truncated preference file is not a reason to refuse to start an engine.</para>
/// </remarks>
public sealed record EngineOnLaunch
{
    /// <summary>What an install with nothing written down does.</summary>
    /// <remarks>
    /// A constant rather than a literal on the property, because two things have to agree about it:
    /// the default a missing file resolves to, and the test that holds this to shipping on. Written
    /// once, they cannot drift.
    /// </remarks>
    public const bool ShipsOn = true;

    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    /// <summary>Whether opening the tray also starts the engine.</summary>
    public bool StartWithTheTray { get; init; } = ShipsOn;

    /// <summary>Read the setting.</summary>
    /// <param name="path">The file <see cref="Write"/> wrote.</param>
    /// <returns>What it held, or the default where there is nothing usable.</returns>
    /// <remarks>
    /// Never null, unlike the window's own reader. The caller of that one has a meaningful "no
    /// history" branch — open where a window with no past opens — and this one does not: there is a
    /// default and it is <see cref="ShipsOn"/>, so handing back null would only move the same
    /// decision to every call site.
    /// </remarks>
    public static EngineOnLaunch Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return (File.Exists(path)
                ? JsonSerializer.Deserialize<EngineOnLaunch>(File.ReadAllText(path))
                : null) ?? new EngineOnLaunch();
        }
        catch (Exception failure) when (failure is IOException or JsonException
            or UnauthorizedAccessException or NotSupportedException)
        {
            return new EngineOnLaunch();
        }
    }

    /// <summary>Write this down.</summary>
    /// <param name="path">Where to write it.</param>
    /// <remarks>
    /// Silent on failure, because this runs from a menu click on the UI thread and an unhandled
    /// exception there takes the tray icon with it — the defect a click handler that threw already
    /// caused once. The setting not sticking is a smaller loss than the icon vanishing.
    /// </remarks>
    public void Write(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Layout));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException
            or NotSupportedException)
        {
        }
    }
}
