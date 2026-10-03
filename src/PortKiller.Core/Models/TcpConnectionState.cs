namespace PortKiller.Models;

/// <summary>
/// Values aligned with MIB_TCP_STATE (iphlpapi). 0 = not applicable (UDP).
/// </summary>
public enum TcpConnectionState : uint
{
    None = 0,
    Closed = 1,
    Listen = 2,
    SynSent = 3,
    SynReceived = 4,
    Established = 5,
    FinWait1 = 6,
    FinWait2 = 7,
    CloseWait = 8,
    Closing = 9,
    LastAck = 10,
    TimeWait = 11,
    DeleteTcb = 12
}
