using PortKiller.Helpers;
using PortKiller.Models;
using Xunit;

namespace PortKiller.Tests;

public sealed class PortEntryCsvExporterTests : IDisposable
{
    private readonly IDisposable _localize;

    public PortEntryCsvExporterTests()
    {
        _localize = PortDisplayFormatter.Use(static key => key switch
        {
            "Protocol_Tcp" => "TCP",
            "State_Listen" => "LISTENING",
            "State_Established" => "ESTABLISHED",
            _ => key
        });
    }

    public void Dispose() => _localize.Dispose();

    [Fact]
    public void Export_IncludesBomRemoteColumnsAndEscapesCommas()
    {
        var entry = new PortEntry
        {
            Endpoint = new PortEndpoint
            {
                Port = 3000,
                Protocol = NetworkProtocol.Tcp,
                LocalAddress = "127.0.0.1",
                RemoteAddress = "10.0.0.5",
                RemotePort = 443,
                State = TcpConnectionState.Established,
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
        Assert.StartsWith("\uFEFF", csv, StringComparison.Ordinal);

        string[] lines = csv.TrimStart('\uFEFF')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Port,Protocol,State,LocalAddress,RemoteAddress,RemotePort,PID,Process,Path", lines[0]);
        Assert.Equal(
            "3000,TCP,ESTABLISHED,127.0.0.1,10.0.0.5,443,42,node,\"C:\\Program Files\\node,x64\\node.exe\"",
            lines[1]);
    }

    [Fact]
    public void Export_PrefixesFormulaTriggerCharacters()
    {
        var entry = new PortEntry
        {
            Endpoint = new PortEndpoint
            {
                Port = 80,
                Protocol = NetworkProtocol.Tcp,
                LocalAddress = "127.0.0.1",
                State = TcpConnectionState.Listen,
                ProcessId = 7
            },
            Process = new ProcessIdentity
            {
                ProcessId = 7,
                ProcessName = "=cmd",
                ExecutablePath = @"+C:\evil\payload.exe"
            }
        };

        string csv = PortEntryCsvExporter.Export([entry]);
        string line = csv.TrimStart('\uFEFF')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)[1];

        Assert.Contains("'=cmd", line, StringComparison.Ordinal);
        Assert.Contains("'+C:\\evil\\payload.exe", line, StringComparison.Ordinal);
    }
}
