using Microsoft.Win32;

namespace FreeWilly.Core.Engine;

/// <summary>
/// Whether the engine starts itself at logon. Off unless the user turns it on.
/// </summary>
/// <remarks>
/// This is the difference the project is built around. Docker Desktop starting on every boot and
/// holding several gigabytes is the complaint that sends people looking for an alternative, so the
/// default here is not a preference — it is the product. Nothing is written to the registry until
/// somebody asks for it, and turning it off removes the value rather than setting it to zero.
///
/// The per-user Run key, and not a scheduled task or a service: it needs no administrator, which is
/// the same reason the whole install lives under LOCALAPPDATA.
/// </remarks>
public sealed class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>The name this tool's entry has under the Run key.</summary>
    public const string EntryName = "FreeWilly";

    /// <summary>
    /// The name an install made before the rename used (DD57).
    /// </summary>
    /// <remarks>
    /// Cleaned up rather than adopted, and that is the opposite of what DD55 decided for the
    /// distribution and the app root — because those hold state and this holds none. A Run value is
    /// a label on a command line, so there is nothing to preserve and one thing to prevent: the old
    /// value points at an executable this build no longer produces, and left in place logon fails
    /// silently while this class reports autostart as off. Two answers about one setting.
    /// </remarks>
    public const string LegacyEntryName = "DockerDesk";

    private readonly string _command;

    /// <summary>Construct against a command line.</summary>
    /// <param name="command">What logon should run, quoted as a command line.</param>
    public Autostart(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        _command = command;
    }

    /// <summary>The command line currently registered, or <see langword="null"/> when off.</summary>
    /// <remarks>
    /// The old name answers too (DD57). A machine that had autostart on before the rename really
    /// does start something at logon, and reporting that as off would be this class disagreeing with
    /// Windows. It reads as on and stale, which <see cref="Current"/> already tells apart — and the
    /// remedy for stale is <see cref="Enable"/>, which replaces it.
    /// </remarks>
    public string? Registered
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(EntryName) as string ?? key?.GetValue(LegacyEntryName) as string;
        }
    }

    /// <summary>Whether autostart is on.</summary>
    public bool Enabled => Registered is not null;

    /// <summary>Whether it is on and points at what this build would register.</summary>
    /// <remarks>
    /// A stale entry from a previous install location is on, and broken. Worth telling apart from
    /// off, because the remedy is different: one is a checkbox, the other is a repair.
    /// </remarks>
    public bool Current => string.Equals(Registered, _command, StringComparison.OrdinalIgnoreCase);

    /// <summary>Turn it on, or update a stale entry. Idempotent.</summary>
    /// <remarks>
    /// Writes the current name and removes the old one in the same call, so a machine can never be
    /// left starting this tool twice at logon under two labels (DD57).
    /// </remarks>
    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException($@"HKCU\{RunKey} could not be opened for writing");
        key.SetValue(EntryName, _command, RegistryValueKind.String);
        key.DeleteValue(LegacyEntryName, throwOnMissingValue: false);
    }

    /// <summary>Turn it off. Idempotent, and removes the value rather than blanking it.</summary>
    /// <remarks>
    /// Both names, because off has to mean off: leaving the old one behind would be a setting the
    /// user turned off that still runs something.
    /// </remarks>
    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(EntryName, throwOnMissingValue: false);
        key?.DeleteValue(LegacyEntryName, throwOnMissingValue: false);
    }
}
