using PortKiller.Helpers;
using PortKiller.Models;
using Xunit;

namespace PortKiller.Tests;

public sealed class PortEntryFilterTests
{
    public PortEntryFilterTests()
    {
        PortDisplayFormatter.Localize = static key => key switch
        {
            "Protocol_Tcp" => "TCP",
            "Protocol_Udp" => "UDP",
            "State_Listen" => "ÉCOUTE",
            "State_Established" => "ÉTABLI",
            "State_NotApplicable" => "—",
            _ => key
        };
    }

    [Fact]
    public void BusinessFilter_KeepsUdpAndListeningTcp_ByDefault()
    {
        PortEntry listening = CreateEntry(3000, NetworkProtocol.Tcp, TcpConnectionState.Listen, "node");
        PortEntry established = CreateEntry(3000, NetworkProtocol.Tcp, TcpConnectionState.Established, "node");
        PortEntry udp = CreateEntry(5353, NetworkProtocol.Udp, TcpConnectionState.None, "mdns");

        Assert.True(PortEntryFilter.MatchesBusinessFilter(listening, showAllTcpConnections: false));
        Assert.False(PortEntryFilter.MatchesBusinessFilter(established, showAllTcpConnections: false));
        Assert.True(PortEntryFilter.MatchesBusinessFilter(udp, showAllTcpConnections: false));
    }

    [Fact]
    public void BusinessFilter_ShowsAllTcp_WhenEnabled()
    {
        PortEntry established = CreateEntry(443, NetworkProtocol.Tcp, TcpConnectionState.Established, "chrome");
        Assert.True(PortEntryFilter.MatchesBusinessFilter(established, showAllTcpConnections: true));
    }

    [Theory]
    [InlineData("3000")]
    [InlineData("node")]
    [InlineData("LISTENING")]
    [InlineData("ÉCOUTE")]
    [InlineData("TCP")]
    public void Search_MatchesPortNameCanonicalAndLocalizedState(string query)
    {
        PortEntry entry = CreateEntry(3000, NetworkProtocol.Tcp, TcpConnectionState.Listen, "node.exe");
        Assert.True(PortEntryFilter.MatchesSearch(entry, query));
    }

    [Fact]
    public void Search_EmptyQuery_MatchesEverything()
    {
        PortEntry entry = CreateEntry(80, NetworkProtocol.Tcp, TcpConnectionState.Listen, "httpd");
        Assert.True(PortEntryFilter.MatchesSearch(entry, "   "));
    }

    private static PortEntry CreateEntry(
        int port,
        NetworkProtocol protocol,
        TcpConnectionState state,
        string processName)
    {
        return new PortEntry
        {
            Endpoint = new PortEndpoint
            {
                Port = port,
                Protocol = protocol,
                LocalAddress = "127.0.0.1",
                State = state,
                ProcessId = 4242
            },
            Process = new ProcessIdentity
            {
                ProcessId = 4242,
                ProcessName = processName,
                ExecutablePath = $@"C:\tools\{processName}"
            }
        };
    }
}
