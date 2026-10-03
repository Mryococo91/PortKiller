namespace PortKiller.Models;

public sealed class PortEndpoint
{
    public required int Port { get; init; }
    public required NetworkProtocol Protocol { get; init; }
    public required string LocalAddress { get; init; }
    public int? RemotePort { get; init; }
    public string? RemoteAddress { get; init; }
    public required TcpConnectionState State { get; init; }
    public required uint ProcessId { get; init; }
}
