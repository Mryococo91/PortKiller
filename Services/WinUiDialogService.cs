using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PortKiller.Services;

/// <summary>
/// ContentDialog host. <see cref="XamlRoot"/> must be set from the active page before use.
/// </summary>
public sealed class WinUiDialogService : IDialogService
{
    public XamlRoot? XamlRoot { get; set; }

    public async Task<bool> ConfirmAsync(string title, string content, string primaryText, string closeText)
    {
        ContentDialog dialog = CreateDialog(title, content, closeText);
        dialog.PrimaryButtonText = primaryText;
        dialog.DefaultButton = ContentDialogButton.Close;
        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowMessageAsync(string title, string content, string closeText)
    {
        ContentDialog dialog = CreateDialog(title, content, closeText);
        dialog.DefaultButton = ContentDialogButton.Close;
        await dialog.ShowAsync();
    }

    private ContentDialog CreateDialog(string title, string content, string closeText)
    {
        if (XamlRoot is null)
        {
            throw new InvalidOperationException("Dialog XamlRoot is not ready.");
        }

        return new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = closeText,
            XamlRoot = XamlRoot
        };
    }
}
