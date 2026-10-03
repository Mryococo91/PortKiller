using System.ComponentModel;
using System.Diagnostics;
using PortKiller.Models;

namespace PortKiller.Services;

public sealed class ProcessTerminationService
{
    public const int IdleProcessId = 0;
    public const int SystemProcessId = 4;

    public bool CanTerminate(ProcessIdentity? identity)
    {
        if (identity is null)
        {
            return false;
        }

        if (identity.ProcessId is IdleProcessId or SystemProcessId)
        {
            return false;
        }

        if (identity.ProcessId == Environment.ProcessId)
        {
            return false;
        }

        return true;
    }

    public string? GetProtectionResourceKey(ProcessIdentity? identity)
    {
        if (identity is null)
        {
            return "Error_NoProcessSelected";
        }

        if (identity.ProcessId == IdleProcessId)
        {
            return "Error_IdleProtected";
        }

        if (identity.ProcessId == SystemProcessId)
        {
            return "Error_SystemProtected";
        }

        if (identity.ProcessId == Environment.ProcessId)
        {
            return "Error_SelfProtected";
        }

        return null;
    }

    public Task TerminateAsync(ProcessIdentity expected, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Terminate(expected), cancellationToken);
    }

    private void Terminate(ProcessIdentity expected)
    {
        string? protectionKey = GetProtectionResourceKey(expected);
        if (protectionKey is not null)
        {
            throw new ProtectedProcessException(protectionKey);
        }

        Process process;
        try
        {
            process = Process.GetProcessById(expected.ProcessId);
        }
        catch (ArgumentException)
        {
            throw new ProcessGoneException(expected.ProcessId);
        }

        using (process)
        {
            if (process.HasExited)
            {
                throw new ProcessGoneException(expected.ProcessId);
            }

            DateTime? actualStartTime = null;
            string? actualName = null;
            try
            {
                actualStartTime = process.StartTime;
                actualName = process.ProcessName;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // Compare with whatever is readable; the file name is a fallback safety net.
            }

            if (expected.StartTime is { } expectedStart && actualStartTime is { } actualStart
                && expectedStart != actualStart)
            {
                throw new ProcessIdentityMismatchException();
            }

            if (!string.IsNullOrWhiteSpace(expected.ProcessName) && actualName is not null)
            {
                string expectedName = StripExe(expected.ProcessName);
                if (!string.Equals(expectedName, actualName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ProcessIdentityMismatchException();
                }
            }

            try
            {
                process.Kill();
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
            {
                throw new ProcessAccessDeniedException(expected.ProcessId, expected.ProcessName);
            }
            catch (UnauthorizedAccessException)
            {
                throw new ProcessAccessDeniedException(expected.ProcessId, expected.ProcessName);
            }
            catch (InvalidOperationException)
            {
                throw new ProcessGoneException(expected.ProcessId);
            }
        }
    }

    private static string StripExe(string processName)
    {
        return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;
    }
}
