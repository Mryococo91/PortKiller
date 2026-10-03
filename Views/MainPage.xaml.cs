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

    private void OnShowAllTcpChecked(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowAllTcpConnections = true;
    }

    private void OnShowAllTcpUnchecked(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowAllTcpConnections = false;
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
