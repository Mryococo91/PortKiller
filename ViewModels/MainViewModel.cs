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

    private IReadOnlyList<PortEntry> _snapshot = [];
    private CancellationTokenSource? _refreshCts;
    private bool _isBusy;
    private string? _errorMessage;
    private string _statusMessage = AppStrings.Get("Status_Ready");
    private string _searchQuery = string.Empty;
    private bool _showAllTcpConnections;
    private bool _isAutoRefreshEnabled;
    private bool _isElevationBannerOpen;
    private PortEntry? _selectedEntry;
    private string _successMessage = string.Empty;

    public MainViewModel(
        PortSnapshotService snapshotService,
        ProcessTerminationService terminationService,
        IDialogService dialogService)
    {
        _snapshotService = snapshotService;
        _terminationService = terminationService;
        _dialogService = dialogService;

        VisibleEntries = [];
        SelectedProcessPorts = [];
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        TerminateSelectedCommand = new AsyncRelayCommand(ConfirmAndTerminateSelectedAsync, () => CanTerminateSelected);
        IsAdministrator = ElevationHelper.IsAdministrator();
        _isElevationBannerOpen = !IsAdministrator;
    }

    public ObservableCollection<PortEntry> VisibleEntries { get; }

    public ObservableCollection<PortEntry> SelectedProcessPorts { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand TerminateSelectedCommand { get; }

    public bool IsAdministrator { get; }

    public bool IsElevationBannerOpen
    {
        get => _isElevationBannerOpen;
        set => SetProperty(ref _isElevationBannerOpen, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                RefreshCommand.NotifyCanExecuteChanged();
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
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string SuccessMessage
    {
        get => _successMessage;
        private set
        {
            if (SetProperty(ref _successMessage, value))
            {
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
                ApplyFilter();
            }
        }
    }

    public bool IsAutoRefreshEnabled
    {
        get => _isAutoRefreshEnabled;
        set => SetProperty(ref _isAutoRefreshEnabled, value);
    }

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

    public string SelectedProcessName => SelectedEntry?.ProcessNameDisplay ?? "—";

    public string SelectedProcessIdDisplay => SelectedEntry is null ? "—" : SelectedEntry.ProcessId.ToString();

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
                    $"  {entry.Port,-6} {entry.ProtocolDisplay,-4} {entry.StateDisplay}"));

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

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        CancellationToken token = _refreshCts.Token;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            string? selectedKey = SelectedEntry?.EndpointKey;
            IReadOnlyList<PortEntry> snapshot = await _snapshotService.GetSnapshotAsync(token);
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
            ErrorMessage = AppStrings.Format("Error_ReadPorts", ex.Message);
            StatusMessage = AppStrings.Get("Status_RefreshFailed");
        }
        finally
        {
            IsBusy = false;
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
            SuccessMessage = AppStrings.Format("Success_Terminated", identity.FriendlyName, identity.ProcessId);
            await RefreshAsync();
        }
        catch (PortKillerException ex)
        {
            ErrorMessage = LocalizeTerminationError(ex);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = AppStrings.Format("Error_Terminate", ex.Message);
        }
    }

    public void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    public void DismissError()
    {
        ErrorMessage = null;
    }

    public void DismissSuccess()
    {
        SuccessMessage = string.Empty;
    }

    private void ApplyFilter()
    {
        IEnumerable<PortEntry> filtered = _snapshot
            .Where(entry => PortEntryFilter.MatchesBusinessFilter(entry, ShowAllTcpConnections))
            .Where(entry => PortEntryFilter.MatchesSearch(entry, SearchQuery));

        VisibleEntries.Clear();
        foreach (PortEntry entry in filtered)
        {
            VisibleEntries.Add(entry);
        }

        int totalBusiness = _snapshot.Count(entry =>
            PortEntryFilter.MatchesBusinessFilter(entry, ShowAllTcpConnections));

        StatusMessage = string.IsNullOrWhiteSpace(SearchQuery)
            ? AppStrings.Format("Status_Entries", VisibleEntries.Count)
            : AppStrings.Format("Status_Results", VisibleEntries.Count, totalBusiness);

        OnPropertyChanged(nameof(IsEmpty));
        UpdateSelectedProcessPorts();
    }

    private void UpdateSelectedProcessPorts()
    {
        SelectedProcessPorts.Clear();
        if (SelectedEntry is null)
        {
            return;
        }

        uint pid = SelectedEntry.Endpoint.ProcessId;
        foreach (PortEntry entry in _snapshot.Where(candidate => candidate.Endpoint.ProcessId == pid))
        {
            SelectedProcessPorts.Add(entry);
        }

        OnPropertyChanged(nameof(SelectedProcessPortCount));
        OnPropertyChanged(nameof(TerminateConfirmationBody));
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

    private static string LocalizeTerminationError(PortKillerException ex)
    {
        return ex switch
        {
            ProcessGoneException gone => AppStrings.Format("Error_ProcessGone", gone.ProcessId),
            ProcessAccessDeniedException denied => AppStrings.Format("Error_AccessDenied", denied.DisplayName),
            ProcessIdentityMismatchException => AppStrings.Get("Error_IdentityMismatch"),
            ProtectedProcessException protectedEx => AppStrings.Get(protectedEx.ResourceKey),
            _ => AppStrings.Format("Error_Terminate", ex.Message)
        };
    }
}
