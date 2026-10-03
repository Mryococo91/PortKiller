using Microsoft.UI.Xaml;
using PortKiller.Helpers;
using PortKiller.Services;
using PortKiller.ViewModels;

namespace PortKiller;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        Preferences = UserPreferences.Load();
        LocalizationService.ApplySavedLanguage(Preferences);
        PortDisplayFormatter.ConfigureDefault(AppStrings.Get);
        InitializeComponent();

        var tableReader = new TcpUdpTableReader();
        var processInfo = new ProcessInfoService();
        var snapshotService = new PortSnapshotService(tableReader, processInfo);
        var terminationService = new ProcessTerminationService();
        DialogService = new WinUiDialogService();
        ClipboardService = new ClipboardService();
        LifecycleService = new AppLifecycleService();
        FileExportService = new FileExportService(() => MainWindow);
        ViewModel = new MainViewModel(
            snapshotService,
            terminationService,
            DialogService,
            FileExportService,
            Preferences);
    }

    public UserPreferences Preferences { get; }

    public MainViewModel ViewModel { get; }

    public WinUiDialogService DialogService { get; }

    public IClipboardService ClipboardService { get; }

    public IAppLifecycleService LifecycleService { get; }

    public IFileExportService FileExportService { get; }

    public MainWindow? MainWindow => _window;

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow(Preferences);
        _window.Activate();
    }
}
