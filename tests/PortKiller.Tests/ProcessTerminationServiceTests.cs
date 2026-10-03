using System.Diagnostics;
using PortKiller.Models;
using PortKiller.Services;
using Xunit;

namespace PortKiller.Tests;

public sealed class ProcessTerminationServiceTests
{
    private readonly ProcessTerminationService _service = new();

    [Theory]
    [InlineData(0, "Error_IdleProtected")]
    [InlineData(4, "Error_SystemProtected")]
    public void ProtectedSystemPids_CannotTerminate(int pid, string resourceKey)
    {
        var identity = new ProcessIdentity
        {
            ProcessId = pid,
            ProcessName = "system",
            StartTime = DateTime.Now
        };

        Assert.False(_service.CanTerminate(identity));
        Assert.Equal(resourceKey, _service.GetProtectionResourceKey(identity));
    }

    [Fact]
    public void SelfProcess_CannotTerminate()
    {
        var identity = new ProcessIdentity
        {
            ProcessId = Environment.ProcessId,
            ProcessName = "PortKiller",
            StartTime = DateTime.Now
        };

        Assert.False(_service.CanTerminate(identity));
        Assert.Equal("Error_SelfProtected", _service.GetProtectionResourceKey(identity));
    }

    [Theory]
    [InlineData("csrss")]
    [InlineData("csrss.exe")]
    [InlineData("LSASS.EXE")]
    [InlineData("services")]
    [InlineData("wininit")]
    [InlineData("winlogon")]
    [InlineData("smss")]
    public void CriticalProcessNames_CannotTerminate(string name)
    {
        var identity = new ProcessIdentity
        {
            ProcessId = 12345,
            ProcessName = name,
            StartTime = DateTime.Now
        };

        Assert.False(_service.CanTerminate(identity));
        Assert.Equal("Error_CriticalProtected", _service.GetProtectionResourceKey(identity));
    }

    [Fact]
    public void RegularUserProcess_WithStartTime_CanTerminate()
    {
        var identity = new ProcessIdentity
        {
            ProcessId = 12345,
            ProcessName = "node",
            StartTime = DateTime.Now
        };

        Assert.True(_service.CanTerminate(identity));
        Assert.Null(_service.GetProtectionResourceKey(identity));
    }

    [Fact]
    public void RegularUserProcess_WithNameAndPath_CanTerminate()
    {
        var identity = new ProcessIdentity
        {
            ProcessId = 12345,
            ProcessName = "node.exe",
            ExecutablePath = @"C:\tools\node.exe"
        };

        Assert.True(_service.CanTerminate(identity));
        Assert.Null(_service.GetProtectionResourceKey(identity));
    }

    [Fact]
    public void UnverifiableIdentity_CannotTerminate()
    {
        var identity = new ProcessIdentity
        {
            ProcessId = 12345,
            ProcessName = "node"
            // No StartTime, no ExecutablePath → refuse.
        };

        Assert.False(_service.CanTerminate(identity));
        Assert.Equal("Error_IdentityUnverified", _service.GetProtectionResourceKey(identity));
    }

    [Fact]
    public void NullIdentity_IsRejected()
    {
        Assert.False(_service.CanTerminate(null));
        Assert.Equal("Error_NoProcessSelected", _service.GetProtectionResourceKey(null));
    }

    [Fact]
    public async Task Terminate_KillsProcessWithMatchingStartTime()
    {
        using Process process = StartSleepProcess();
        try
        {
            var identity = new ProcessIdentity
            {
                ProcessId = process.Id,
                ProcessName = AppendExe(process.ProcessName),
                StartTime = process.StartTime,
                ExecutablePath = TryGetPath(process)
            };

            await _service.TerminateAsync(identity);

            Assert.True(process.WaitForExit(10_000));
        }
        finally
        {
            EnsureKilled(process);
        }
    }

    [Fact]
    public async Task Terminate_RejectsStartTimeMismatch()
    {
        using Process process = StartSleepProcess();
        try
        {
            var identity = new ProcessIdentity
            {
                ProcessId = process.Id,
                ProcessName = AppendExe(process.ProcessName),
                StartTime = process.StartTime.AddSeconds(-30),
                ExecutablePath = TryGetPath(process)
            };

            await Assert.ThrowsAsync<ProcessIdentityMismatchException>(() =>
                _service.TerminateAsync(identity));

            Assert.False(process.HasExited);
        }
        finally
        {
            EnsureKilled(process);
        }
    }

    [Fact]
    public async Task Terminate_RejectsWhenIdentityCannotBeVerifiedAtKillTime()
    {
        using Process process = StartSleepProcess();
        try
        {
            // Expected side has only a name (verifiable via name+path rule requires path too).
            // CanTerminate is false; Terminate must still refuse rather than kill on PID alone.
            var identity = new ProcessIdentity
            {
                ProcessId = process.Id,
                ProcessName = AppendExe(process.ProcessName)
            };

            ProtectedProcessException ex = await Assert.ThrowsAsync<ProtectedProcessException>(() =>
                _service.TerminateAsync(identity));

            Assert.Equal("Error_IdentityUnverified", ex.ResourceKey);
            Assert.False(process.HasExited);
        }
        finally
        {
            EnsureKilled(process);
        }
    }

    private static Process StartSleepProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 60",
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start test process.");

        // Ensure StartTime is readable before building the identity.
        _ = process.StartTime;
        return process;
    }

    private static string AppendExe(string processName) =>
        processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : processName + ".exe";

    private static string? TryGetPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureKilled(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit(5_000);
            }
        }
        catch
        {
            // Best-effort cleanup for the test host.
        }
    }
}
