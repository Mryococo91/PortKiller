namespace PortKiller.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string content, string primaryText, string closeText);

    Task ShowMessageAsync(string title, string content, string closeText);
}
