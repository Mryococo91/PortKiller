using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PortKiller.Helpers;
using PortKiller.Services;
using PortKiller.ViewModels;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace PortKiller.Views;

public sealed partial class MainPage : Page
{
    private readonly DispatcherQueueTimer _autoRefreshTimer;
    private readonly IClipboardService _clipboard;
    private readonly IAppLifecycleService _lifecycle;
    private readonly WinUiDialogService _dialogs;
    private bool _suppressLanguageChange;
    private bool _columnsReady;

    public MainPage()
    {
        var app = (App)Application.Current;
        ViewModel = app.ViewModel
            ?? throw new InvalidOperationException(AppStrings.Get("ViewModelNotInitialized"));
        _clipboard = app.ClipboardService;
        _lifecycle = app.LifecycleService;
        _dialogs = app.DialogService;

        InitializeComponent();
        PopulateLanguageBox();
        ApplySavedColumnWidths();

        _autoRefreshTimer = DispatcherQueue.CreateTimer();
        _autoRefreshTimer.Interval = TimeSpan.FromSeconds(5);
        _autoRefreshTimer.Tick += OnAutoRefreshTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        CharacterReceived += OnCharacterReceived;
    }

    public MainViewModel ViewModel { get; }

    public Visibility EmptyVisibility(bool isEmpty) =>
        isEmpty ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SelectionVisibility(bool hasSelection) =>
        hasSelection ? Visibility.Visible : Visibility.Collapsed;

    public string PortsHeader(int count) =>
        AppStrings.Format("PortsHeader", count);

    private void PopulateLanguageBox()
    {
        _suppressLanguageChange = true;
        LanguageBox.Items.Clear();
        LanguageBox.Items.Add(new ComboBoxItem
        {
            Content = AppStrings.Get("Language_System"),
            Tag = LocalizationService.SystemTag
        });

        foreach ((string tag, string nativeName) in LocalizationService.Languages)
        {
            LanguageBox.Items.Add(new ComboBoxItem
            {
                Content = nativeName,
                Tag = tag
            });
        }

        string saved = LocalizationService.GetSavedLanguageTag();
        LanguageBox.SelectedItem = LanguageBox.Items.OfType<ComboBoxItem>()
            .First(item => (string?)item.Tag == saved);
        _suppressLanguageChange = false;
    }

    private void ApplySavedColumnWidths()
    {
        UserPreferences prefs = ViewModel.Preferences;
        ColPort.Width = new GridLength(prefs.ColumnPortWidth);
        ColProtocol.Width = new GridLength(prefs.ColumnProtocolWidth);
        ColState.Width = new GridLength(prefs.ColumnStateWidth);
        ColLocalAddress.Width = new GridLength(1, GridUnitType.Star);
        ColPid.Width = new GridLength(prefs.ColumnPidWidth);
        ColProcess.Width = new GridLength(prefs.ColumnProcessWidth);
        _columnsReady = true;
    }

    private void PersistColumnWidths()
    {
        if (!_columnsReady)
        {
            return;
        }

        ViewModel.SaveColumnWidths(
            ColPort.ActualWidth > 0 ? ColPort.ActualWidth : ColPort.Width.Value,
            ColProtocol.ActualWidth > 0 ? ColProtocol.ActualWidth : ColProtocol.Width.Value,
            ColState.ActualWidth > 0 ? ColState.ActualWidth : ColState.Width.Value,
            ViewModel.Preferences.ColumnLocalAddressWidth,
            ColPid.ActualWidth > 0 ? ColPid.ActualWidth : ColPid.Width.Value,
            ColProcess.ActualWidth > 0 ? ColProcess.ActualWidth : ColProcess.Width.Value);
    }

    private void OnRowGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Grid row)
        {
            return;
        }

        if (row.ColumnDefinitions.Count < 6)
        {
            return;
        }

        row.ColumnDefinitions[0].Width = ColPort.Width;
        row.ColumnDefinitions[1].Width = ColProtocol.Width;
        row.ColumnDefinitions[2].Width = ColState.Width;
        row.ColumnDefinitions[3].Width = new GridLength(1, GridUnitType.Star);
        row.ColumnDefinitions[4].Width = ColPid.Width;
        row.ColumnDefinitions[5].Width = ColProcess.Width;
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageChange || LanguageBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        string tag = item.Tag as string ?? LocalizationService.SystemTag;
        if (tag == LocalizationService.GetSavedLanguageTag())
        {
            return;
        }

        LocalizationService.SaveLanguage(tag);
        _lifecycle.RestartCurrentProcess();
    }

    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        if (args.Character != '/' || SearchBox.FocusState != FocusState.Unfocused)
        {
            return;
        }

        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
        args.Handled = true;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _dialogs.XamlRoot = XamlRoot;
        SearchBox.Focus(FocusState.Programmatic);
        await ViewModel.RefreshCommand.ExecuteAsync(null);
        UpdateAutoRefreshTimer();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PersistColumnWidths();
        _autoRefreshTimer.Stop();
        _autoRefreshTimer.Tick -= OnAutoRefreshTick;
    }

    private async void OnAutoRefreshTick(DispatcherQueueTimer sender, object args)
    {
        if (!ViewModel.IsAutoRefreshEnabled || ViewModel.IsBusy)
        {
            return;
        }

        if (GetWindow()?.AppWindow.IsVisible == false)
        {
            return;
        }

        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel.SearchQuery != SearchBox.Text)
        {
            ViewModel.SearchQuery = SearchBox.Text;
        }
    }

    private void OnAutoRefreshToggled(object sender, RoutedEventArgs e)
    {
        UpdateAutoRefreshTimer();
    }

    private void UpdateAutoRefreshTimer()
    {
        if (ViewModel.IsAutoRefreshEnabled)
        {
            _autoRefreshTimer.Start();
        }
        else
        {
            _autoRefreshTimer.Stop();
        }
    }

    private async void OnTerminateClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.TerminateSelectedCommand.ExecuteAsync(null);
    }

    private async void OnRelaunchAsAdminClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            _lifecycle.RelaunchAsAdministrator();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync(
                AppStrings.Get("ElevationCanceledTitle"),
                AppStrings.Format("ElevationCanceledContent", ex.Message),
                AppStrings.Get("Dialog_Ok"));
        }
    }

    private void OnCopyPidClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry is null)
        {
            return;
        }

        _clipboard.SetText(ViewModel.SelectedEntry.ProcessId.ToString());
    }

    private void OnCopyPathClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.SelectedEntry?.ExecutablePath))
        {
            return;
        }

        _clipboard.SetText(ViewModel.SelectedEntry.ExecutablePath);
    }

    private async void OnRefreshAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void OnSearchAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.SelectAll();
    }

    private async void OnExportAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ViewModel.ExportCsvCommand.ExecuteAsync(null);
    }

    private async void OnDeleteAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ViewModel.TerminateSelectedCommand.ExecuteAsync(null);
    }

    private void OnEscapeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.ClearSearch();
        SearchBox.Text = string.Empty;
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void OnErrorClosed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel.DismissError();
    }

    private void OnSuccessClosed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel.DismissSuccess();
    }

    private Window? GetWindow()
    {
        return ((App)Application.Current).MainWindow;
    }
}
