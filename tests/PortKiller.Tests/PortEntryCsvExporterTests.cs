using PortKiller.Helpers;
using PortKiller.Models;
using Xunit;

namespace PortKiller.Tests;

public sealed class PortEntryCsvExporterTests
{
    public PortEntryCsvExporterTests()
    {
        PortDisplayFormatter.Localize = static key => key switch
        {
            "Protocol_Tcp" => "TCP",
            "State_Listen" => "LISTENING",
            _ => key
        };
    }

    [Fact]
    public void Export_IncludesHeaderAndEscapesCommas()
    {
        PortDisplayFormatter.Localize = static key => key switch
        {
            "Protocol_Tcp" => "TCP",
            "State_Listen" => "LISTENING",
            _ => key
        };

        var entry = new PortEntry
        {
            Endpoint = new PortEndpoint
            {
                Port = 3000,
                Protocol = NetworkProtocol.Tcp,
                LocalAddress = "127.0.0.1",
                State = TcpConnectionState.Listen,
                ProcessId = 42
            },
            Process = new ProcessIdentity
            {
                ProcessId = 42,
                ProcessName = "node",
                ExecutablePath = @"C:\Program Files\node,x64\node.exe"
            }
        };

        string csv = PortEntryCsvExporter.Export([entry]);
        string[] lines = csv.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Port,Protocol,State,LocalAddress,PID,Process,Path", lines[0]);
        Assert.Equal("3000,TCP,LISTENING,127.0.0.1,42,node,\"C:\\Program Files\\node,x64\\node.exe\"", lines[1]);
    }
}
