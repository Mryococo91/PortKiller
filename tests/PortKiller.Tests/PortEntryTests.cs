using PortKiller.Models;
using Xunit;

namespace PortKiller.Tests;

public sealed class PortEntryTests
{
    [Fact]
    public void EndpointKey_IncludesRemoteEndpoint()
    {
        var localOnly = CreateEntry(remoteAddress: null, remotePort: null, state: TcpConnectionState.Listen);
        var peerA = CreateEntry(remoteAddress: "1.1.1.1", remotePort: 443, state: TcpConnectionState.Established);
        var peerB = CreateEntry(remoteAddress: "8.8.8.8", remotePort: 443, state: TcpConnectionState.Established);

        Assert.NotEqual(peerA.EndpointKey, peerB.EndpointKey);
        Assert.Contains("1.1.1.1", peerA.EndpointKey, StringComparison.Ordinal);
        Assert.Contains("8.8.8.8", peerB.EndpointKey, StringComparison.Ordinal);
        Assert.Contains("Listen", localOnly.EndpointKey, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoteEndpointDisplay_FormatsAddressAndPort()
    {
        var entry = CreateEntry(remoteAddress: "2001:db8::1", remotePort: 8080, state: TcpConnectionState.Established);

        Assert.Equal("[2001:db8::1]:8080", entry.RemoteEndpointDisplay);
        Assert.Equal("[2001:db8::1]", entry.RemoteAddressDisplay);
    }

    private static PortEntry CreateEntry(string? remoteAddress, int? remotePort, TcpConnectionState state) =>
        new()
        {
            Endpoint = new PortEndpoint
            {
                Port = 3000,
                Protocol = NetworkProtocol.Tcp,
                LocalAddress = "127.0.0.1",
                RemoteAddress = remoteAddress,
                RemotePort = remotePort,
                State = state,
                ProcessId = 42
            }
        };
}
