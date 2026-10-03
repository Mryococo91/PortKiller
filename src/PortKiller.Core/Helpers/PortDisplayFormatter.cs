using PortKiller.Models;

namespace PortKiller.Helpers;

/// <summary>
/// Protocol / TCP state labels. Resource keys are stable for tests;
/// <see cref="Localize"/> is wired to WinUI .resw at app startup.
/// </summary>
public static class PortDisplayFormatter
{
    /// <summary>Defaults to returning the resource key (useful in unit tests).</summary>
    public static Func<string, string> Localize { get; set; } = static key => key;

    public static string GetProtocolResourceKey(NetworkProtocol protocol) =>
        protocol switch
        {
            NetworkProtocol.Tcp => "Protocol_Tcp",
            NetworkProtocol.Udp => "Protocol_Udp",
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null)
        };

    public static string GetStateResourceKey(NetworkProtocol protocol, TcpConnectionState state)
    {
        if (protocol == NetworkProtocol.Udp || state == TcpConnectionState.None)
        {
            return "State_NotApplicable";
        }

        return state switch
        {
            TcpConnectionState.Closed => "State_Closed",
            TcpConnectionState.Listen => "State_Listen",
            TcpConnectionState.SynSent => "State_SynSent",
            TcpConnectionState.SynReceived => "State_SynReceived",
            TcpConnectionState.Established => "State_Established",
            TcpConnectionState.FinWait1 => "State_FinWait1",
            TcpConnectionState.FinWait2 => "State_FinWait2",
            TcpConnectionState.CloseWait => "State_CloseWait",
            TcpConnectionState.Closing => "State_Closing",
            TcpConnectionState.LastAck => "State_LastAck",
            TcpConnectionState.TimeWait => "State_TimeWait",
            TcpConnectionState.DeleteTcb => "State_DeleteTcb",
            _ => "State_NotApplicable"
        };
    }

    /// <summary>English technical token used for search regardless of UI language.</summary>
    public static string GetCanonicalProtocol(NetworkProtocol protocol) =>
        protocol == NetworkProtocol.Tcp ? "TCP" : "UDP";

    /// <summary>English technical token used for search regardless of UI language.</summary>
    public static string GetCanonicalState(NetworkProtocol protocol, TcpConnectionState state)
    {
        if (protocol == NetworkProtocol.Udp || state == TcpConnectionState.None)
        {
            return "—";
        }

        return state switch
        {
            TcpConnectionState.Closed => "CLOSED",
            TcpConnectionState.Listen => "LISTENING",
            TcpConnectionState.SynSent => "SYN_SENT",
            TcpConnectionState.SynReceived => "SYN_RECEIVED",
            TcpConnectionState.Established => "ESTABLISHED",
            TcpConnectionState.FinWait1 => "FIN_WAIT_1",
            TcpConnectionState.FinWait2 => "FIN_WAIT_2",
            TcpConnectionState.CloseWait => "CLOSE_WAIT",
            TcpConnectionState.Closing => "CLOSING",
            TcpConnectionState.LastAck => "LAST_ACK",
            TcpConnectionState.TimeWait => "TIME_WAIT",
            TcpConnectionState.DeleteTcb => "DELETE_TCB",
            _ => state.ToString().ToUpperInvariant()
        };
    }

    public static string FormatProtocol(NetworkProtocol protocol) =>
        Localize(GetProtocolResourceKey(protocol));

    public static string FormatState(NetworkProtocol protocol, TcpConnectionState state) =>
        Localize(GetStateResourceKey(protocol, state));
}
