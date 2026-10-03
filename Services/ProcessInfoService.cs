using System.ComponentModel;
using System.Diagnostics;
using PortKiller.Models;
using PortKiller.Native;

namespace PortKiller.Services;

public sealed class ProcessInfoService
{
    /// <summary>
    /// Resolves process identity. Pass a per-snapshot cache so concurrent snapshots
    /// never share mutable dictionary state.
    /// </summary>
    public ProcessIdentity Get(uint processId, Dictionary<uint, ProcessIdentity> cache)
    {
        ArgumentNullException.ThrowIfNull(cache);

        if (cache.TryGetValue(processId, out ProcessIdentity? cached))
        {
            return cached;
        }

        ProcessIdentity identity = Resolve(processId);
        cache[processId] = identity;
        return identity;
    }

    private static ProcessIdentity Resolve(uint processId)
    {
        if (processId == ProcessTerminationService.IdleProcessId)
        {
            return new ProcessIdentity
            {
                ProcessId = ProcessTerminationService.IdleProcessId,
                ProcessName = "Idle",
                ExecutablePath = null,
                StartTime = null
            };
        }

        if (processId == ProcessTerminationService.SystemProcessId)
        {
            return new ProcessIdentity
            {
                ProcessId = ProcessTerminationService.SystemProcessId,
                ProcessName = "System",
                ExecutablePath = null,
                StartTime = null
            };
        }

        int pid = unchecked((int)processId);
        string? processName = null;
        DateTime? startTime = null;
        string? path = TryGetImagePath(processId);

        try
        {
            using Process process = Process.GetProcessById(pid);
            processName = AppendExeExtension(process.ProcessName);
            try
            {
                startTime = process.StartTime;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // StartTime is not always readable without privileges.
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // The PID disappeared between the network table read and process open.
        }

        if (string.IsNullOrWhiteSpace(processName) && !string.IsNullOrWhiteSpace(path))
        {
            processName = Path.GetFileName(path);
        }

        processName ??= $"PID {pid}";

        return new ProcessIdentity
        {
            ProcessId = pid,
            ProcessName = processName,
            ExecutablePath = path,
            StartTime = startTime
        };
    }

    private static unsafe string? TryGetImagePath(uint processId)
    {
        nint handle = ProcessNative.OpenProcess(ProcessNative.ProcessQueryLimitedInformation, false, processId);
        if (handle == nint.Zero)
        {
            return null;
        }

        try
        {
            char[] buffer = new char[1024];
            if (TryQueryImageName(handle, buffer, out string? path))
            {
                return path;
            }

            buffer = new char[32768];
            return TryQueryImageName(handle, buffer, out path) ? path : null;
        }
        finally
        {
            _ = ProcessNative.CloseHandle(handle);
        }
    }

    private static unsafe bool TryQueryImageName(nint handle, char[] buffer, out string? path)
    {
        uint size = (uint)buffer.Length;
        fixed (char* pointer = buffer)
        {
            if (!ProcessNative.QueryFullProcessImageNameW(handle, 0, pointer, ref size) || size == 0)
            {
                path = null;
                return false;
            }

            path = new string(pointer, 0, (int)size);
            return true;
        }
    }

    private static string AppendExeExtension(string processName)
    {
        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return processName;
        }

        return processName + ".exe";
    }
}
