using System.Globalization;
using Windows.Globalization;

namespace PortKiller.Helpers;

public static class LocalizationService
{
    public const string SystemTag = "";
    public const string EnglishTag = "en-US";
    public const string FrenchTag = "fr-FR";

    public static IReadOnlyList<(string Tag, string NativeName)> Languages { get; } =
    [
        (EnglishTag, "English"),
        (FrenchTag, "Français")
    ];

    public static void ApplyLanguage(string tag)
    {
        ApplicationLanguages.PrimaryLanguageOverride = tag ?? SystemTag;
        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        var culture = new CultureInfo(tag);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static void ApplySavedLanguage(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ApplyLanguage(preferences.LanguageTag);
    }

    public static void SaveLanguage(UserPreferences preferences, string tag)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.LanguageTag = tag ?? SystemTag;
        preferences.Save();
        ApplyLanguage(preferences.LanguageTag);
    }
}
