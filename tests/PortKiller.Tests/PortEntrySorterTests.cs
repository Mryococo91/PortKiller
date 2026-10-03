using PortKiller.Helpers;
using PortKiller.Models;
using Xunit;

namespace PortKiller.Tests;

public sealed class PortEntrySorterTests : IDisposable
{
    private readonly IDisposable _localize;

    public PortEntrySorterTests()
    {
        _localize = PortDisplayFormatter.Use(static key => key switch
        {
            "Protocol_Tcp" => "TCP",
            "Protocol_Udp" => "UDP",
            "State_Listen" => "LISTENING",
            "State_Established" => "ESTABLISHED",
            "State_NotApplicable" => "—",
            _ => key
        });
    }

    public void Dispose() => _localize.Dispose();

    [Fact]
    public void Sort_ByPortDescending()
    {
        PortEntry[] entries =
        [
            Create(80, "httpd"),
            Create(443, "chrome"),
            Create(3000, "node")
        ];

        List<PortEntry> sorted = PortEntrySorter.Sort(entries, PortSortColumn.Port, ascending: false).ToList();

        Assert.Equal([3000, 443, 80], sorted.Select(entry => entry.Port));
    }

    [Fact]
    public void Sort_ByProcessAscending()
    {
        PortEntry[] entries =
        [
            Create(1, "zeta"),
            Create(2, "alpha"),
            Create(3, "beta")
        ];

        List<PortEntry> sorted = PortEntrySorter.Sort(entries, PortSortColumn.Process, ascending: true).ToList();

        Assert.Equal(["alpha", "beta", "zeta"], sorted.Select(entry => entry.ProcessNameDisplay));
    }

    private static PortEntry Create(int port, string processName) =>
        new()
        {
            Endpoint = new PortEndpoint
            {
                Port = port,
                Protocol = NetworkProtocol.Tcp,
                LocalAddress = "127.0.0.1",
                State = TcpConnectionState.Listen,
                ProcessId = (uint)port
            },
            Process = new ProcessIdentity
            {
                ProcessId = port,
                ProcessName = processName
            }
        };
}
