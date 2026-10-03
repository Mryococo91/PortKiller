using System.Diagnostics;
using Microsoft.UI.Xaml;
using PortKiller.Helpers;

namespace PortKiller.Services;

public sealed class AppLifecycleService : IAppLifecycleService
{
    public void RestartCurrentProcess()
    {
        string path = Environment.ProcessPath
            ?? throw new InvalidOperationException(AppStrings.Get("Error_CannotResolvePath"));

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory
        });

        Exit();
    }

    public void RelaunchAsAdministrator()
    {
        ElevationHelper.RelaunchAsAdministrator();
        Exit();
    }

    public void Exit()
    {
        Application.Current.Exit();
    }
}