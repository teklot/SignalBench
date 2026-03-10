using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SignalBench.Core;
using SignalBench.Core.Data;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;
using SignalBench.Core.Session;
using SignalBench.SDK.Interfaces;
using System.Collections.ObjectModel;

namespace SignalBench.ViewModels;

public class RecentFileViewModel
{
    public int Index { get; set; }
    public string Path { get; set; } = string.Empty;
    public string DisplayName => $"{Index}. {Path}";
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFeatureService _featureService;
    public string AppTitle => string.IsNullOrEmpty(LicenseStatusText) ? $"{AppInfo.Name} v{AppInfo.Version}" : $"{AppInfo.Name} v{AppInfo.Version} - {LicenseStatusText}";

    public string LicenseStatusText => _featureService.CurrentStatus switch
    {
        LicenseStatus.Pro => "PRO Edition",
        LicenseStatus.Free => "",
        LicenseStatus.Expired => "Trial Expired",
        LicenseStatus.Invalid => "Invalid License",
        _ => ""
    };

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set {
            SetProperty(ref _statusText, value);
            if (SelectedPlot != null) SelectedPlot.StatusMessage = value;
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsPlaybackBarVisible));
            }
        }
    }

    private double _loadProgress;
    public double LoadProgress
    {
        get => _loadProgress;
        set => SetProperty(ref _loadProgress, value);
    }

    private string _loadElapsed = "";
    public string LoadElapsed
    {
        get => _loadElapsed;
        set => SetProperty(ref _loadElapsed, value);
    }

    private PacketSchema? _selectedSchema;
    public PacketSchema? SelectedSchema
    {
        get => SelectedPlot != null ? SelectedPlot.Schema : _selectedSchema;
        set {
            if (SelectedPlot != null) {
                SelectedPlot.Schema = value;
                OnPropertyChanged(nameof(SelectedSchema));
            }
            else SetProperty(ref _selectedSchema, value);
            OnPropertyChanged(nameof(SerialInfo));
        }
    }

    public bool HasData => SelectedPlot != null && (SelectedPlot.AvailableSignals.Count > 0 || !string.IsNullOrEmpty(SelectedPlot.TelemetryPath));

    public bool AnyTabHasData => Tabs.OfType<PlotViewModel>().Any(p => p.AvailableSignals.Count > 0 || !string.IsNullOrEmpty(p.TelemetryPath));

    public bool CanPlayback => HasData && (SelectedPlot == null || !SelectedPlot.IsStreaming || SelectedPlot.IsPaused);

    public bool CanAddPlot => Tabs.Count < 10;

    public bool IsPlaybackBarVisible => !IsLoading && HasData && (SelectedPlot == null || !SelectedPlot.IsStreaming || SelectedPlot.IsPaused);

    public string SerialInfo
    {
        get
        {
            if (SelectedPlot == null) return "No plot selected";
            var s = SelectedPlot.SerialSettings;
            if (string.IsNullOrEmpty(s.Port)) return "Serial not configured";
            string sb = s.StopBits switch {
                "One" => "1",
                "Two" => "2",
                "OnePointFive" => "1.5",
                _ => "0"
            };
            return $"{s.Port}: {s.BaudRate} {s.DataBits}{s.Parity[0]}{sb}";
        }
    }

    public string NetworkInfo
    {
        get
        {
            if (SelectedPlot == null) return "No plot selected";
            var n = SelectedPlot.NetworkSettings;
            if (string.IsNullOrEmpty(n.IpAddress)) return "Network not configured";
            return $"{n.Protocol}: {n.IpAddress}:{n.Port}";
        }
    }

    public bool IsSignalsPaneOpen
    {
        get => SelectedPlot?.IsSignalsPaneOpen ?? true;
        set {
            if (SelectedPlot != null) SelectedPlot.IsSignalsPaneOpen = value;
            OnPropertyChanged(nameof(IsSignalsPaneOpen));
            OnPropertyChanged(nameof(SignalsPaneColumnWidth));
        }
    }

    public GridLength SignalsPaneColumnWidth
    {
        get => SelectedPlot?.SignalsPaneColumnWidth ?? new GridLength(200);
        set {
            if (SelectedPlot != null) SelectedPlot.SignalsPaneColumnWidth = value;
            OnPropertyChanged(nameof(SignalsPaneColumnWidth));
        }
    }

    private bool _isToolbarVisible = true;
    public bool IsToolbarVisible
    {
        get => _isToolbarVisible;
        set => SetProperty(ref _isToolbarVisible, value);
    }

    public ObservableCollection<RecentFileViewModel> RecentFiles { get; } = [];
    public ObservableCollection<TabViewModelBase> Tabs { get; } = [];
    public ObservableCollection<ITabFactory> AvailableTabFactories { get; } = [];

    private TabViewModelBase? _selectedTab;
    public TabViewModelBase? SelectedTab
    {
        get => _selectedTab;
        set {
            if (_selectedTab is PlotViewModel oldPlot)
            {
                oldPlot.SourceStateChanged -= NotifySourceStateChanged;
            }

            SetProperty(ref _selectedTab, value);
            OnPropertyChanged(nameof(SelectedPlot));
            OnPropertyChanged(nameof(CanPlayback));
            NotifyPlaybackCommands();
            
            if (value is PlotViewModel plot)
            {
                plot.SourceStateChanged += NotifySourceStateChanged;
                
                _currentPlaybackIndex = plot.CurrentPlaybackIndex;
                _savedElapsedSeconds = plot.SavedElapsedSeconds;
                _fullDuration = plot.FullDuration;
                _playbackTimestamps = plot.PlaybackTimestamps;
                _playbackSignalData = plot.PlaybackSignalData;
                _totalRecords = plot.TotalRecords;
                _playbackProgressValue = _totalRecords > 1 ? (double)_currentPlaybackIndex / (_totalRecords - 1) * 100 : 0;
                
                // Sync Status
                _statusText = plot.StatusMessage;
                OnPropertyChanged(nameof(StatusText));
                
                plot.RaisePropertyChanged(nameof(PlotViewModel.ConnectionInfo));
                plot.RaisePropertyChanged(nameof(PlotViewModel.ConnectionIcon));
            }

            OnPropertyChanged(nameof(SelectedSchema));
            OnPropertyChanged(nameof(HasData));
            OnPropertyChanged(nameof(IsPlaybackBarVisible));
            OnPropertyChanged(nameof(IsStreaming));
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(IsSerialStreaming));
            OnPropertyChanged(nameof(IsNetworkStreaming));
            OnPropertyChanged(nameof(IsSerialPaused));
            OnPropertyChanged(nameof(IsNetworkPaused));
            OnPropertyChanged(nameof(TotalRecords));
            OnPropertyChanged(nameof(PlaybackProgress));
            OnPropertyChanged(nameof(CurrentPlaybackTime));
            OnPropertyChanged(nameof(FormattedPlaybackTime));
            OnPropertyChanged(nameof(IsSignalsPaneOpen));
            OnPropertyChanged(nameof(SignalsPaneColumnWidth));
            
            if (value != null)
            {
                value.RaisePropertyChanged(nameof(TabViewModelBase.ConnectionInfo));
                value.RaisePropertyChanged(nameof(TabViewModelBase.ConnectionIcon));
            }

            SyncSignalCheckboxes();
            if (value is PlotViewModel p) UpdatePlot(p);
        }
    }

    public PlotViewModel? SelectedPlot => SelectedTab as PlotViewModel;

    public bool IsStreaming
    {
        get => SelectedPlot?.IsStreaming ?? false;
        set {
            if (SelectedPlot != null) SelectedPlot.IsStreaming = value;
            OnPropertyChanged(nameof(IsStreaming));
            OnPropertyChanged(nameof(IsPlaybackBarVisible));
            OnPropertyChanged(nameof(CanAddPlot));
            OnPropertyChanged(nameof(IsSerialStreaming));
            OnPropertyChanged(nameof(IsNetworkStreaming));
        }
    }

    public bool IsPaused
    {
        get => SelectedPlot?.IsPaused ?? false;
        set {
            if (SelectedPlot != null) SelectedPlot.IsPaused = value;
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(IsSerialPaused));
            OnPropertyChanged(nameof(IsNetworkPaused));
        }
    }

    public bool IsFileSource => SelectedPlot?.SourceType == PlotSourceType.File;
    public bool IsSerialSource => SelectedPlot?.SourceType == PlotSourceType.Serial;
    public bool IsNetworkSource => SelectedPlot?.SourceType == PlotSourceType.Network;

    public bool IsSerialStreaming => IsSerialSource && IsStreaming;
    public bool IsNetworkStreaming => IsNetworkSource && IsStreaming;
    public bool IsSerialPaused => IsSerialSource && IsPaused;
    public bool IsNetworkPaused => IsNetworkSource && IsPaused;

    public void NotifySourceStateChanged()
    {
        OnPropertyChanged(nameof(IsFileSource));
        OnPropertyChanged(nameof(IsSerialSource));
        OnPropertyChanged(nameof(IsNetworkSource));
        OnPropertyChanged(nameof(IsStreaming));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsSerialStreaming));
        OnPropertyChanged(nameof(IsNetworkStreaming));
        OnPropertyChanged(nameof(IsSerialPaused));
        OnPropertyChanged(nameof(IsNetworkPaused));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(AnyTabHasData));
        OnPropertyChanged(nameof(IsPlaybackBarVisible));
        OnPropertyChanged(nameof(CanPlayback));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(CanAddPlot));
        NotifyPlaybackCommands();
        CreateDerivedSignalCommand?.NotifyCanExecuteChanged();
        SaveSessionCommand?.NotifyCanExecuteChanged();
        ExportCsvCommand?.NotifyCanExecuteChanged();
    }

    private void NotifyPlaybackCommands()
    {
        PlayPauseCommand?.NotifyCanExecuteChanged();
        StepForwardCommand?.NotifyCanExecuteChanged();
        StepBackwardCommand?.NotifyCanExecuteChanged();
        FastForwardCommand?.NotifyCanExecuteChanged();
        FastBackwardCommand?.NotifyCanExecuteChanged();
        RestartCommand?.NotifyCanExecuteChanged();
    }

    public bool IsRecording
    {
        get => SelectedPlot?.IsRecording ?? false;
        set {
            if (SelectedPlot != null) SelectedPlot.IsRecording = value;
            OnPropertyChanged(nameof(IsRecording));
        }
    }

    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ISettingsService _settingsService;
    public ISettingsService SettingsService => _settingsService;
    private readonly SessionManager _sessionManager = new();
    private readonly Core.DerivedSignals.FormulaEngine _formulaEngine = new();
    private readonly PluginLoader _pluginLoader;

    private IDataStore _dataStore => SelectedPlot?.DataStore ?? _dummyDataStore;
    private readonly IDataStore _dummyDataStore = new InMemoryDataStore();

    public IAsyncRelayCommand OpenCsvCommand { get; }
    public IAsyncRelayCommand OpenBinaryCommand { get; }
    public IAsyncRelayCommand<string> OpenRecentFileCommand { get; }
    public IAsyncRelayCommand SaveSessionCommand { get; }
    public IAsyncRelayCommand OpenSessionCommand { get; }
    public IAsyncRelayCommand CloseAllCommand { get; }
    public IAsyncRelayCommand ExportCsvCommand { get; }
    public IRelayCommand ToggleSignalsPaneCommand { get; }
    public IRelayCommand ToggleToolbarCommand { get; }
    public IAsyncRelayCommand OpenSchemaEditorCommand { get; }
    public IAsyncRelayCommand CreateDerivedSignalCommand { get; }
    public IAsyncRelayCommand<string> EditDerivedSignalCommand { get; }
    public IAsyncRelayCommand<string> RemoveDerivedSignalCommand { get; }
    public IRelayCommand<ITabFactory> AddTabCommand { get; }
    public IRelayCommand AddEmptyPlotCommand { get; }
    public IRelayCommand<TabViewModelBase> RemoveTabCommand { get; }
    public IAsyncRelayCommand OpenSettingsCommand { get; }
    public IAsyncRelayCommand OpenLicenseSettingsCommand { get; }
    public IAsyncRelayCommand OpenAboutCommand { get; }
    public IRelayCommand ExitCommand { get; }
    public IRelayCommand PlayPauseCommand { get; }
    public IRelayCommand<string> SetSpeedCommand { get; }
    public IRelayCommand<double> SeekCommand { get; }
    public IRelayCommand StepForwardCommand { get; }
    public IRelayCommand StepBackwardCommand { get; }
    public IRelayCommand FastForwardCommand { get; }
    public IRelayCommand FastBackwardCommand { get; }
    public IRelayCommand RestartCommand { get; }
    public IAsyncRelayCommand ToggleStreamingCommand { get; }
    public IAsyncRelayCommand ToggleUdpStreamingCommand { get; }
    public IAsyncRelayCommand ToggleRecordingCommand { get; }
    public IRelayCommand RefreshPortsCommand { get; }
    public IAsyncRelayCommand OpenSerialSettingsCommand { get; }
    public IAsyncRelayCommand OpenNetworkSettingsCommand { get; }

    public MainWindowViewModel() : this(null!, null!, null!, null!, null!, null!) { }

    public MainWindowViewModel(IDataStore dataStore, ILogger<MainWindowViewModel> logger, ILoggerFactory loggerFactory, ISettingsService settingsService, IFeatureService featureService, PluginLoader pluginLoader)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _settingsService = settingsService;
        _featureService = featureService;
        _pluginLoader = pluginLoader;

        // Register default factories
        AvailableTabFactories.Add(new PlotTabFactory());

        // Pull additional factories from plugins
        foreach (var plugin in _pluginLoader.Plugins)
        {
            if (plugin is ITabFactory tabFactory)
            {
                AvailableTabFactories.Add(tabFactory);
            }
        }

        OpenCsvCommand = new AsyncRelayCommand(OpenCsvAsync);
        OpenBinaryCommand = new AsyncRelayCommand(OpenBinaryAsync);
        OpenRecentFileCommand = new AsyncRelayCommand<string>(path => LoadTelemetryFileAsync(path!));
        
        SaveSessionCommand = new AsyncRelayCommand(SaveSessionAsync, () => AnyTabHasData);
        OpenSessionCommand = new AsyncRelayCommand(OpenSessionAsync);
        CloseAllCommand = new AsyncRelayCommand(CloseAllAsync);
        
        ExportCsvCommand = new AsyncRelayCommand(ExportCsv, () => HasData);
        ToggleSignalsPaneCommand = new RelayCommand(() => { IsSignalsPaneOpen = !IsSignalsPaneOpen; });
        ToggleToolbarCommand = new RelayCommand(() => { IsToolbarVisible = !IsToolbarVisible; });
        
        OpenSchemaEditorCommand = new AsyncRelayCommand(OpenSchemaEditorAsync);
        
        CreateDerivedSignalCommand = new AsyncRelayCommand(CreateDerivedSignalAsync, () => SelectedPlot != null && SelectedPlot.AvailableSignals.Count > 0);
        
        EditDerivedSignalCommand = new AsyncRelayCommand<string>(EditDerivedSignalAsync!);
        RemoveDerivedSignalCommand = new AsyncRelayCommand<string>(RemoveDerivedSignalAsync!);
        
        AddTabCommand = new RelayCommand<ITabFactory>(f => AddTab(f!), _ => Tabs.Count < 10);
        AddEmptyPlotCommand = new RelayCommand(() => AddPlot(), () => Tabs.Count < 10);
        RemoveTabCommand = new RelayCommand<TabViewModelBase>(t => RemoveTab(t!));

        OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);
        OpenLicenseSettingsCommand = new AsyncRelayCommand(OpenLicenseSettingsAsync);
        OpenAboutCommand = new AsyncRelayCommand(OpenAboutAsync);
        ExitCommand = new RelayCommand(() => {
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        });

        PlayPauseCommand = new RelayCommand(PlayPause, () => CanPlayback);
        SetSpeedCommand = new RelayCommand<string>(s => SetSpeed(s!));
        SeekCommand = new RelayCommand<double>(Seek);
        StepForwardCommand = new RelayCommand(StepForward, () => CanPlayback);
        StepBackwardCommand = new RelayCommand(StepBackward, () => CanPlayback);
        FastForwardCommand = new RelayCommand(FastForward, () => CanPlayback);
        FastBackwardCommand = new RelayCommand(FastBackward, () => CanPlayback);
        RestartCommand = new RelayCommand(Restart, () => CanPlayback);

        ToggleStreamingCommand = new AsyncRelayCommand(ToggleStreamingAsync);
        ToggleUdpStreamingCommand = new AsyncRelayCommand(ToggleUdpStreamingAsync);
        ToggleRecordingCommand = new AsyncRelayCommand(ToggleRecording, () => IsStreaming);
        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        OpenSerialSettingsCommand = new AsyncRelayCommand(OpenSerialSettingsAsync);
        OpenNetworkSettingsCommand = new AsyncRelayCommand(OpenNetworkSettingsAsync);

        RefreshPorts();

        if (!Design.IsDesignMode) { 
            RefreshRecentFiles(); 
            AddPlot("Untitled");
        }

        if (!Design.IsDesignMode && _settingsService.Current.AutoLoadLastSession && !string.IsNullOrEmpty(_settingsService.Current.LastSessionPath))
        {
            if (File.Exists(_settingsService.Current.LastSessionPath))
            {
                _ = Task.Run(async () => {
                    await Task.Delay(500); // Wait for UI to settle
                    Avalonia.Threading.Dispatcher.UIThread.Post(async () => {
                        await LoadSessionInternalAsync(_settingsService.Current.LastSessionPath);
                    });
                });
            }
        }
    }

    private void PopulateSignals(PlotViewModel plot, IEnumerable<FieldDefinition> fields, string prefix = "")
    {
        foreach (var field in fields)
        {
            string fullName = string.IsNullOrEmpty(prefix) ? field.Name : $"{prefix}/{field.Name}";

            if (field.Fields != null && field.Fields.Count > 0)
            {
                PopulateSignals(plot, field.Fields, fullName);
                continue;
            }

            if (field.Name.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) continue;

            bool shouldSelect = plot.AvailableSignals.Count < 3;
            var item = new SignalItemViewModel 
            { 
                Name = fullName, 
                IsSelected = shouldSelect, 
                ColorIndex = plot.AvailableSignals.Count,
                Unit = field.Unit,
                Lookup = field.Lookup
            };

            if (shouldSelect) plot.SelectedSignalNames.Add(fullName);
            plot.AvailableSignals.Add(item);
            plot.RegularSignals.Add(item);
        }
    }

    private void AddTab(ITabFactory factory)
    {
        var tab = factory.CreateTab();
        if (tab is TabViewModelBase tabVm)
        {
            Tabs.Add(tabVm);
            SelectedTab = tabVm;
        }
        OnPropertyChanged(nameof(CanAddPlot));
    }

    private void AddPlot(string? name = null, string? telemetryPath = null, PacketSchema? schema = null)
    {
        string plotName = !string.IsNullOrEmpty(schema?.Name) ? schema.Name : (name ?? "Untitled");

        // Reuse the SELECTED plot if we are loading data (path or schema provided)
        if (SelectedPlot != null && (telemetryPath != null || schema != null))
        {
            var p = SelectedPlot;
            p.Name = plotName;
            p.TelemetryPath = telemetryPath;
            p.Schema = schema;
            
            // Clear old state
            p.DataStore.Clear();
            p.AvailableSignals.Clear();
            p.RegularSignals.Clear();
            p.DerivedSignals.Clear();
            p.SelectedSignalNames.Clear();
            p.TotalRecords = 0;
            p.IsStreaming = false;
            p.RequestPlotClear?.Invoke();

            OnPropertyChanged(nameof(HasData));
            OnPropertyChanged(nameof(IsPlaybackBarVisible));
            OnPropertyChanged(nameof(CanAddPlot));
            return;
        }

        // If trying to add a new empty plot, but one already exists, just select it
        if (name == null && telemetryPath == null && schema == null)
        {
            var existingEmpty = Tabs.OfType<PlotViewModel>().FirstOrDefault(p => string.IsNullOrEmpty(p.TelemetryPath) && p.AvailableSignals.Count == 0 && !p.IsStreaming);
            if (existingEmpty != null)
            {
                SelectedTab = existingEmpty;
                return;
            }
        }

        var mode = _settingsService.Current.StorageMode == "Sqlite" ? StorageMode.Sqlite : StorageMode.InMemory;
        IDataStore store;
        if (mode == StorageMode.Sqlite) {
            string dbPath = Path.Combine(Path.GetTempPath(), $"signalbench_{Guid.NewGuid():N}.db");
            store = new SqliteDataStore(dbPath);
        } else {
            store = new InMemoryDataStore();
        }

        var plot = new PlotViewModel(plotName, store);
        plot.TelemetryPath = telemetryPath;
        plot.Schema = schema;
        Tabs.Add(plot);
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(IsPlaybackBarVisible));
        OnPropertyChanged(nameof(CanAddPlot));
        SelectedTab = plot;
    }

    private void RemoveTab(TabViewModelBase tab)
    {
        Tabs.Remove(tab);
        tab.Dispose();
        if (Tabs.Count == 0)
        {
            AddPlot("Untitled"); // Always keep at least one tab
        }
        else if (SelectedTab == tab)
        {
            SelectedTab = Tabs.LastOrDefault();
        }
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(IsPlaybackBarVisible));
        OnPropertyChanged(nameof(CanAddPlot));
    }

    private void SyncSignalCheckboxes()
    {
        if (SelectedPlot == null) return;
        foreach (var signal in SelectedPlot.AvailableSignals)
        {
            signal.PropertyChanged -= SignalItem_PropertyChanged;
            signal.IsSelected = SelectedPlot.IsSignalSelected(signal.Name);
            signal.PropertyChanged += SignalItem_PropertyChanged;
        }
    }

    private async Task CloseAllAsync()
    {
        if (IsStreaming) await StopStreamingAsync();
        IsPlaying = false;
        _playbackStopwatch = null;
        _playbackTimer?.Stop();
        _playbackTimer?.Dispose();
        _playbackTimer = null;
        
        foreach(var p in Tabs) p.Dispose();
        Tabs.Clear();
        AddPlot("Untitled"); // Re-initialize with an empty tab
        
        IsRecording = false;
        StatusText = "Ready";
        NotifySourceStateChanged();
        OnPropertyChanged(nameof(TotalRecords));
        OnPropertyChanged(nameof(CurrentPlaybackTime));
        OnPropertyChanged(nameof(FormattedPlaybackTime));
        OnPropertyChanged(nameof(PlaybackProgress));
    }

    private async Task ShowError(string title, string message, Exception? ex = null)
    {
        if (ex != null) _logger.LogError(ex, "{Title}: {Message}", title, message);
        else _logger.LogError("{Title}: {Message}", title, message);
        var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(title, message);
        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel != null) await box.ShowWindowDialogAsync(topLevel);
    }

    private async Task<bool> OpenSerialSettingsAsync()
    {
        try {
            if (SelectedPlot == null) return false;
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return false;
            
            var dialogVm = new SerialDialogViewModel(SelectedPlot.SerialSettings, SelectedPlot.SerialSchemaPath);
            var dialog = new SignalBench.Views.SerialDialog { DataContext = dialogVm };
            var saved = await dialog.ShowDialog<bool>(topLevel);
            
            if (saved)
            {
                dialogVm.ApplyTo(SelectedPlot.SerialSettings);
                SelectedPlot.SerialSchemaPath = dialogVm.LoadedSchemaPath;
                if (!string.IsNullOrEmpty(SelectedPlot.SerialSchemaPath))
                {
                    var yaml = await File.ReadAllTextAsync(SelectedPlot.SerialSchemaPath);
                    var schema = new SchemaLoader().Load(yaml);
                    if (schema != null)
                    {
                        SelectedPlot.Schema = schema;
                        OnPropertyChanged(nameof(SelectedSchema));
                        StatusText = $"Schema loaded: {schema.Name}";
                    }
                }
            }
            
            OnPropertyChanged(nameof(SerialInfo));
            return saved;
        } catch (Exception ex) { await ShowError("Serial Settings Error", "Failed to open serial settings.", ex); return false; }
    }

    private async Task<bool> OpenNetworkSettingsAsync()
    {
        try {
            if (SelectedPlot == null) return false;
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return false;
            
            var dialogVm = new NetworkDialogViewModel(SelectedPlot.NetworkSettings, SelectedPlot.NetworkSchemaPath);
            var dialog = new SignalBench.Views.NetworkDialog { DataContext = dialogVm };
            var saved = await dialog.ShowDialog<bool>(topLevel);
            
            if (saved)
            {
                dialogVm.ApplyTo(SelectedPlot.NetworkSettings);
                SelectedPlot.NetworkSchemaPath = dialogVm.LoadedSchemaPath;
                if (!string.IsNullOrEmpty(SelectedPlot.NetworkSchemaPath))
                {
                    var yaml = await File.ReadAllTextAsync(SelectedPlot.NetworkSchemaPath);
                    var schema = new SchemaLoader().Load(yaml);
                    if (schema != null)
                    {
                        SelectedPlot.Schema = schema;
                        OnPropertyChanged(nameof(SelectedSchema));
                        StatusText = $"Schema loaded: {schema.Name}";
                    }
                }
            }
            
            OnPropertyChanged(nameof(NetworkInfo));
            return saved;
        } catch (Exception ex) { await ShowError("Network Settings Error", "Failed to open network settings.", ex); return false; }
    }

    private async Task<bool> OpenSettingsAsync()
    {
        try {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return false;

            var settingsVm = new SettingsDialogViewModel(_settingsService, _featureService);
            var dialog = new SignalBench.Views.SettingsDialog { DataContext = settingsVm };
            var saved = await dialog.ShowDialog<bool>(topLevel);
            return saved;
        } catch (Exception ex) { await ShowError("Settings Error", "Failed to open settings.", ex); return false; }
    }

    private void SignalItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SignalItemViewModel.IsSelected))
        {
            if (sender is SignalItemViewModel signalItem && SelectedPlot != null)
            {
                if (signalItem.IsSelected) { if (!SelectedPlot.SelectedSignalNames.Contains(signalItem.Name)) SelectedPlot.SelectedSignalNames.Add(signalItem.Name); }
                else SelectedPlot.SelectedSignalNames.Remove(signalItem.Name);
                UpdatePlot(SelectedPlot);
            }
        }
    }

    private void UpdatePlot(PlotViewModel? targetPlot = null)
    {
        try {
            var plot = targetPlot ?? SelectedPlot;
            if (plot == null) return;

            int rowCount = plot.DataStore.GetRowCount();
            plot.TotalRecords = rowCount;
            
            // Sync plot playback data
            plot.PlaybackTimestamps = plot.DataStore.GetTimestamps();
            plot.PlaybackSignalData.Clear();
            foreach (var signal in plot.AvailableSignals)
                plot.PlaybackSignalData[signal.Name] = plot.DataStore.GetSignalData(signal.Name);

            if (plot == SelectedPlot)
            {
                _totalRecords = rowCount;
                _playbackTimestamps = plot.PlaybackTimestamps;
                _playbackSignalData = plot.PlaybackSignalData;
                OnPropertyChanged(nameof(TotalRecords));
            }
            
            var maxPlotPoints = 10000;
            var shouldDownsample = rowCount > maxPlotPoints;
            var timestamps = plot.DataStore.GetTimestamps(shouldDownsample ? maxPlotPoints : null);

            var plotData = new Dictionary<string, List<double>>();
            foreach (var signalName in plot.SelectedSignalNames)
                plotData[signalName] = plot.DataStore.GetSignalData(signalName, shouldDownsample ? maxPlotPoints : null);
            
            plot.RequestPlotUpdate?.Invoke(timestamps, plotData, null, null, null);
            
            // Update current values for the signals pane
            if (plot == SelectedPlot) RefreshCurrentValues();
        } catch (Exception ex) { _logger.LogError(ex, "Plot Error"); StatusText = $"Plot Error: {ex.Message}"; }
    }

    private void RefreshCurrentValues()
    {
        if (SelectedPlot == null) return;
        var rowCount = SelectedPlot.TotalRecords;
        if (rowCount == 0) return;

        var index = Math.Clamp(_currentPlaybackIndex, 0, rowCount - 1);
        
        foreach (var signal in SelectedPlot.AvailableSignals)
        {
            var data = SelectedPlot.DataStore.GetSignalData(signal.Name, index, 1);
            if (data.Count > 0)
            {
                signal.CurrentValue = data[0];
            }
        }
    }

    private async Task OpenAboutAsync() {
        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel != null) await new SignalBench.Views.AboutWindow().ShowDialog(topLevel);
    }

    private async Task OpenLicenseSettingsAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var settingsVm = new SettingsDialogViewModel(_settingsService, _featureService);
            settingsVm.SelectedTabIndex = 1; // License tab
            var dialog = new SignalBench.Views.SettingsDialog { DataContext = settingsVm };
            await dialog.ShowDialog<bool>(topLevel);
        }
        catch (Exception ex) { await ShowError("Settings Error", "Failed to open license settings.", ex); }
    }
}
