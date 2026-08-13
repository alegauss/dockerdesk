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

    private readonly string _command;

    /// <summary>Construct against a command line.</summary>
    /// <param name="command">What logon should run, quoted as a command line.</param>
    public Autostart(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        _command = command;
    }

    /// <summary>The command line currently registered, or <see langword="null"/> when off.</summary>
    public string? Registered
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(EntryName) as string;
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
    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException($@"HKCU\{RunKey} could not be opened for writing");
        key.SetValue(EntryName, _command, RegistryValueKind.String);
    }

    /// <summary>Turn it off. Idempotent, and removes the value rather than blanking it.</summary>
    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(EntryName) is not null)
        {
            key.DeleteValue(EntryName, throwOnMissingValue: false);
        }
    }
}
