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
        var identity = new ProcessIdentity { ProcessId = pid, ProcessName = "system" };

        Assert.False(_service.CanTerminate(identity));
        Assert.Equal(resourceKey, _service.GetProtectionResourceKey(identity));
    }

    [Fact]
    public void SelfProcess_CannotTerminate()
    {
        var identity = new ProcessIdentity
        {
            ProcessId = Environment.ProcessId,
            ProcessName = "PortKiller"
        };

        Assert.False(_service.CanTerminate(identity));
        Assert.Equal("Error_SelfProtected", _service.GetProtectionResourceKey(identity));
    }

    [Fact]
    public void RegularUserProcess_CanTerminate()
    {
        var identity = new ProcessIdentity
        {
            ProcessId = 12345,
            ProcessName = "node"
        };

        Assert.True(_service.CanTerminate(identity));
        Assert.Null(_service.GetProtectionResourceKey(identity));
    }

    [Fact]
    public void NullIdentity_IsRejected()
    {
        Assert.False(_service.CanTerminate(null));
        Assert.Equal("Error_NoProcessSelected", _service.GetProtectionResourceKey(null));
    }
}
