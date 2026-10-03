using System.ComponentModel;
using System.Diagnostics;
using PortKiller.Models;

namespace PortKiller.Services;

public sealed class ProcessTerminationService
{
    public const int IdleProcessId = 0;
    public const int SystemProcessId = 4;

    /// <summary>
    /// Process image names that must never be terminated (BSOD / system instability risk).
    /// Compared case-insensitively with or without a trailing ".exe".
    /// </summary>
    private static readonly HashSet<string> CriticalProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "csrss",
        "wininit",
        "winlogon",
        "services",
        "lsass",
        "smss",
        "system",
        "registry",
        "memcompression",
        "memory compression",
    };

    public bool CanTerminate(ProcessIdentity? identity)
    {
        return GetProtectionResourceKey(identity) is null;
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

        if (IsCriticalProcessName(identity.ProcessName))
        {
            return "Error_CriticalProtected";
        }

        if (!HasVerifiableExpectedIdentity(identity))
        {
            return "Error_IdentityUnverified";
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
            string? actualPath = null;
            try
            {
                actualStartTime = process.StartTime;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // StartTime is not always readable without privileges.
            }

            try
            {
                actualName = process.ProcessName;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // ProcessName can fail for some protected processes.
            }

            try
            {
                actualPath = process.MainModule?.FileName;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // MainModule often requires higher privileges than the snapshot path query.
            }

            if (actualName is not null && IsCriticalProcessName(actualName))
            {
                throw new ProtectedProcessException("Error_CriticalProtected");
            }

            EnsureIdentityMatches(expected, actualStartTime, actualName, actualPath);

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

    /// <summary>
    /// Fail-closed identity check: require matching StartTime on both sides, or
    /// matching name + path on both sides. Any readable field that disagrees is a mismatch.
    /// </summary>
    private static void EnsureIdentityMatches(
        ProcessIdentity expected,
        DateTime? actualStartTime,
        string? actualName,
        string? actualPath)
    {
        bool startTimeVerified = false;
        if (expected.StartTime is { } expectedStart && actualStartTime is { } actualStart)
        {
            if (expectedStart != actualStart)
            {
                throw new ProcessIdentityMismatchException();
            }

            startTimeVerified = true;
        }

        bool nameVerified = false;
        if (!string.IsNullOrWhiteSpace(expected.ProcessName) && actualName is not null)
        {
            if (!NamesEqual(expected.ProcessName, actualName))
            {
                throw new ProcessIdentityMismatchException();
            }

            nameVerified = true;
        }

        bool pathVerified = false;
        if (!string.IsNullOrWhiteSpace(expected.ExecutablePath) && actualPath is not null)
        {
            if (!string.Equals(expected.ExecutablePath, actualPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ProcessIdentityMismatchException();
            }

            pathVerified = true;
        }

        if (startTimeVerified)
        {
            return;
        }

        if (nameVerified && pathVerified)
        {
            return;
        }

        throw new ProcessIdentityUnverifiedException();
    }

    private static bool HasVerifiableExpectedIdentity(ProcessIdentity identity)
    {
        if (identity.StartTime is not null)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(identity.ProcessName)
            && !string.IsNullOrWhiteSpace(identity.ExecutablePath);
    }

    internal static bool IsCriticalProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        return CriticalProcessNames.Contains(StripExe(processName));
    }

    private static bool NamesEqual(string expected, string actual) =>
        string.Equals(StripExe(expected), StripExe(actual), StringComparison.OrdinalIgnoreCase);

    private static string StripExe(string processName)
    {
        return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;
    }
}
