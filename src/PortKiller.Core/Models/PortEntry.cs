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

    public string RemoteAddressDisplay =>
        string.IsNullOrWhiteSpace(Endpoint.RemoteAddress)
            ? string.Empty
            : FormatAddress(Endpoint.RemoteAddress);

    public string RemoteEndpointDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Endpoint.RemoteAddress))
            {
                return string.Empty;
            }

            return Endpoint.RemotePort is { } remotePort
                ? $"{RemoteAddressDisplay}:{remotePort}"
                : RemoteAddressDisplay;
        }
    }

    public int ProcessId => unchecked((int)Endpoint.ProcessId);

    public string ProcessNameDisplay => Process?.FriendlyName ?? PortDisplayFormatter.NotApplicableDisplay;

    public string? ExecutablePath => Process?.ExecutablePath;

    /// <summary>
    /// Stable row identity including remote endpoint so ESTABLISHED peers do not collide.
    /// </summary>
    public string EndpointKey =>
        $"{Endpoint.Protocol}:{Endpoint.LocalAddress}:{Endpoint.Port}:{Endpoint.RemoteAddress}:{Endpoint.RemotePort}:{Endpoint.ProcessId}:{Endpoint.State}";

    private static string FormatAddress(string address)
    {
        if (address.Contains(':', StringComparison.Ordinal))
        {
            return $"[{address}]";
        }

        return address;
    }
}
