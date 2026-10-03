using System.Runtime.InteropServices;
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

        uint count = *(uint*)buffer.Pointer;
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

        uint count = *(uint*)buffer.Pointer;
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

        uint count = *(uint*)buffer.Pointer;
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

        uint count = *(uint*)buffer.Pointer;
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

    private static TcpConnectionState MapTcpState(uint state)
    {
        var mapped = (TcpConnectionState)state;
        return Enum.IsDefined(mapped) ? mapped : TcpConnectionState.None;
    }

    private delegate uint NativeTableCall(nint buffer, ref uint size);

    private sealed class NativeBuffer : IDisposable
    {
        public nint Pointer { get; private set; }

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
                nint buffer = Marshal.AllocHGlobal((int)Math.Max(size, 4u));
                bool transferOwnership = false;
                try
                {
                    status = nativeCall(buffer, ref size);
                    if (status == IpHelperNative.ErrorInsufficientBuffer)
                    {
                        continue;
                    }

                    if (status != IpHelperNative.NoError)
                    {
                        throw new InvalidOperationException($"Failed to read the network table (code {status}).");
                    }

                    transferOwnership = true;
                    return new NativeBuffer { Pointer = buffer };
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
            }
        }
    }
}
