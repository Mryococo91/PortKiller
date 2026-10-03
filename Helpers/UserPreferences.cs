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

    private static readonly string AppDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PortKiller");

    private static readonly string SettingsPath = Path.Combine(AppDataDirectory, "settings.json");

    private static readonly string LegacyLanguagePath = Path.Combine(AppDataDirectory, "language.txt");

    public bool ShowAllTcpConnections { get; set; }

    public bool IsAutoRefreshEnabled { get; set; }

    public PortSortColumn SortColumn { get; set; } = PortSortColumn.Port;

    public bool SortAscending { get; set; } = true;

    public double ColumnPortWidth { get; set; } = 72;

    public double ColumnProtocolWidth { get; set; } = 80;

    public double ColumnStateWidth { get; set; } = 120;

    public double ColumnPidWidth { get; set; } = 80;

    public double ColumnProcessWidth { get; set; } = 180;

    /// <summary>Empty = follow Windows display language; otherwise en-US / fr-FR.</summary>
    public string LanguageTag { get; set; } = LocalizationService.SystemTag;

    public bool HideElevationBanner { get; set; }

    public double WindowWidth { get; set; } = 1280;

    public double WindowHeight { get; set; } = 800;

    public static UserPreferences Load()
    {
        UserPreferences preferences;
        try
        {
            if (!File.Exists(SettingsPath))
            {
                preferences = new UserPreferences();
            }
            else
            {
                string json = File.ReadAllText(SettingsPath);
                preferences = JsonSerializer.Deserialize<UserPreferences>(json, JsonOptions) ?? new UserPreferences();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            preferences = new UserPreferences();
        }

        MigrateLegacyLanguageFile(preferences);
        NormalizeLanguageTag(preferences);
        return preferences;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppDataDirectory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Preferences are best-effort; never crash the UI for disk errors.
        }
    }

    private static void MigrateLegacyLanguageFile(UserPreferences preferences)
    {
        try
        {
            if (!File.Exists(LegacyLanguagePath))
            {
                return;
            }

            if (string.IsNullOrEmpty(preferences.LanguageTag))
            {
                string tag = File.ReadAllText(LegacyLanguagePath).Trim();
                if (tag.Equals("system", StringComparison.OrdinalIgnoreCase) || tag.Length == 0)
                {
                    preferences.LanguageTag = LocalizationService.SystemTag;
                }
                else if (LocalizationService.Languages.Any(language => language.Tag == tag))
                {
                    preferences.LanguageTag = tag;
                }
            }

            File.Delete(LegacyLanguagePath);
            preferences.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void NormalizeLanguageTag(UserPreferences preferences)
    {
        if (string.IsNullOrEmpty(preferences.LanguageTag))
        {
            preferences.LanguageTag = LocalizationService.SystemTag;
            return;
        }

        if (!LocalizationService.Languages.Any(language => language.Tag == preferences.LanguageTag))
        {
            preferences.LanguageTag = LocalizationService.SystemTag;
        }
    }
}
