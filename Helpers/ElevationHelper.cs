using System.Diagnostics;
using System.Security.Principal;

namespace PortKiller.Helpers;

public static class ElevationHelper
{
    public static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RelaunchAsAdministrator()
    {
        string path = Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to determine the Port Killer path.");

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.CurrentDirectory
        });
    }
}
