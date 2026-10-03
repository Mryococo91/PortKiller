namespace PortKiller.Models;

public class PortKillerException : Exception
{
    public PortKillerException(string message)
        : base(message)
    {
    }

    public PortKillerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ProcessGoneException : PortKillerException
{
    public int ProcessId { get; }

    public ProcessGoneException(int processId)
        : base($"Process {processId} no longer exists.")
    {
        ProcessId = processId;
    }
}

public sealed class ProcessAccessDeniedException : PortKillerException
{
    public int ProcessId { get; }

    public string? ProcessName { get; }

    public ProcessAccessDeniedException(int processId, string? processName)
        : base(
            $"Access denied: unable to terminate {(string.IsNullOrWhiteSpace(processName) ? $"PID {processId}" : processName)}. " +
            "Relaunch Port Killer as administrator if needed.")
    {
        ProcessId = processId;
        ProcessName = processName;
    }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(ProcessName) ? $"PID {ProcessId}" : ProcessName;
}

public sealed class ProcessIdentityMismatchException : PortKillerException
{
    public ProcessIdentityMismatchException()
        : base("The process changed since it was listed (the PID may have been reused). The list will be refreshed.")
    {
    }
}

public sealed class ProcessIdentityUnverifiedException : PortKillerException
{
    public ProcessIdentityUnverifiedException()
        : base("Unable to verify the process identity (start time or name and path). Termination was refused.")
    {
    }
}

public sealed class ProtectedProcessException : PortKillerException
{
    public string ResourceKey { get; }

    public ProtectedProcessException(string resourceKey)
        : base($"Protected process: {resourceKey}")
    {
        ResourceKey = resourceKey;
    }
}
