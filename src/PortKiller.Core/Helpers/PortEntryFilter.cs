using PortKiller.Models;

namespace PortKiller.Helpers;

public static class PortEntryFilter
{
    public static bool MatchesBusinessFilter(PortEntry entry, bool showAllTcpConnections)
    {
        if (entry.Endpoint.Protocol == NetworkProtocol.Udp)
        {
            return true;
        }

        if (showAllTcpConnections)
        {
            return true;
        }

        return entry.Endpoint.State == TcpConnectionState.Listen;
    }

    public static bool MatchesSearch(PortEntry entry, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        string trimmed = query.Trim();

        if (int.TryParse(trimmed, out int number))
        {
            if (entry.Port == number || entry.ProcessId == number)
            {
                return true;
            }
        }

        return Contains(entry.Port.ToString(), trimmed)
            || Contains(entry.ProcessId.ToString(), trimmed)
            || Contains(entry.ProcessNameDisplay, trimmed)
            || Contains(entry.ExecutablePath, trimmed)
            || Contains(entry.LocalAddressDisplay, trimmed)
            || Contains(entry.RemoteEndpointDisplay, trimmed)
            || Contains(entry.ProtocolDisplay, trimmed)
            || Contains(entry.StateDisplay, trimmed)
            || Contains(PortDisplayFormatter.GetCanonicalProtocol(entry.Endpoint.Protocol), trimmed)
            || Contains(PortDisplayFormatter.GetCanonicalState(entry.Endpoint.Protocol, entry.Endpoint.State), trimmed);
    }

    private static bool Contains(string? value, string query)
    {
        return !string.IsNullOrEmpty(value)
            && value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
