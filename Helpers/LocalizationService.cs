using System.Globalization;
using Windows.Globalization;

namespace PortKiller.Helpers;

public static class LocalizationService
{
    public const string SystemTag = "";
    public const string EnglishTag = "en-US";
    public const string FrenchTag = "fr-FR";

    private const string SystemSettingValue = "system";

    public static IReadOnlyList<(string Tag, string NativeName)> Languages { get; } =
    [
        (EnglishTag, "English"),
        (FrenchTag, "Français")
    ];

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PortKiller",
        "language.txt");

    public static string GetSavedLanguageTag()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return SystemTag;
            }

            string tag = File.ReadAllText(SettingsPath).Trim();
            if (tag.Length == 0 || tag.Equals(SystemSettingValue, StringComparison.OrdinalIgnoreCase))
            {
                return SystemTag;
            }

            if (Languages.Any(language => language.Tag == tag))
            {
                return tag;
            }
        }
        catch (IOException)
        {
        }

        return SystemTag;
    }

    public static void ApplySavedLanguage()
    {
        string tag = GetSavedLanguageTag();
        ApplicationLanguages.PrimaryLanguageOverride = tag;
        if (tag.Length == 0)
        {
            return;
        }

        var culture = new CultureInfo(tag);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static void SaveLanguage(string tag)
    {
        string directory = Path.GetDirectoryName(SettingsPath)
            ?? throw new InvalidOperationException("Unable to resolve the language settings path.");

        Directory.CreateDirectory(directory);
        File.WriteAllText(
            SettingsPath,
            string.IsNullOrEmpty(tag) ? SystemSettingValue : tag);

        ApplicationLanguages.PrimaryLanguageOverride = tag;
    }
}
