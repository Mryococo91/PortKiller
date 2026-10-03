using PortKiller.Helpers;
using PortKiller.Models;
using Xunit;

namespace PortKiller.Tests;

public sealed class NetworkMappingTests
{
    [Theory]
    [InlineData(2u, TcpConnectionState.Listen)]
    [InlineData(5u, TcpConnectionState.Established)]
    [InlineData(11u, TcpConnectionState.TimeWait)]
    [InlineData(0u, TcpConnectionState.None)]
    [InlineData(999u, TcpConnectionState.None)]
    public void MapTcpState_KnownAndUnknown(uint raw, TcpConnectionState expected)
    {
        Assert.Equal(expected, NetworkMapping.MapTcpState(raw));
    }

    [Theory]
    [InlineData(0xBB13u, 5051)] // low 16 bits in network byte order
    [InlineData(0xB80Bu, 3000)]
    [InlineData(0x5000u, 80)]
    public void ToHostPort_ReversesNetworkOrderLowWord(uint networkPort, int expected)
    {
        Assert.Equal(expected, NetworkMapping.ToHostPort(networkPort));
    }

    [Fact]
    public void FormatIpv4_MatchesDottedQuad()
    {
        // 127.0.0.1 in network order on little-endian Windows MIB storage
        uint loopback = BitConverter.ToUInt32([127, 0, 0, 1], 0);
        Assert.Equal("127.0.0.1", NetworkMapping.FormatIpv4(loopback));
    }
}
