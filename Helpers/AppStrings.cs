using Microsoft.Windows.ApplicationModel.Resources;

namespace PortKiller.Helpers;

public static class AppStrings
{
    private static ResourceLoader? _loader;

    private static ResourceLoader Loader => _loader ??= new ResourceLoader();

    public static string Get(string key)
    {
        string value = Loader.GetString(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    public static string Format(string key, params object?[] args) =>
        string.Format(Get(key), args);
}
