namespace PortKiller.Models;

public sealed class ProcessIdentity
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public string? ExecutablePath { get; init; }
    public DateTime? StartTime { get; init; }

    public string FriendlyName =>
        string.IsNullOrWhiteSpace(ProcessName) ? $"PID {ProcessId}" : ProcessName;
}
