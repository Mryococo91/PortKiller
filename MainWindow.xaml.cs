using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PortKiller.Helpers;
using PortKiller.Views;
using Windows.Graphics;

namespace PortKiller;

public sealed partial class MainWindow : Window
{
    private readonly UserPreferences _preferences;

    public MainWindow(UserPreferences preferences)
    {
        _preferences = preferences;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        ApplySavedSize();
        AppWindow.Changed += OnAppWindowChanged;

        RootFrame.Navigate(typeof(MainPage));
    }

    private void ApplySavedSize()
    {
        int width = (int)Math.Clamp(_preferences.WindowWidth, 800, 4000);
        int height = (int)Math.Clamp(_preferences.WindowHeight, 500, 3000);
        AppWindow.Resize(new SizeInt32(width, height));
    }

    private void OnAppWindowChanged(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange)
        {
            return;
        }

        Windows.Graphics.SizeInt32 size = sender.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        _preferences.WindowWidth = size.Width;
        _preferences.WindowHeight = size.Height;
        _preferences.Save();
    }
}
