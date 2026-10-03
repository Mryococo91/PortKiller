using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PortKiller.Views;

namespace PortKiller;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));

        RootFrame.Navigate(typeof(MainPage));
    }
}
