using PortKiller.Models;

namespace PortKiller.Helpers;

/// <summary>
/// Protocol / TCP state labels. Resource keys are stable for tests;
/// call <see cref="ConfigureDefault"/> once at app startup, or <see cref="Use"/> in tests.
/// </summary>
public static class PortDisplayFormatter
{
    private static Func<string, string> _default = static key => key;
    private static readonly AsyncLocal<Func<string, string>?> OverrideLocalizer = new();

    public static Func<string, string> Localize => OverrideLocalizer.Value ?? _default;

    public static void ConfigureDefault(Func<string, string> localize) =>
        _default = localize ?? throw new ArgumentNullException(nameof(localize));

    /// <summary>Scoped override (AsyncLocal) so parallel tests do not clobber each other.</summary>
    public static IDisposable Use(Func<string, string> localize)
    {
        ArgumentNullException.ThrowIfNull(localize);
        Func<string, string>? previous = OverrideLocalizer.Value;
        OverrideLocalizer.Value = localize;
        return new DelegateDisposable(() => OverrideLocalizer.Value = previous);
    }

    public static string NotApplicableDisplay => Localize("State_NotApplicable");

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
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    /// <summary>English technical token used for search regardless of UI language.</summary>
    public static string GetCanonicalProtocol(NetworkProtocol protocol) =>
        protocol switch
        {
            NetworkProtocol.Tcp => "TCP",
            NetworkProtocol.Udp => "UDP",
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null)
        };

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
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    public static string FormatProtocol(NetworkProtocol protocol) =>
        Localize(GetProtocolResourceKey(protocol));

    public static string FormatState(NetworkProtocol protocol, TcpConnectionState state) =>
        Localize(GetStateResourceKey(protocol, state));

    private sealed class DelegateDisposable(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }
}
