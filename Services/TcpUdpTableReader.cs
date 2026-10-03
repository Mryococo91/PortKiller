using System.Runtime.InteropServices;
using PortKiller.Helpers;
using PortKiller.Models;
using PortKiller.Native;

namespace PortKiller.Services;

public sealed class TcpUdpTableReader
{
    public IReadOnlyList<PortEndpoint> ReadAll()
    {
        var results = new List<PortEndpoint>(256);
        ReadTcp4(results);
        ReadTcp6(results);
        ReadUdp4(results);
        ReadUdp6(results);
        return results;
    }

    private static unsafe void ReadTcp4(List<PortEndpoint> results)
    {
        using NativeBuffer buffer = NativeBuffer.Query(
            (nint table, ref uint size) => IpHelperNative.GetExtendedTcpTable(
                table,
                ref size,
                true,
                IpHelperNative.AfInet,
                IpHelperNative.TcpTableClass.OwnerPidAll,
                0));

        uint count = ReadRowCount(buffer, (uint)sizeof(IpHelperNative.MibTcpRowOwnerPid));
        var rows = (IpHelperNative.MibTcpRowOwnerPid*)((byte*)buffer.Pointer + sizeof(uint));
        for (uint i = 0; i < count; i++)
        {
            IpHelperNative.MibTcpRowOwnerPid row = rows[i];
            results.Add(new PortEndpoint
            {
                Port = IpHelperNative.ToHostPort(row.LocalPort),
                Protocol = NetworkProtocol.Tcp,
                LocalAddress = IpHelperNative.FormatIpv4(row.LocalAddr),
                RemotePort = IpHelperNative.ToHostPort(row.RemotePort),
                RemoteAddress = IpHelperNative.FormatIpv4(row.RemoteAddr),
                State = MapTcpState(row.State),
                ProcessId = row.OwningPid
            });
        }
    }

    private static unsafe void ReadTcp6(List<PortEndpoint> results)
    {
        using NativeBuffer buffer = NativeBuffer.Query(
            (nint table, ref uint size) => IpHelperNative.GetExtendedTcpTable(
                table,
                ref size,
                true,
                IpHelperNative.AfInet6,
                IpHelperNative.TcpTableClass.OwnerPidAll,
                0));

        uint count = ReadRowCount(buffer, (uint)sizeof(IpHelperNative.MibTcp6RowOwnerPid));
        var rows = (IpHelperNative.MibTcp6RowOwnerPid*)((byte*)buffer.Pointer + sizeof(uint));
        for (uint i = 0; i < count; i++)
        {
            IpHelperNative.MibTcp6RowOwnerPid row = rows[i];
            results.Add(new PortEndpoint
            {
                Port = IpHelperNative.ToHostPort(row.LocalPort),
                Protocol = NetworkProtocol.Tcp,
                LocalAddress = IpHelperNative.FormatIpv6(row.LocalAddr, row.LocalScopeId),
                RemotePort = IpHelperNative.ToHostPort(row.RemotePort),
                RemoteAddress = IpHelperNative.FormatIpv6(row.RemoteAddr, row.RemoteScopeId),
                State = MapTcpState(row.State),
                ProcessId = row.OwningPid
            });
        }
    }

    private static unsafe void ReadUdp4(List<PortEndpoint> results)
    {
        using NativeBuffer buffer = NativeBuffer.Query(
            (nint table, ref uint size) => IpHelperNative.GetExtendedUdpTable(
                table,
                ref size,
                true,
                IpHelperNative.AfInet,
                IpHelperNative.UdpTableClass.OwnerPid,
                0));

        uint count = ReadRowCount(buffer, (uint)sizeof(IpHelperNative.MibUdpRowOwnerPid));
        var rows = (IpHelperNative.MibUdpRowOwnerPid*)((byte*)buffer.Pointer + sizeof(uint));
        for (uint i = 0; i < count; i++)
        {
            IpHelperNative.MibUdpRowOwnerPid row = rows[i];
            results.Add(new PortEndpoint
            {
                Port = IpHelperNative.ToHostPort(row.LocalPort),
                Protocol = NetworkProtocol.Udp,
                LocalAddress = IpHelperNative.FormatIpv4(row.LocalAddr),
                State = TcpConnectionState.None,
                ProcessId = row.OwningPid
            });
        }
    }

    private static unsafe void ReadUdp6(List<PortEndpoint> results)
    {
        using NativeBuffer buffer = NativeBuffer.Query(
            (nint table, ref uint size) => IpHelperNative.GetExtendedUdpTable(
                table,
                ref size,
                true,
                IpHelperNative.AfInet6,
                IpHelperNative.UdpTableClass.OwnerPid,
                0));

        uint count = ReadRowCount(buffer, (uint)sizeof(IpHelperNative.MibUdp6RowOwnerPid));
        var rows = (IpHelperNative.MibUdp6RowOwnerPid*)((byte*)buffer.Pointer + sizeof(uint));
        for (uint i = 0; i < count; i++)
        {
            IpHelperNative.MibUdp6RowOwnerPid row = rows[i];
            results.Add(new PortEndpoint
            {
                Port = IpHelperNative.ToHostPort(row.LocalPort),
                Protocol = NetworkProtocol.Udp,
                LocalAddress = IpHelperNative.FormatIpv6(row.LocalAddr, row.LocalScopeId),
                State = TcpConnectionState.None,
                ProcessId = row.OwningPid
            });
        }
    }

    private static unsafe uint ReadRowCount(NativeBuffer buffer, uint rowSize)
    {
        if (buffer.ByteLength < sizeof(uint))
        {
            throw new InvalidOperationException("The network table buffer is too small to contain a row count.");
        }

        uint count = *(uint*)buffer.Pointer;
        ulong needed = (ulong)sizeof(uint) + ((ulong)count * rowSize);
        if (needed > buffer.ByteLength)
        {
            throw new InvalidOperationException(
                $"The network table buffer is smaller than the declared row count ({count}).");
        }

        return count;
    }

    private static TcpConnectionState MapTcpState(uint state) => NetworkMapping.MapTcpState(state);

    private delegate uint NativeTableCall(nint buffer, ref uint size);

    private sealed class NativeBuffer : IDisposable
    {
        public nint Pointer { get; private set; }

        public uint ByteLength { get; private set; }

        public static NativeBuffer Query(NativeTableCall nativeCall)
        {
            uint size = 0;
            uint status = nativeCall(nint.Zero, ref size);
            if (status != IpHelperNative.ErrorInsufficientBuffer && status != IpHelperNative.NoError)
            {
                throw new InvalidOperationException($"Failed to read the network table (code {status}).");
            }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                uint allocSize = Math.Max(size, 4u);
                nint buffer = Marshal.AllocHGlobal((int)allocSize);
                bool transferOwnership = false;
                try
                {
                    uint requestSize = allocSize;
                    status = nativeCall(buffer, ref requestSize);
                    if (status == IpHelperNative.ErrorInsufficientBuffer)
                    {
                        size = requestSize;
                        continue;
                    }

                    if (status != IpHelperNative.NoError)
                    {
                        throw new InvalidOperationException($"Failed to read the network table (code {status}).");
                    }

                    transferOwnership = true;
                    return new NativeBuffer { Pointer = buffer, ByteLength = allocSize };
                }
                finally
                {
                    if (!transferOwnership)
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }

            throw new InvalidOperationException("The network table changed too quickly to be read.");
        }

        public void Dispose()
        {
            if (Pointer != nint.Zero)
            {
                Marshal.FreeHGlobal(Pointer);
                Pointer = nint.Zero;
                ByteLength = 0;
            }
        }
    }
}
