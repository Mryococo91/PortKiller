namespace PortKiller.Services;

public interface IFileExportService
{
    /// <summary>
    /// Shows a save dialog and writes <paramref name="content"/> when the user confirms.
    /// Returns the saved path, or null if cancelled.
    /// </summary>
    Task<string?> SaveTextAsync(string suggestedFileName, string fileTypeName, string fileExtension, string content);
}
