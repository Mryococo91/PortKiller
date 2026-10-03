using System.Net;
using System.Runtime.InteropServices;
using PortKiller.Helpers;

namespace PortKiller.Native;

internal static partial class IpHelperNative
{
    internal const uint AfInet = 2;
    internal const uint AfInet6 = 23;
    internal const uint ErrorInsufficientBuffer = 122;
    internal const uint NoError = 0;

    internal enum TcpTableClass : uint
    {
        BasicListener = 0,
        BasicConnections = 1,
        BasicAll = 2,
        OwnerPidListener = 3,
        OwnerPidConnections = 4,
        OwnerPidAll = 5,
        OwnerModuleListener = 6,
        OwnerModuleConnections = 7,
        OwnerModuleAll = 8
    }

    internal enum UdpTableClass : uint
    {
        Basic = 0,
        OwnerPid = 1,
        OwnerModule = 2
    }

    [LibraryImport("iphlpapi.dll")]
    internal static partial uint GetExtendedTcpTable(
        nint tcpTable,
        ref uint size,
        [MarshalAs(UnmanagedType.Bool)] bool order,
        uint addressFamily,
        TcpTableClass tableClass,
        uint reserved);

    [LibraryImport("iphlpapi.dll")]
    internal static partial uint GetExtendedUdpTable(
        nint udpTable,
        ref uint size,
        [MarshalAs(UnmanagedType.Bool)] bool order,
        uint addressFamily,
        UdpTableClass tableClass,
        uint reserved);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct MibTcp6RowOwnerPid
    {
        public fixed byte LocalAddr[16];
        public uint LocalScopeId;
        public uint LocalPort;
        public fixed byte RemoteAddr[16];
        public uint RemoteScopeId;
        public uint RemotePort;
        public uint State;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MibUdpRowOwnerPid
    {
        public uint LocalAddr;
        public uint LocalPort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct MibUdp6RowOwnerPid
    {
        public fixed byte LocalAddr[16];
        public uint LocalScopeId;
        public uint LocalPort;
        public uint OwningPid;
    }

    internal static int ToHostPort(uint networkPort) => NetworkMapping.ToHostPort(networkPort);

    internal static string FormatIpv4(uint networkAddress) => NetworkMapping.FormatIpv4(networkAddress);

    internal static unsafe string FormatIpv6(byte* address, uint scopeId)
    {
        Span<byte> bytes = stackalloc byte[16];
        new ReadOnlySpan<byte>(address, 16).CopyTo(bytes);
        return new IPAddress(bytes, scopeId).ToString();
    }
}
