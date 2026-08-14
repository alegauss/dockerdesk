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
///
/// <para><b>Two entries, because there are two settings</b> (DD97). Starting the tray at logon and
/// starting the engine at logon are independent, and a user may want either, both or neither. They
/// shared one value name until DD97, written by two things that meant different things by it: the
/// installer's checkbox wrote <c>--tray</c> and <c>--autostart on</c> wrote <c>--run</c> over it, so
/// whichever ran last won. Turning the engine on stopped the tray appearing; turning it off deleted
/// a box the user had ticked in the installer, because <see cref="Disable"/> removes rather than
/// blanks and could not know whose entry it was removing.</para>
/// </remarks>
public sealed class Autostart
{
    /// <summary>Where Windows looks at logon.</summary>
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>The name the tray's own entry has under the Run key.</summary>
    /// <remarks>
    /// The installer writes this one and nothing at runtime does, so it is here to be *read* — by
    /// the test that holds the installer's spelling to it, which is the only thing that would ever
    /// notice the two files drifting apart.
    /// </remarks>
    public const string TrayEntryName = "FreeWilly";

    /// <summary>The name the engine's entry has under the Run key.</summary>
    /// <remarks>
    /// Distinct from <see cref="TrayEntryName"/>, and that distinctness is the whole of DD97 — a
    /// test asserts they differ, because equal is the state this repaired.
    /// </remarks>
    public const string EngineEntryName = "FreeWilly Engine";

    private readonly string _command;
    private readonly string _entry;
    private readonly string _key;

    /// <summary>Construct against a command line.</summary>
    /// <param name="command">What logon should run, quoted as a command line.</param>
    /// <param name="entry">Which entry, defaulting to the engine's.</param>
    /// <param name="key">
    /// Which key under HKCU, defaulting to the real one. A test passes a scratch key of its own:
    /// what DD97 repaired is one verb deleting another feature's value, and the only way to assert
    /// that is to write both and take one away — which must not happen to the machine running the
    /// suite.
    /// </param>
    public Autostart(string command, string entry = EngineEntryName, string key = RunKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _command = command;
        _entry = entry;
        _key = key;
    }

    /// <summary>The command line currently registered, or <see langword="null"/> when off.</summary>
    public string? Registered
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(_key);
            return key?.GetValue(_entry) as string;
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

    /// <summary>Which entry under the Run key this reads and writes.</summary>
    public string EntryName => _entry;

    /// <summary>Turn it on, or update a stale entry. Idempotent.</summary>
    /// <remarks>
    /// Writes one value under one name, and since DD97 that name is this setting's alone: the other
    /// entry belongs to the other setting and is never touched from here.
    /// </remarks>
    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_key, writable: true)
            ?? throw new InvalidOperationException($@"HKCU\{_key} could not be opened for writing");
        key.SetValue(_entry, _command, RegistryValueKind.String);
    }

    /// <summary>Turn it off. Idempotent, and removes the value rather than blanking it.</summary>
    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_key, writable: true);
        key?.DeleteValue(_entry, throwOnMissingValue: false);
    }
}
