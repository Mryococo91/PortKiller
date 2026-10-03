using PortKiller.Helpers;
using PortKiller.Models;
using Xunit;

namespace PortKiller.Tests;

public sealed class PortDisplayFormatterTests
{
    [Theory]
    [InlineData(NetworkProtocol.Tcp, "Protocol_Tcp", "TCP")]
    [InlineData(NetworkProtocol.Udp, "Protocol_Udp", "UDP")]
    public void Protocol_KeysAndCanonical(NetworkProtocol protocol, string key, string canonical)
    {
        Assert.Equal(key, PortDisplayFormatter.GetProtocolResourceKey(protocol));
        Assert.Equal(canonical, PortDisplayFormatter.GetCanonicalProtocol(protocol));
    }

    [Theory]
    [InlineData(NetworkProtocol.Tcp, TcpConnectionState.Listen, "State_Listen", "LISTENING")]
    [InlineData(NetworkProtocol.Tcp, TcpConnectionState.Established, "State_Established", "ESTABLISHED")]
    [InlineData(NetworkProtocol.Tcp, TcpConnectionState.TimeWait, "State_TimeWait", "TIME_WAIT")]
    [InlineData(NetworkProtocol.Udp, TcpConnectionState.None, "State_NotApplicable", "—")]
    public void State_KeysAndCanonical(
        NetworkProtocol protocol,
        TcpConnectionState state,
        string key,
        string canonical)
    {
        Assert.Equal(key, PortDisplayFormatter.GetStateResourceKey(protocol, state));
        Assert.Equal(canonical, PortDisplayFormatter.GetCanonicalState(protocol, state));
    }
}
