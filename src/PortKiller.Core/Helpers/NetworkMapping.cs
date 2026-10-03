using System.Buffers.Binary;
using System.Net;
using PortKiller.Models;

namespace PortKiller.Helpers;

/// <summary>
/// Pure mapping helpers for IP Helper row fields (unit-testable, no P/Invoke).
/// </summary>
public static class NetworkMapping
{
    public static TcpConnectionState MapTcpState(uint state)
    {
        var mapped = (TcpConnectionState)state;
        return Enum.IsDefined(mapped) ? mapped : TcpConnectionState.None;
    }

    /// <summary>Convert a network-order port dword (low 16 bits) to a host-order port.</summary>
    public static int ToHostPort(uint networkPort) =>
        BinaryPrimitives.ReverseEndianness((ushort)(networkPort & 0xFFFF));

    public static string FormatIpv4(uint networkAddress) =>
        new IPAddress(BitConverter.GetBytes(networkAddress)).ToString();
}
