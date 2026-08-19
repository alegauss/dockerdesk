using System.Text.Json;

namespace FreeWilly.Core.Settings;

/// <summary>
/// Everything the user has decided about how this tool behaves, and the file that remembers it.
/// </summary>
/// <remarks>
/// <b>One record because there is one file.</b> This was <c>EngineOnLaunch</c>, which held the single
/// setting DD135 introduced and was named after it. DD154 needed a second, and a second record over
/// the same path would have deleted the first value on every write: <see cref="Write"/> serialises
/// the object it is called on, so two writers each round-trip only their own property and each one's
/// save is the other one's reset. So the type is named after the file rather than after either
/// setting, and a third setting joins it here.
///
/// <para><b>Still not a settings system.</b> Two values in one small file beside everything else this
/// tool owns, read the way <c>WindowMemory</c> is read: every failure answers with the defaults,
/// because a truncated preference file is not a reason to refuse to start an engine.</para>
///
/// <para><b>The two defaults point in opposite directions, and that is deliberate.</b> The engine
/// comes up with the tray because opening this tool usually means wanting one. The release check does
/// not, because it is outbound traffic — see <see cref="CheckForReleases"/>.</para>
/// </remarks>
public sealed record TraySettings
{
    /// <summary>What an install with nothing written down does about the engine.</summary>
    /// <remarks>
    /// A constant rather than a literal on the property, because two things have to agree about it:
    /// the default a missing file resolves to, and the test that holds this to shipping on. Written
    /// once, they cannot drift.
    /// </remarks>
    public const bool EngineShipsOn = true;

    /// <summary>What an install with nothing written down does about looking for releases.</summary>
    /// <remarks>
    /// Off, and this is the one default in this file that is a promise rather than a convenience. The
    /// site says the only network traffic this project makes is downloading the five pinned artefacts
    /// during a provision the user asked for; a check that shipped on would make that sentence false
    /// on every machine before anybody had chosen anything.
    /// </remarks>
    public const bool ReleaseCheckShipsOn = false;

    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    /// <summary>Whether opening the tray also starts the engine (DD135).</summary>
    public bool StartWithTheTray { get; init; } = EngineShipsOn;

    /// <summary>Whether this install looks for a newer release (DD154).</summary>
    /// <remarks>
    /// Off unless turned on, and the switch is what the promise about outbound traffic is qualified
    /// by. Nothing about the user goes out either way — the request carries a user agent and asks for
    /// one release's tag — but it does reach a host the rest of this tool never touches, which a
    /// proxy may block and whose unauthenticated budget a shared NAT shares.
    /// </remarks>
    public bool CheckForReleases { get; init; } = ReleaseCheckShipsOn;

    /// <summary>Read the settings.</summary>
    /// <param name="path">The file <see cref="Write"/> wrote.</param>
    /// <returns>What it held, or the defaults where there is nothing usable.</returns>
    /// <remarks>
    /// Never null, unlike the window's own reader. The caller of that one has a meaningful "no
    /// history" branch — open where a window with no past opens — and this one does not: there are
    /// defaults, and handing back null would only move the same decision to every call site.
    /// </remarks>
    public static TraySettings Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return (File.Exists(path)
                ? JsonSerializer.Deserialize<TraySettings>(File.ReadAllText(path))
                : null) ?? new TraySettings();
        }
        catch (Exception failure) when (failure is IOException or JsonException
            or UnauthorizedAccessException or NotSupportedException)
        {
            return new TraySettings();
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
