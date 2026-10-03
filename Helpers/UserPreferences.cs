using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortKiller.Helpers;

public sealed class UserPreferences
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PortKiller",
        "settings.json");

    public bool ShowAllTcpConnections { get; set; }

    public bool IsAutoRefreshEnabled { get; set; }

    public PortSortColumn SortColumn { get; set; } = PortSortColumn.Port;

    public bool SortAscending { get; set; } = true;

    public double ColumnPortWidth { get; set; } = 72;

    public double ColumnProtocolWidth { get; set; } = 80;

    public double ColumnStateWidth { get; set; } = 120;

    public double ColumnLocalAddressWidth { get; set; } = 220;

    public double ColumnPidWidth { get; set; } = 80;

    public double ColumnProcessWidth { get; set; } = 180;

    public static UserPreferences Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new UserPreferences();
            }

            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<UserPreferences>(json, JsonOptions) ?? new UserPreferences();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new UserPreferences();
        }
    }

    public void Save()
    {
        string? directory = Path.GetDirectoryName(SettingsPath);
        if (directory is null)
        {
            return;
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
