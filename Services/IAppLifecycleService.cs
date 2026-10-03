namespace PortKiller.Services;

public interface IAppLifecycleService
{
    void RestartCurrentProcess();

    void RelaunchAsAdministrator();

    void Exit();
}
