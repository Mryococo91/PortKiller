using PortKiller.Helpers;

namespace PortKiller.Models;

public sealed class PortEntry
{
    public required PortEndpoint Endpoint { get; init; }
    public ProcessIdentity? Process { get; init; }

    public int Port => Endpoint.Port;

    public string ProtocolDisplay => PortDisplayFormatter.FormatProtocol(Endpoint.Protocol);

    public string StateDisplay => PortDisplayFormatter.FormatState(Endpoint.Protocol, Endpoint.State);

    public string LocalAddressDisplay => FormatAddress(Endpoint.LocalAddress);

    public int ProcessId => unchecked((int)Endpoint.ProcessId);

    public string ProcessNameDisplay => Process?.FriendlyName ?? "—";

    public string? ExecutablePath => Process?.ExecutablePath;

    public string EndpointKey =>
        $"{Endpoint.Protocol}:{Endpoint.LocalAddress}:{Endpoint.Port}:{Endpoint.ProcessId}:{Endpoint.State}";

    private static string FormatAddress(string address)
    {
        if (address.Contains(':', StringComparison.Ordinal))
        {
            return $"[{address}]";
        }

        return address;
    }
}
