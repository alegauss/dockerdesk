using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DockerDesk.Core.Agent;

/// <summary>What holds a host port.</summary>
/// <param name="Pid">The owning process.</param>
/// <param name="Image">Its executable's name, which is always readable.</param>
/// <param name="Path">
/// Its executable's full path, or <see langword="null"/> where this process may not open it.
/// </param>
public sealed record PortHolder(int Pid, string Image, string? Path);

/// <summary>Who holds a TCP port on this machine.</summary>
public interface IPortOwners
{
    /// <summary>The process listening on <paramref name="port"/>, where there is one.</summary>
    /// <param name="port">The host port.</param>
    /// <returns>The holder, or <see langword="null"/> when nothing is listening.</returns>
    PortHolder? Holding(int port);
}

/// <summary>
/// The Windows socket table, joined to the process that owns each entry.
/// </summary>
/// <remarks>
/// DD28. <c>port is already allocated</c> is the refusal an agent cannot act on: the daemon knows a bind
/// failed and does not know what holds the socket, and no Docker command anywhere can tell it. Windows
/// knows, and this is a Windows process — which is the whole argument for this product having an agent
/// surface rather than a JSON re-wrapping of what <c>docker</c> already says.
///
/// <para>Read from <c>GetExtendedTcpTable</c> rather than by connecting, for the same reason
/// <see cref="HostPorts"/> is: a connect reaches somebody's service and appears in their log, and a verb
/// under <c>read</c> has promised not to have side effects.</para>
///
/// <para>Both address families are asked. A process listening only on <c>::</c> holds the port as firmly
/// as one on <c>0.0.0.0</c>, and reporting "nothing holds it" because only IPv4 was checked would be a
/// confident wrong answer about the one fact this exists to supply.</para>
/// </remarks>
public sealed class PortOwners : IPortOwners
{
    private const int AfInet = 2;
    private const int AfInet6 = 23;

    /// <summary>TCP_TABLE_OWNER_PID_LISTENER.</summary>
    private const int ListenersWithPid = 3;

    private const int NoError = 0;
    private const int InsufficientBuffer = 122;

    /// <inheritdoc/>
    public PortHolder? Holding(int port)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);

        var pid = OwningPid(port, AfInet) ?? OwningPid(port, AfInet6);
        if (pid is not { } owner)
        {
            return null;
        }

        return Describe(owner);
    }

    /// <summary>Name a process without needing the right to open it.</summary>
    /// <remarks>
    /// <c>ProcessName</c> is readable for any process; <c>MainModule</c> is not — a service running as
    /// another user, or a protected process, refuses it. So the path is best effort and its absence is
    /// reported as absence rather than as a failure: a pid and an image name are already enough to act
    /// on, which is the point of the field.
    /// </remarks>
    private static PortHolder Describe(int pid)
    {
        string image;
        string? path = null;
        try
        {
            using var process = Process.GetProcessById(pid);
            image = process.ProcessName + ".exe";
            try
            {
                path = process.MainModule?.FileName;
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // Readable only for a process this one may open. Named, not guessed.
            }
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException)
        {
            // It exited between the table read and this call, which is a real race on a machine that
            // is doing something. The pid still identifies what held the port a moment ago.
            image = "(exited)";
        }

        return new PortHolder(pid, image, path);
    }

    private static int? OwningPid(int port, int family)
    {
        var size = 0;
        var result = GetExtendedTcpTable(
            IntPtr.Zero, ref size, order: false, family, ListenersWithPid, reserved: 0);
        if (result is not InsufficientBuffer || size <= 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            result = GetExtendedTcpTable(
                buffer, ref size, order: false, family, ListenersWithPid, reserved: 0);
            if (result is not NoError)
            {
                return null;
            }

            var count = Marshal.ReadInt32(buffer);
            var rowSize = family == AfInet
                ? Marshal.SizeOf<TcpRowOwnerPid>()
                : Marshal.SizeOf<Tcp6RowOwnerPid>();
            var at = buffer + sizeof(int);

            for (var i = 0; i < count; i++)
            {
                var (localPort, owner) = family == AfInet
                    ? Read<TcpRowOwnerPid>(at, r => (r.LocalPort, r.OwningPid))
                    : Read<Tcp6RowOwnerPid>(at, r => (r.LocalPort, r.OwningPid));

                if (HostOrder(localPort) == port)
                {
                    return (int)owner;
                }

                at += rowSize;
            }

            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static (uint Port, uint Pid) Read<T>(IntPtr at, Func<T, (uint, uint)> take)
        where T : struct =>
        take(Marshal.PtrToStructure<T>(at));

    /// <summary>
    /// The port, which the table stores in network byte order inside a 32-bit field.
    /// </summary>
    /// <remarks>
    /// Only the low two bytes carry it, and they are big-endian. Reading the field as a number gives
    /// 20480 for port 80, which is the classic way to report the wrong process confidently.
    /// </remarks>
    internal static int HostOrder(uint stored) =>
        (int)(((stored & 0x000000FFu) << 8) | ((stored & 0x0000FF00u) >> 8));

    [StructLayout(LayoutKind.Sequential)]
    private struct TcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Tcp6RowOwnerPid
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] LocalAddr;
        public uint LocalScopeId;
        public uint LocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] RemoteAddr;
        public uint RemoteScopeId;
        public uint RemotePort;
        public uint State;
        public uint OwningPid;
    }

    // DllImport rather than LibraryImport: the generated marshalling stubs need AllowUnsafeBlocks, and
    // turning unsafe on for the whole application to reach one table read is the wrong trade.
    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetExtendedTcpTable(
        IntPtr table, ref int size, bool order, int family, int tableClass, int reserved);
}
