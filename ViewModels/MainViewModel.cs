using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortKiller.Helpers;
using PortKiller.Models;
using PortKiller.Services;

namespace PortKiller.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly PortSnapshotService _snapshotService;
    private readonly ProcessTerminationService _terminationService;
    private readonly IDialogService _dialogService;
    private readonly IFileExportService _fileExportService;
    private readonly UserPreferences _preferences;

    private IReadOnlyList<PortEntry> _snapshot = [];
    private CancellationTokenSource? _refreshCts;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private int _refreshGeneration;
    private bool _isBusy;
    private string? _errorMessage;
    private string _statusMessage = AppStrings.Get("Status_Ready");
    private string _searchQuery = string.Empty;
    private bool _showAllTcpConnections;
    private bool _isAutoRefreshEnabled;
    private bool _isElevationBannerOpen;
    private bool _offerRelaunchAsAdmin;
    private PortEntry? _selectedEntry;
    private string _successTitle = string.Empty;
    private string _successMessage = string.Empty;
    private PortSortColumn _sortColumn;
    private bool _sortAscending;

    public MainViewModel(
        PortSnapshotService snapshotService,
        ProcessTerminationService terminationService,
        IDialogService dialogService,
        IFileExportService fileExportService,
        UserPreferences preferences)
    {
        _snapshotService = snapshotService;
        _terminationService = terminationService;
        _dialogService = dialogService;
        _fileExportService = fileExportService;
        _preferences = preferences;

        _showAllTcpConnections = preferences.ShowAllTcpConnections;
        _isAutoRefreshEnabled = preferences.IsAutoRefreshEnabled;
        _sortColumn = preferences.SortColumn;
        _sortAscending = preferences.SortAscending;

        VisibleEntries = [];
        SelectedProcessPorts = [];
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(clearFeedback: true), () => !IsBusy);
        TerminateSelectedCommand = new AsyncRelayCommand(ConfirmAndTerminateSelectedAsync, () => CanTerminateSelected);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync, () => !IsBusy && VisibleEntries.Count > 0);
        SortByPortCommand = new RelayCommand(() => ToggleSort(PortSortColumn.Port));
        SortByProtocolCommand = new RelayCommand(() => ToggleSort(PortSortColumn.Protocol));
        SortByStateCommand = new RelayCommand(() => ToggleSort(PortSortColumn.State));
        SortByLocalAddressCommand = new RelayCommand(() => ToggleSort(PortSortColumn.LocalAddress));
        SortByPidCommand = new RelayCommand(() => ToggleSort(PortSortColumn.Pid));
        SortByProcessCommand = new RelayCommand(() => ToggleSort(PortSortColumn.Process));
        IsAdministrator = ElevationHelper.IsAdministrator();
        _isElevationBannerOpen = !IsAdministrator && !preferences.HideElevationBanner;
    }

    public ObservableCollection<PortEntry> VisibleEntries { get; }

    public ObservableCollection<PortEntry> SelectedProcessPorts { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand TerminateSelectedCommand { get; }

    public IAsyncRelayCommand ExportCsvCommand { get; }

    public IRelayCommand SortByPortCommand { get; }

    public IRelayCommand SortByProtocolCommand { get; }

    public IRelayCommand SortByStateCommand { get; }

    public IRelayCommand SortByLocalAddressCommand { get; }

    public IRelayCommand SortByPidCommand { get; }

    public IRelayCommand SortByProcessCommand { get; }

    public bool IsAdministrator { get; }

    public bool IsElevationBannerOpen
    {
        get => _isElevationBannerOpen;
        set
        {
            if (!SetProperty(ref _isElevationBannerOpen, value))
            {
                return;
            }

            // Persist dismissal so the informational banner stays hidden next launch.
            if (!value && !IsAdministrator && !_preferences.HideElevationBanner)
            {
                _preferences.HideElevationBanner = true;
                _preferences.Save();
            }
        }
    }

    public bool OfferRelaunchAsAdmin
    {
        get => _offerRelaunchAsAdmin;
        private set
        {
            if (SetProperty(ref _offerRelaunchAsAdmin, value))
            {
                OnPropertyChanged(nameof(ShowErrorAdminAction));
            }
        }
    }

    public bool ShowErrorAdminAction => HasError && OfferRelaunchAsAdmin && !IsAdministrator;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                RefreshCommand.NotifyCanExecuteChanged();
                ExportCsvCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    ClearSuccessFeedback();
                }

                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(ShowErrorAdminAction));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string SuccessTitle
    {
        get => _successTitle;
        private set => SetProperty(ref _successTitle, value);
    }

    public string SuccessMessage
    {
        get => _successMessage;
        private set
        {
            if (SetProperty(ref _successMessage, value))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    ErrorMessage = null;
                    OfferRelaunchAsAdmin = false;
                }

                OnPropertyChanged(nameof(HasSuccess));
            }
        }
    }

    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool ShowAllTcpConnections
    {
        get => _showAllTcpConnections;
        set
        {
            if (SetProperty(ref _showAllTcpConnections, value))
            {
                _preferences.ShowAllTcpConnections = value;
                _preferences.Save();
                ApplyFilter();
            }
        }
    }

    public bool IsAutoRefreshEnabled
    {
        get => _isAutoRefreshEnabled;
        set
        {
            if (SetProperty(ref _isAutoRefreshEnabled, value))
            {
                _preferences.IsAutoRefreshEnabled = value;
                _preferences.Save();
            }
        }
    }

    public PortSortColumn SortColumn
    {
        get => _sortColumn;
        private set
        {
            if (SetProperty(ref _sortColumn, value))
            {
                NotifySortHeaders();
            }
        }
    }

    public bool SortAscending
    {
        get => _sortAscending;
        private set
        {
            if (SetProperty(ref _sortAscending, value))
            {
                NotifySortHeaders();
            }
        }
    }

    public string PortSortGlyph => SortGlyph(PortSortColumn.Port);

    public string ProtocolSortGlyph => SortGlyph(PortSortColumn.Protocol);

    public string StateSortGlyph => SortGlyph(PortSortColumn.State);

    public string LocalAddressSortGlyph => SortGlyph(PortSortColumn.LocalAddress);

    public string PidSortGlyph => SortGlyph(PortSortColumn.Pid);

    public string ProcessSortGlyph => SortGlyph(PortSortColumn.Process);

    public PortEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                UpdateSelectedProcessPorts();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(CanTerminateSelected));
                OnPropertyChanged(nameof(SelectedProcessName));
                OnPropertyChanged(nameof(SelectedProcessIdDisplay));
                OnPropertyChanged(nameof(SelectedProcessPath));
                OnPropertyChanged(nameof(SelectedProcessStartTime));
                OnPropertyChanged(nameof(SelectedProcessPortCount));
                OnPropertyChanged(nameof(HasSelectedProcessPath));
                TerminateSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedEntry is not null;

    public bool IsEmpty => !IsBusy && VisibleEntries.Count == 0;

    public bool CanTerminateSelected =>
        SelectedEntry?.Process is { } identity && _terminationService.CanTerminate(identity);

    public string SelectedProcessName =>
        SelectedEntry?.ProcessNameDisplay ?? PortDisplayFormatter.NotApplicableDisplay;

    public string SelectedProcessIdDisplay =>
        SelectedEntry is null
            ? PortDisplayFormatter.NotApplicableDisplay
            : SelectedEntry.ProcessId.ToString();

    public string SelectedProcessPath =>
        string.IsNullOrWhiteSpace(SelectedEntry?.ExecutablePath)
            ? AppStrings.Get("Unavailable")
            : SelectedEntry.ExecutablePath;

    public bool HasSelectedProcessPath => !string.IsNullOrWhiteSpace(SelectedEntry?.ExecutablePath);

    public string SelectedProcessStartTime =>
        SelectedEntry?.Process?.StartTime is { } start
            ? start.ToString("yyyy-MM-dd HH:mm:ss")
            : AppStrings.Get("Unavailable");

    public int SelectedProcessPortCount => SelectedProcessPorts.Count;

    public string TerminateConfirmationBody
    {
        get
        {
            if (SelectedEntry?.Process is null)
            {
                return string.Empty;
            }

            ProcessIdentity identity = SelectedEntry.Process;
            string ports = string.Join(
                Environment.NewLine,
                SelectedProcessPorts.Select(entry =>
                {
                    string remote = string.IsNullOrEmpty(entry.RemoteEndpointDisplay)
                        ? string.Empty
                        : $"  → {entry.RemoteEndpointDisplay}";
                    return $"  {entry.Port,-6} {entry.ProtocolDisplay,-4} {entry.StateDisplay}{remote}";
                }));

            return
                AppStrings.Format("TerminateConfirm_Name", identity.FriendlyName) + Environment.NewLine +
                AppStrings.Format("TerminateConfirm_Pid", identity.ProcessId) + Environment.NewLine +
                AppStrings.Format("TerminateConfirm_Path", identity.ExecutablePath ?? AppStrings.Get("Unavailable")) +
                Environment.NewLine + Environment.NewLine +
                AppStrings.Get("TerminateConfirm_Ports") + Environment.NewLine +
                ports + Environment.NewLine + Environment.NewLine +
                AppStrings.Get("TerminateConfirm_Warning");
        }
    }

    public Task RefreshAsync() => RefreshAsync(clearFeedback: true);

    public async Task RefreshAsync(bool clearFeedback)
    {
        // Always preempt an in-flight refresh so post-terminate / F5 is never dropped.
        _refreshCts?.Cancel();
        CancellationTokenSource cts = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _refreshCts, cts);
        previous?.Dispose();

        int generation = Interlocked.Increment(ref _refreshGeneration);
        await _refreshLock.WaitAsync();
        try
        {
            if (generation != _refreshGeneration)
            {
                return;
            }

            IsBusy = true;
            if (clearFeedback)
            {
                ErrorMessage = null;
                OfferRelaunchAsAdmin = false;
                ClearSuccessFeedback();
            }

            try
            {
                string? selectedKey = SelectedEntry?.EndpointKey;
                IReadOnlyList<PortEntry> snapshot = await _snapshotService.GetSnapshotAsync(cts.Token);
                if (generation != _refreshGeneration)
                {
                    return;
                }

                _snapshot = snapshot;
                ApplyFilter();
                RestoreSelection(selectedKey);
            }
            catch (OperationCanceledException)
            {
                // A newer refresh replaced this one.
            }
            catch (Exception ex)
            {
                if (generation != _refreshGeneration)
                {
                    return;
                }

                ErrorMessage = AppStrings.Format("Error_ReadPorts", ex.Message);
                StatusMessage = AppStrings.Get("Status_RefreshFailed");
            }
            finally
            {
                if (generation == _refreshGeneration)
                {
                    IsBusy = false;
                }
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task ConfirmAndTerminateSelectedAsync()
    {
        if (!CanTerminateSelected || SelectedEntry?.Process is null)
        {
            return;
        }

        bool confirmed = await _dialogService.ConfirmAsync(
            AppStrings.Get("TerminateDialogTitle"),
            TerminateConfirmationBody,
            AppStrings.Get("TerminateDialogPrimary"),
            AppStrings.Get("TerminateDialogClose"));

        if (!confirmed)
        {
            return;
        }

        await TerminateSelectedAsync();
    }

    public async Task TerminateSelectedAsync()
    {
        if (SelectedEntry?.Process is not { } identity)
        {
            return;
        }

        try
        {
            await _terminationService.TerminateAsync(identity);
            await RefreshAsync(clearFeedback: false);
            ShowSuccess(
                AppStrings.Get("SuccessTitle_Terminated"),
                AppStrings.Format("Success_Terminated", identity.FriendlyName, identity.ProcessId));
        }
        catch (ProcessAccessDeniedException ex)
        {
            await RefreshAsync(clearFeedback: false);
            ErrorMessage = AppStrings.Format("Error_AccessDenied", ex.DisplayName);
            OfferRelaunchAsAdmin = !IsAdministrator;
        }
        catch (PortKillerException ex)
        {
            await RefreshAsync(clearFeedback: false);
            ErrorMessage = LocalizeTerminationError(ex);
        }
        catch (Exception ex)
        {
            ErrorMessage = AppStrings.Format("Error_Terminate", ex.Message);
        }
    }

    public async Task ExportCsvAsync()
    {
        if (VisibleEntries.Count == 0)
        {
            return;
        }

        string csv = PortEntryCsvExporter.Export(VisibleEntries);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string? path = await _fileExportService.SaveTextAsync(
            $"PortKiller-{stamp}",
            AppStrings.Get("Export_CsvFileType"),
            ".csv",
            csv);

        if (path is null)
        {
            return;
        }

        ShowSuccess(
            AppStrings.Get("SuccessTitle_Exported"),
            AppStrings.Format("Success_Exported", path));
    }

    public void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    public void DismissError()
    {
        ErrorMessage = null;
        OfferRelaunchAsAdmin = false;
    }

    public void DismissSuccess()
    {
        ClearSuccessFeedback();
    }

    public void SaveColumnWidths(
        double port,
        double protocol,
        double state,
        double pid,
        double process)
    {
        _preferences.ColumnPortWidth = ClampWidth(port, 48);
        _preferences.ColumnProtocolWidth = ClampWidth(protocol, 56);
        _preferences.ColumnStateWidth = ClampWidth(state, 72);
        _preferences.ColumnPidWidth = ClampWidth(pid, 48);
        _preferences.ColumnProcessWidth = ClampWidth(process, 100);
        _preferences.Save();
    }

    public UserPreferences Preferences => _preferences;

    private void ToggleSort(PortSortColumn column)
    {
        if (SortColumn == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = true;
        }

        _preferences.SortColumn = SortColumn;
        _preferences.SortAscending = SortAscending;
        _preferences.Save();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        List<PortEntry> filtered = PortEntrySorter.Sort(
                _snapshot
                    .Where(entry => PortEntryFilter.MatchesBusinessFilter(entry, ShowAllTcpConnections))
                    .Where(entry => PortEntryFilter.MatchesSearch(entry, SearchQuery)),
                SortColumn,
                SortAscending)
            .ToList();

        SyncObservableByKey(VisibleEntries, filtered, static entry => entry.EndpointKey);

        int totalBusiness = _snapshot.Count(entry =>
            PortEntryFilter.MatchesBusinessFilter(entry, ShowAllTcpConnections));

        StatusMessage = string.IsNullOrWhiteSpace(SearchQuery)
            ? AppStrings.Format("Status_Entries", VisibleEntries.Count)
            : AppStrings.Format("Status_Results", VisibleEntries.Count, totalBusiness);

        OnPropertyChanged(nameof(IsEmpty));
        ExportCsvCommand.NotifyCanExecuteChanged();
        UpdateSelectedProcessPorts();
    }

    private void UpdateSelectedProcessPorts()
    {
        if (SelectedEntry is null)
        {
            SelectedProcessPorts.Clear();
            OnPropertyChanged(nameof(SelectedProcessPortCount));
            OnPropertyChanged(nameof(TerminateConfirmationBody));
            return;
        }

        uint pid = SelectedEntry.Endpoint.ProcessId;
        List<PortEntry> ports = _snapshot
            .Where(candidate => candidate.Endpoint.ProcessId == pid)
            .ToList();

        SyncObservableByKey(SelectedProcessPorts, ports, static entry => entry.EndpointKey);

        OnPropertyChanged(nameof(SelectedProcessPortCount));
        OnPropertyChanged(nameof(TerminateConfirmationBody));
    }

    /// <summary>
    /// Updates <paramref name="target"/> to match <paramref name="source"/> order/keys
    /// with Move/Insert/Remove instead of Clear, to reduce ListView flicker on auto-refresh.
    /// </summary>
    private static void SyncObservableByKey(
        ObservableCollection<PortEntry> target,
        IReadOnlyList<PortEntry> source,
        Func<PortEntry, string> keySelector)
    {
        if (target.Count == source.Count)
        {
            bool sameKeys = true;
            for (int i = 0; i < source.Count; i++)
            {
                if (!string.Equals(keySelector(target[i]), keySelector(source[i]), StringComparison.Ordinal))
                {
                    sameKeys = false;
                    break;
                }
            }

            if (sameKeys)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    if (!ReferenceEquals(target[i], source[i]))
                    {
                        target[i] = source[i];
                    }
                }

                return;
            }
        }

        var sourceKeys = new HashSet<string>(source.Select(keySelector), StringComparer.Ordinal);
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!sourceKeys.Contains(keySelector(target[i])))
            {
                target.RemoveAt(i);
            }
        }

        for (int i = 0; i < source.Count; i++)
        {
            string key = keySelector(source[i]);
            int existing = -1;
            for (int j = i; j < target.Count; j++)
            {
                if (string.Equals(keySelector(target[j]), key, StringComparison.Ordinal))
                {
                    existing = j;
                    break;
                }
            }

            if (existing == i)
            {
                if (!ReferenceEquals(target[i], source[i]))
                {
                    target[i] = source[i];
                }
            }
            else if (existing > i)
            {
                target.Move(existing, i);
                if (!ReferenceEquals(target[i], source[i]))
                {
                    target[i] = source[i];
                }
            }
            else
            {
                target.Insert(i, source[i]);
            }
        }

        while (target.Count > source.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private void ShowSuccess(string title, string message)
    {
        SuccessTitle = title;
        SuccessMessage = message;
    }

    private void ClearSuccessFeedback()
    {
        SuccessTitle = string.Empty;
        SuccessMessage = string.Empty;
    }

    private void RestoreSelection(string? endpointKey)
    {
        if (string.IsNullOrEmpty(endpointKey))
        {
            SelectedEntry = null;
            return;
        }

        PortEntry? match = VisibleEntries.FirstOrDefault(entry => entry.EndpointKey == endpointKey)
            ?? VisibleEntries.FirstOrDefault(entry =>
                SelectedEntry is not null && entry.ProcessId == SelectedEntry.ProcessId);

        SelectedEntry = match;
    }

    private string SortGlyph(PortSortColumn column)
    {
        if (SortColumn != column)
        {
            return string.Empty;
        }

        return SortAscending ? "▲" : "▼";
    }

    private void NotifySortHeaders()
    {
        OnPropertyChanged(nameof(PortSortGlyph));
        OnPropertyChanged(nameof(ProtocolSortGlyph));
        OnPropertyChanged(nameof(StateSortGlyph));
        OnPropertyChanged(nameof(LocalAddressSortGlyph));
        OnPropertyChanged(nameof(PidSortGlyph));
        OnPropertyChanged(nameof(ProcessSortGlyph));
    }

    private static double ClampWidth(double width, double min) =>
        Math.Clamp(width, min, 800);

    private static string LocalizeTerminationError(PortKillerException ex)
    {
        return ex switch
        {
            ProcessGoneException gone => AppStrings.Format("Error_ProcessGone", gone.ProcessId),
            ProcessAccessDeniedException denied => AppStrings.Format("Error_AccessDenied", denied.DisplayName),
            ProcessIdentityMismatchException => AppStrings.Get("Error_IdentityMismatch"),
            ProcessIdentityUnverifiedException => AppStrings.Get("Error_IdentityUnverified"),
            ProtectedProcessException protectedEx => AppStrings.Get(protectedEx.ResourceKey),
            _ => AppStrings.Format("Error_Terminate", ex.Message)
        };
    }
}
