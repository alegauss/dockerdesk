// System.IO is not in this project's implicit usings: enabling WinForms replaces the SDK's default
// list rather than adding to it, which is also why the rest of this project spells System.IO.Path out.
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DockerDesk.Tray.Cli;

/// <summary>
/// Gives a windowed executable the console it was typed into.
/// </summary>
/// <remarks>
/// The price of one <c>.exe</c>. A tray icon must be a Windows subsystem executable — the console
/// one flashes a black window on every start — and Windows gives such a process no console at all,
/// so <c>Console.Out</c> resolves to <see cref="Stream.Null"/> and every verb here would print into
/// nothing. <c>AttachConsole(ATTACH_PARENT_PROCESS)</c> is the documented way back: it borrows the
/// console of whatever started this, which for a typed command is the terminal the user is looking
/// at.
///
/// One wart is inherent and worth stating rather than hiding: the shell does not wait for a windowed
/// process, so the prompt returns before the output does, and the report lands under it. Redirecting
/// (<c>DockerDesk.exe --preflight --json &gt; report.json</c>) has neither problem, which is the form
/// an installer and a script use anyway.
/// </remarks>
internal static class ParentConsole
{
    private const int StdOutputHandle = -11;
    private const uint AttachParentProcess = 0xFFFFFFFF;
    private const uint FileTypeUnknown = 0x0000;
    private const uint FileTypeChar = 0x0002;

    /// <summary>
    /// Attach to the caller's console, and make <see cref="Console"/> write to it.
    /// </summary>
    /// <remarks>
    /// Called once, before any verb writes anything. Nothing here can fail in a way worth reporting:
    /// the only outcome of a failure is output nobody was reading.
    /// </remarks>
    internal static void Attach()
    {
        // Only when the caller has not already redirected us. `DockerDesk.exe --preflight > file`
        // hands this process a file as its standard output, and that handle is inherited whatever
        // the subsystem is — attaching a console on top of it is how a redirect ends up half in the
        // file and half on the screen.
        if (!Redirected())
        {
            _ = AttachConsole(AttachParentProcess);
        }

        // The report carries em dashes and arrows whatever the console's code page is. This throws
        // when there is no console and no redirect either — a double-click, or the tray's own
        // detached `--run` — and there is nothing to say about it, because nothing is reading.
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // Nobody is listening.
        }

        // Re-point both writers from the handles as they are now. Setting OutputEncoding above
        // already discards whatever was cached, but only on the path where it did not throw, and a
        // writer cached as Stream.Null before the attach is exactly the failure this exists to
        // prevent.
        Console.SetOut(Writer(Console.OpenStandardOutput()));
        Console.SetError(Writer(Console.OpenStandardError()));
    }

    private static StreamWriter Writer(Stream stream) =>
        new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };

    /// <summary>Whether standard output is already a file or a pipe rather than a console.</summary>
    private static bool Redirected()
    {
        var handle = GetStdHandle(StdOutputHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return false;
        }

        // FILE_TYPE_CHAR is a console or a printer; DISK and PIPE are the two redirections. UNKNOWN
        // means the handle is not usable, which is the same as not having one.
        var type = GetFileType(handle);
        return type is not (FileTypeUnknown or FileTypeChar);
    }

    // DllImport rather than LibraryImport: the source generator's marshalling stubs need
    // AllowUnsafeBlocks, and turning unsafe code on for the whole application to reach three
    // parameterless-marshalling calls is the wrong trade. Nothing here marshals a string or a
    // struct, which is where LibraryImport would earn its cost.
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int which);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr handle);
}
