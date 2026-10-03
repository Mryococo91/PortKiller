using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PortKiller.Services;

public sealed class FileExportService : IFileExportService
{
    private readonly Func<Window?> _windowProvider;

    public FileExportService(Func<Window?> windowProvider)
    {
        _windowProvider = windowProvider;
    }

    public async Task<string?> SaveTextAsync(
        string suggestedFileName,
        string fileTypeName,
        string fileExtension,
        string content)
    {
        Window? window = _windowProvider();
        if (window is null)
        {
            return null;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName
        };
        picker.FileTypeChoices.Add(fileTypeName, [fileExtension]);

        nint hwnd = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, hwnd);

        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return null;
        }

        await FileIO.WriteTextAsync(file, content);
        return file.Path;
    }
}
