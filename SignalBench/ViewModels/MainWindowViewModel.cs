using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using SignalBench.Core;
using SignalBench.Core.Data;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;
using SignalBench.Core.Session;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Timers;

namespace SignalBench.ViewModels;

public class RecentFileViewModel
{
    public int Index { get; set; }
    public string Path { get; set; } = string.Empty;
    public string DisplayName => $"{Index}. {Path}";
}

public class MainWindowViewModel : ViewModelBase
{
    public string AppTitle => $"{AppInfo.Name} v{AppInfo.Version}";

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set {
            this.RaiseAndSetIfChanged(ref _statusText, value);
            if (SelectedPlot != null) SelectedPlot.StatusMessage = value;
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    private double _loadProgress;
    public double LoadProgress
    {
        get => _loadProgress;
        set => this.RaiseAndSetIfChanged(ref _loadProgress, value);
    }

    private string _loadElapsed = "";
    public string LoadElapsed
    {
        get => _loadElapsed;
        set => this.RaiseAndSetIfChanged(ref _loadElapsed, value);
    }

    private PacketSchema? _selectedSchema;
    public PacketSchema? SelectedSchema
    {
        get => SelectedPlot != null ? SelectedPlot.Schema : _selectedSchema;
        set {
            if (SelectedPlot != null) {
                SelectedPlot.Schema = value;
                this.RaisePropertyChanged(nameof(SelectedSchema));
            }
            else this.RaiseAndSetIfChanged(ref _selectedSchema, value);
            this.RaisePropertyChanged(nameof(SerialInfo));
        }
    }

    public bool HasData => SelectedPlot != null && (SelectedPlot.AvailableSignals.Count > 0 || !string.IsNullOrEmpty(SelectedPlot.TelemetryPath));

    public bool CanAddPlot
    {
        get
        {
            if (Plots.Count == 0) return true;
            var lastPlot = Plots.Last();
            // A plot is "empty" if it has no telemetry path, no signals, and is not streaming
            bool isEmpty = string.IsNullOrEmpty(lastPlot.TelemetryPath) && 
                           lastPlot.AvailableSignals.Count == 0 && 
                           !lastPlot.IsStreaming;
            return !isEmpty && Plots.Count < 10;
        }
    }

    public bool IsPlaybackBarVisible => HasData && (SelectedPlot == null || !SelectedPlot.IsStreaming);

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
            this.RaisePropertyChanged(nameof(IsSignalsPaneOpen));
            this.RaisePropertyChanged(nameof(SignalsPaneColumnWidth));
        }
    }

    public GridLength SignalsPaneColumnWidth
    {
        get => SelectedPlot?.SignalsPaneColumnWidth ?? new GridLength(200);
        set {
            if (SelectedPlot != null) SelectedPlot.SignalsPaneColumnWidth = value;
            this.RaisePropertyChanged(nameof(SignalsPaneColumnWidth));
        }
    }

    private bool _isToolbarVisible = true;
    public bool IsToolbarVisible
    {
        get => _isToolbarVisible;
        set => this.RaiseAndSetIfChanged(ref _isToolbarVisible, value);
    }

    public ObservableCollection<RecentFileViewModel> RecentFiles { get; } = [];
    public ObservableCollection<PlotViewModel> Plots { get; } = [];

    private PlotViewModel? _selectedPlot;
    public PlotViewModel? SelectedPlot
    {
        get => _selectedPlot;
        set {
            this.RaiseAndSetIfChanged(ref _selectedPlot, value);
            
            if (value != null)
            {
                _currentPlaybackIndex = value.CurrentPlaybackIndex;
                _savedElapsedSeconds = value.SavedElapsedSeconds;
                _fullDuration = value.FullDuration;
                _playbackTimestamps = value.PlaybackTimestamps;
                _playbackSignalData = value.PlaybackSignalData;
                _totalRecords = value.TotalRecords;
                _playbackProgressValue = _totalRecords > 1 ? (double)_currentPlaybackIndex / (_totalRecords - 1) * 100 : 0;
                
                // Sync Status
                _statusText = value.StatusMessage;
                this.RaisePropertyChanged(nameof(StatusText));
                
                value.RaisePropertyChanged(nameof(PlotViewModel.ConnectionInfo));
                value.RaisePropertyChanged(nameof(PlotViewModel.ConnectionIcon));
            }

            this.RaisePropertyChanged(nameof(SelectedSchema));
            this.RaisePropertyChanged(nameof(HasData));
            this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
            this.RaisePropertyChanged(nameof(IsStreaming));
            this.RaisePropertyChanged(nameof(IsRecording));
            this.RaisePropertyChanged(nameof(IsSerialStreaming));
            this.RaisePropertyChanged(nameof(IsNetworkStreaming));
            this.RaisePropertyChanged(nameof(IsSerialPaused));
            this.RaisePropertyChanged(nameof(IsNetworkPaused));
            this.RaisePropertyChanged(nameof(TotalRecords));
            this.RaisePropertyChanged(nameof(PlaybackProgress));
            this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
            this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
            this.RaisePropertyChanged(nameof(IsSignalsPaneOpen));
            this.RaisePropertyChanged(nameof(SignalsPaneColumnWidth));
            
            if (value != null)
            {
                value.RaisePropertyChanged(nameof(PlotViewModel.ConnectionInfo));
                value.RaisePropertyChanged(nameof(PlotViewModel.ConnectionIcon));
            }

            SyncSignalCheckboxes();
            if (value != null) UpdatePlot(value);
        }
    }

    public bool IsStreaming
    {
        get => SelectedPlot?.IsStreaming ?? false;
        set {
            if (SelectedPlot != null) SelectedPlot.IsStreaming = value;
            this.RaisePropertyChanged(nameof(IsStreaming));
            this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
            this.RaisePropertyChanged(nameof(CanAddPlot));
            this.RaisePropertyChanged(nameof(IsSerialStreaming));
            this.RaisePropertyChanged(nameof(IsNetworkStreaming));
        }
    }

    public bool IsPaused
    {
        get => SelectedPlot?.IsPaused ?? false;
        set {
            if (SelectedPlot != null) SelectedPlot.IsPaused = value;
            this.RaisePropertyChanged(nameof(IsPaused));
            this.RaisePropertyChanged(nameof(IsSerialPaused));
            this.RaisePropertyChanged(nameof(IsNetworkPaused));
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
        this.RaisePropertyChanged(nameof(IsFileSource));
        this.RaisePropertyChanged(nameof(IsSerialSource));
        this.RaisePropertyChanged(nameof(IsNetworkSource));
        this.RaisePropertyChanged(nameof(IsStreaming));
        this.RaisePropertyChanged(nameof(IsPaused));
        this.RaisePropertyChanged(nameof(IsSerialStreaming));
        this.RaisePropertyChanged(nameof(IsNetworkStreaming));
        this.RaisePropertyChanged(nameof(IsSerialPaused));
        this.RaisePropertyChanged(nameof(IsNetworkPaused));
        this.RaisePropertyChanged(nameof(HasData));
        this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
        this.RaisePropertyChanged(nameof(IsRecording));
        this.RaisePropertyChanged(nameof(CanAddPlot));
    }

    public bool IsRecording
    {
        get => SelectedPlot?.IsRecording ?? false;
        set {
            if (SelectedPlot != null) SelectedPlot.IsRecording = value;
            this.RaisePropertyChanged(nameof(IsRecording));
        }
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set => this.RaiseAndSetIfChanged(ref _isPlaying, value);
    }

    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => this.RaiseAndSetIfChanged(ref _playbackSpeed, value);
    }

    public string[] PlaybackSpeeds { get; } = ["0.5x", "1x", "2x", "5x", "10x", "100x", "1000x"];

    private int _currentPlaybackIndex;
    public int CurrentPlaybackIndex
    {
        get => _currentPlaybackIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentPlaybackIndex, value);
            if (SelectedPlot != null) SelectedPlot.CurrentPlaybackIndex = value;
            this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
            this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
            _playbackProgressValue = TotalRecords > 1 ? (double)value / (TotalRecords - 1) * 100 : 0;
            this.RaisePropertyChanged(nameof(PlaybackProgress));
        }
    }

    private int _totalRecords;
    public int TotalRecords => _totalRecords;

    public DateTime? CurrentPlaybackTime
    {
        get
        {
            if (TotalRecords == 0 || _currentPlaybackIndex < 0 || _currentPlaybackIndex >= TotalRecords) return null;
            return _dataStore.GetTimestamp(_currentPlaybackIndex);
        }
    }

    public string FormattedPlaybackTime
    {
        get
        {
            var time = CurrentPlaybackTime;
            return time?.ToString("yyyy-MM-dd\nHH:mm:ss.fff") ?? "0000-00-00\n00:00:00.000";
        }
    }

    private double _playbackProgressValue = 0;
    public double PlaybackProgress
    {
        get => _playbackProgressValue;
        set
        {
            if (Math.Abs(_playbackProgressValue - value) < 0.1) return;
            _playbackProgressValue = value;
            this.RaisePropertyChanged();
            
            if (TotalRecords > 0)
            {
                var newIndex = (int)(value / 100.0 * (TotalRecords - 1));
                newIndex = Math.Clamp(newIndex, 0, Math.Max(0, TotalRecords - 1));
                
                _currentPlaybackIndex = newIndex;
                if (SelectedPlot != null) SelectedPlot.CurrentPlaybackIndex = newIndex;

                if (TotalRecords > 1 && _fullDuration > 0)
                {
                    _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
                }
                else
                {
                    _savedElapsedSeconds = 0;
                }

                if (SelectedPlot != null) SelectedPlot.SavedElapsedSeconds = _savedElapsedSeconds;

                if (IsPlaying && _playbackStopwatch != null)
                {
                    _playbackStopwatch.Restart();
                }

                this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
                this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
                UpdateCursorPosition();
            }
        }
    }

    private DateTime? _cursorPosition;
    public DateTime? CursorPosition
    {
        get => _cursorPosition;
        set => this.RaiseAndSetIfChanged(ref _cursorPosition, value);
    }

    private System.Timers.Timer? _playbackTimer;
    private readonly object _playbackLock = new();
    private List<DateTime> _playbackTimestamps = [];
    private Dictionary<string, List<double>> _playbackSignalData = [];
    private double _savedElapsedSeconds = 0;
    private double _fullDuration = 0;

    private IDataStore _dataStore => SelectedPlot?.DataStore ?? _dummyDataStore;
    private readonly IDataStore _dummyDataStore = new InMemoryDataStore();

    private SignalBench.Core.Ingestion.IStreamingSource? _activeSource => SelectedPlot?.ActiveSource;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ISettingsService _settingsService;
    public ISettingsService SettingsService => _settingsService;
    private readonly SessionManager _sessionManager = new();
    private readonly Core.DerivedSignals.FormulaEngine _formulaEngine = new();

    public ReactiveCommand<Unit, Unit> OpenCsvCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenBinaryCommand { get; }
    public ReactiveCommand<string, Unit> OpenRecentFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSessionCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSessionCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseAllCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportCsvCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSignalsPaneCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleToolbarCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateSchemaCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSchemaCommand { get; }
    public ReactiveCommand<Unit, Unit> EditSchemaCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateDerivedSignalCommand { get; }
    public ReactiveCommand<string, Unit> EditDerivedSignalCommand { get; }
    public ReactiveCommand<string, Unit> RemoveDerivedSignalCommand { get; }
    public ReactiveCommand<Unit, Unit> AddEmptyPlotCommand { get; }
    public ReactiveCommand<PlotViewModel, Unit> RemovePlotCommand { get; }
    public ReactiveCommand<Unit, bool> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenAboutCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<Unit, Unit> PlayPauseCommand { get; }
    public ReactiveCommand<string, Unit> SetSpeedCommand { get; }
    public ReactiveCommand<double, Unit> SeekCommand { get; }
    public ReactiveCommand<Unit, Unit> StepForwardCommand { get; }
    public ReactiveCommand<Unit, Unit> StepBackwardCommand { get; }
    public ReactiveCommand<Unit, Unit> FastForwardCommand { get; }
    public ReactiveCommand<Unit, Unit> FastBackwardCommand { get; }
    public ReactiveCommand<Unit, Unit> RestartCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleStreamingCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleUdpStreamingCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleRecordingCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshPortsCommand { get; }
    public ReactiveCommand<Unit, bool> OpenSerialSettingsCommand { get; }
    public ReactiveCommand<Unit, bool> OpenNetworkSettingsCommand { get; }

    public MainWindowViewModel() : this(null!, null!, null!, null!) { }

    public MainWindowViewModel(IDataStore dataStore, ILogger<MainWindowViewModel> logger, ILoggerFactory loggerFactory, ISettingsService settingsService)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _settingsService = settingsService;

        if (!Design.IsDesignMode) { 
            RefreshRecentFiles(); 
            AddPlot("Untitled");
        }

        OpenCsvCommand = ReactiveCommand.CreateFromTask(OpenCsvAsync);
        OpenBinaryCommand = ReactiveCommand.CreateFromTask(OpenBinaryAsync);
        OpenRecentFileCommand = ReactiveCommand.CreateFromTask<string>(path => LoadTelemetryFileAsync(path));
        
        var canExecuteSession = this.WhenAnyValue(x => x.HasData);
        SaveSessionCommand = ReactiveCommand.CreateFromTask(SaveSessionAsync, canExecuteSession);
        OpenSessionCommand = ReactiveCommand.CreateFromTask(OpenSessionAsync);
        CloseAllCommand = ReactiveCommand.CreateFromTask(CloseAllAsync, canExecuteSession);
        
        ExportCsvCommand = ReactiveCommand.CreateFromTask(ExportCsv, canExecuteSession);
        ToggleSignalsPaneCommand = ReactiveCommand.Create(() => { IsSignalsPaneOpen = !IsSignalsPaneOpen; });
        ToggleToolbarCommand = ReactiveCommand.Create(() => { IsToolbarVisible = !IsToolbarVisible; });
        
        CreateSchemaCommand = ReactiveCommand.CreateFromTask(CreateSchemaAsync);
        OpenSchemaCommand = ReactiveCommand.CreateFromTask(OpenSchemaAsync);
        
        var canEditSchema = this.WhenAnyValue(x => x.SelectedSchema, (PacketSchema? s) => s != null);
        EditSchemaCommand = ReactiveCommand.CreateFromTask(EditSchemaAsync, canEditSchema);
        
        var canCreateDerived = this.WhenAnyValue(x => x.SelectedPlot, p => p != null && p.AvailableSignals.Count > 0);
        CreateDerivedSignalCommand = ReactiveCommand.CreateFromTask(CreateDerivedSignalAsync, canCreateDerived);
        
        EditDerivedSignalCommand = ReactiveCommand.CreateFromTask<string>(EditDerivedSignalAsync);
        RemoveDerivedSignalCommand = ReactiveCommand.CreateFromTask<string>(RemoveDerivedSignalAsync);
        
        AddEmptyPlotCommand = ReactiveCommand.Create(() => AddPlot());
        RemovePlotCommand = ReactiveCommand.Create<PlotViewModel>(RemovePlot);

        OpenSettingsCommand = ReactiveCommand.CreateFromTask(OpenSettingsAsync);
        OpenAboutCommand = ReactiveCommand.CreateFromTask(OpenAboutAsync);
        ExitCommand = ReactiveCommand.Create(() => {
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        });

        var canPlay = this.WhenAnyValue(x => x.HasData);
        PlayPauseCommand = ReactiveCommand.Create(PlayPause, canPlay);
        SetSpeedCommand = ReactiveCommand.Create<string>(SetSpeed);
        SeekCommand = ReactiveCommand.Create<double>(Seek);
        StepForwardCommand = ReactiveCommand.Create(StepForward, canPlay);
        StepBackwardCommand = ReactiveCommand.Create(StepBackward, canPlay);
        FastForwardCommand = ReactiveCommand.Create(FastForward, canPlay);
        FastBackwardCommand = ReactiveCommand.Create(FastBackward, canPlay);
        RestartCommand = ReactiveCommand.Create(Restart, canPlay);

        ToggleStreamingCommand = ReactiveCommand.CreateFromTask(ToggleStreamingAsync);
        ToggleUdpStreamingCommand = ReactiveCommand.CreateFromTask(ToggleUdpStreamingAsync);
        ToggleRecordingCommand = ReactiveCommand.CreateFromTask(ToggleRecording, this.WhenAnyValue(x => x.IsStreaming));
        RefreshPortsCommand = ReactiveCommand.Create(RefreshPorts);
        OpenSerialSettingsCommand = ReactiveCommand.CreateFromTask(OpenSerialSettingsAsync);
        OpenNetworkSettingsCommand = ReactiveCommand.CreateFromTask(OpenNetworkSettingsAsync);

        RefreshPorts();

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

            this.RaisePropertyChanged(nameof(HasData));
            this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
            this.RaisePropertyChanged(nameof(CanAddPlot));
            return;
        }

        // If trying to add a new empty plot, but one already exists, just select it
        if (name == null && telemetryPath == null && schema == null)
        {
            var existingEmpty = Plots.FirstOrDefault(p => string.IsNullOrEmpty(p.TelemetryPath) && p.AvailableSignals.Count == 0 && !p.IsStreaming);
            if (existingEmpty != null)
            {
                SelectedPlot = existingEmpty;
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
        Plots.Add(plot);
        this.RaisePropertyChanged(nameof(HasData));
        this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
        this.RaisePropertyChanged(nameof(CanAddPlot));
        SelectedPlot = plot;
    }

    private void RemovePlot(PlotViewModel plot)
    {
        Plots.Remove(plot);
        plot.Dispose();
        if (Plots.Count == 0)
        {
            AddPlot("Untitled"); // Always keep at least one tab
        }
        else if (SelectedPlot == plot)
        {
            SelectedPlot = Plots.LastOrDefault();
        }
        this.RaisePropertyChanged(nameof(HasData));
        this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
        this.RaisePropertyChanged(nameof(CanAddPlot));
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

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        int i = 1;
        foreach (var path in _settingsService.Current.RecentFiles)
            RecentFiles.Add(new RecentFileViewModel { Index = i++, Path = path });
    }

    private void AddToRecentFiles(string path)
    {
        var list = _settingsService.Current.RecentFiles;
        if (list.Contains(path)) list.Remove(path);
        list.Insert(0, path);
        if (list.Count > _settingsService.Current.MaxRecentFiles) list.RemoveAt(list.Count - 1);
        _settingsService.Save();
        RefreshRecentFiles();
    }

    private async Task CloseAllAsync()
    {
        if (IsStreaming) await StopStreamingAsync();
        IsPlaying = false;
        _playbackStopwatch = null;
        _playbackTimer?.Stop();
        _playbackTimer?.Dispose();
        _playbackTimer = null;
        
        foreach(var p in Plots) p.Dispose();
        Plots.Clear();
        AddPlot("Untitled"); // Re-initialize with an empty tab
        
        IsRecording = false;
        StatusText = "Ready";
        this.RaisePropertyChanged(nameof(HasData));
        this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
        this.RaisePropertyChanged(nameof(TotalRecords));
        this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
        this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        this.RaisePropertyChanged(nameof(PlaybackProgress));
        this.RaisePropertyChanged(nameof(IsStreaming));
        this.RaisePropertyChanged(nameof(IsRecording));
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
                        schema.Type = SchemaType.Streaming;
                        SelectedPlot.Schema = schema;
                        this.RaisePropertyChanged(nameof(SelectedSchema));
                        StatusText = $"Schema loaded: {schema.Name}";
                    }
                }
            }
            
            this.RaisePropertyChanged(nameof(SerialInfo));
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
                        schema.Type = SchemaType.Streaming;
                        SelectedPlot.Schema = schema;
                        this.RaisePropertyChanged(nameof(SelectedSchema));
                        StatusText = $"Schema loaded: {schema.Name}";
                    }
                }
            }
            
            this.RaisePropertyChanged(nameof(NetworkInfo));
            return saved;
        } catch (Exception ex) { await ShowError("Network Settings Error", "Failed to open network settings.", ex); return false; }
    }

    private async Task<bool> OpenSettingsAsync()
    {
        try {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return false;
            var settingsVm = new SettingsViewModel(_settingsService);
            var dialog = new SignalBench.Views.SettingsWindow { DataContext = settingsVm };
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
            if (plot == SelectedPlot)
            {
                _totalRecords = rowCount;
                this.RaisePropertyChanged(nameof(TotalRecords));
            }
            
            var maxPlotPoints = 10000;
            var shouldDownsample = rowCount > maxPlotPoints;
            var timestamps = plot.DataStore.GetTimestamps(shouldDownsample ? maxPlotPoints : null);

            var plotData = new Dictionary<string, List<double>>();
            foreach (var signalName in plot.SelectedSignalNames)
                plotData[signalName] = plot.DataStore.GetSignalData(signalName, shouldDownsample ? maxPlotPoints : null);
            
            plot.RequestPlotUpdate?.Invoke(timestamps, plotData, null, null, null);
        } catch (Exception ex) { _logger.LogError(ex, "Plot Error"); StatusText = $"Plot Error: {ex.Message}"; }
    }

    private void PlayPause() { if (IsPlaying) StopPlayback(); else StartPlayback(); }

    private void StartPlayback()
    {
        if (TotalRecords == 0 || SelectedPlot == null) return;
        lock (_playbackLock) {
            if (TotalRecords > 1) {
                var firstTs = _dataStore.GetTimestamp(0);
                var lastTs = _dataStore.GetTimestamp(TotalRecords - 1);
                _fullDuration = (lastTs - firstTs).TotalSeconds;
                if (_fullDuration < 0.1) _fullDuration = TotalRecords - 1;
                _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            } else { _fullDuration = 0; _savedElapsedSeconds = 0; }

            if (_currentPlaybackIndex >= TotalRecords - 1) {
                _currentPlaybackIndex = 0; _savedElapsedSeconds = 0; _playbackProgressValue = 0;
                this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
                this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
                this.RaisePropertyChanged(nameof(PlaybackProgress));
            }

            if (_playbackTimestamps.Count == 0) {
                var maxPlaybackPoints = 10000;
                _playbackTimestamps = _dataStore.GetTimestamps(maxPlaybackPoints);
                _playbackSignalData = [];
                foreach (var signalName in SelectedPlot.SelectedSignalNames) {
                    var data = _dataStore.GetSignalData(signalName, maxPlaybackPoints);
                    if (data.Count == _playbackTimestamps.Count) _playbackSignalData[signalName] = data;
                }
            }

            IsPlaying = true;
            _playbackStopwatch = System.Diagnostics.Stopwatch.StartNew();
            _playbackTimer?.Stop(); _playbackTimer?.Dispose();
            _playbackTimer = new System.Timers.Timer(100);
            _playbackTimer.Elapsed += PlaybackTimer_Elapsed;
            _playbackTimer.Start();
        }
        UpdatePlaybackView();
    }

    private void StopPlayback()
    {
        lock (_playbackLock) {
            IsPlaying = false;
            if (_playbackStopwatch != null) {
                _savedElapsedSeconds += _playbackStopwatch.Elapsed.TotalSeconds * PlaybackSpeed;
                _playbackStopwatch.Stop();
            }
            _playbackStopwatch = null;
            _playbackTimer?.Stop(); _playbackTimer?.Dispose(); _playbackTimer = null;
        }
    }

    private System.Diagnostics.Stopwatch? _playbackStopwatch;

    private void PlaybackTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        lock (_playbackLock) {
            if (!IsPlaying || _playbackStopwatch == null || TotalRecords <= 1 || _fullDuration <= 0) { StopPlayback(); return; }
            var timestamps = _playbackTimestamps;
            if (timestamps.Count == 0 || _currentPlaybackIndex >= TotalRecords - 1) {
                _currentPlaybackIndex = TotalRecords - 1; _playbackTimestamps = []; _playbackSignalData = []; _savedElapsedSeconds = 0; _fullDuration = 0;
                StopPlayback(); _playbackProgressValue = 100;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
                    this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                    this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
                    this.RaisePropertyChanged(nameof(PlaybackProgress));
                    UpdatePlaybackView();
                });
                return;
            }

            var elapsedSinceLastCommit = _playbackStopwatch.Elapsed.TotalSeconds;
            var targetSec = _savedElapsedSeconds + (elapsedSinceLastCommit * PlaybackSpeed);
            var progress = Math.Min(targetSec / _fullDuration, 1.0);
            var newFullIndex = (int)(progress * (TotalRecords - 1));
            newFullIndex = Math.Clamp(newFullIndex, 0, TotalRecords - 1);
            
            if (newFullIndex == _currentPlaybackIndex) return;
            _currentPlaybackIndex = newFullIndex;
            _playbackProgressValue = TotalRecords > 1 ? (double)newFullIndex / (TotalRecords - 1) * 100 : 0;
            _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            _playbackStopwatch.Restart();

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
                this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
                this.RaisePropertyChanged(nameof(PlaybackProgress));
                UpdateCursorPosition();
            });
            
            if (_currentPlaybackIndex >= TotalRecords - 1) {
                StopPlayback();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
                    this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                    this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
                    this.RaisePropertyChanged(nameof(PlaybackProgress));
                    UpdatePlaybackView();
                });
            }
        }
    }

    private void UpdateCursorPosition()
    {
        if (TotalRecords == 0 || SelectedPlot == null) return;
        DateTime currentTime;
        if (_playbackTimestamps.Count > 0) {
            int mappedIndex = (int)((double)_currentPlaybackIndex / TotalRecords * _playbackTimestamps.Count);
            mappedIndex = Math.Clamp(mappedIndex, 0, _playbackTimestamps.Count - 1);
            currentTime = _playbackTimestamps[mappedIndex];
        } else { currentTime = _dataStore.GetTimestamp(_currentPlaybackIndex); }

        CursorPosition = currentTime;
        SelectedPlot.RequestCursorUpdate?.Invoke(currentTime);
    }

    private void UpdatePlaybackView()
    {
        try {
            if (SelectedPlot == null) return;
            if (_playbackTimestamps.Count == 0) {
                var maxPoints = 10000;
                _playbackTimestamps = _dataStore.GetTimestamps(maxPoints);
                _playbackSignalData = [];
                foreach (var signalName in SelectedPlot.SelectedSignalNames) {
                    var data = _dataStore.GetSignalData(signalName, maxPoints);
                    if (data.Count == _playbackTimestamps.Count) _playbackSignalData[signalName] = data;
                }
            }
            
            if (_playbackTimestamps.Count == 0) { StopPlayback(); return; }
            int mappedIndex = (int)((double)_currentPlaybackIndex / TotalRecords * _playbackTimestamps.Count);
            mappedIndex = Math.Clamp(mappedIndex, 0, _playbackTimestamps.Count - 1);
            var currentTime = _playbackTimestamps[mappedIndex];
            CursorPosition = currentTime;

            var plotData = new Dictionary<string, List<double>>();
            foreach (var signalName in SelectedPlot.SelectedSignalNames) {
                if (_playbackSignalData.TryGetValue(signalName, out var data)) plotData[signalName] = data;
            }
            SelectedPlot.RequestPlotUpdate?.Invoke(_playbackTimestamps, plotData, currentTime, null, null);
        } catch (Exception ex) { _logger.LogError(ex, "Playback Error"); StopPlayback(); }
    }

    private void SetSpeed(string speedStr)
    {
        var newSpeed = double.Parse(speedStr.Replace("x", ""));
        lock (_playbackLock) {
            if (IsPlaying && _playbackStopwatch != null) {
                _savedElapsedSeconds += _playbackStopwatch.Elapsed.TotalSeconds * PlaybackSpeed;
                _playbackStopwatch.Restart();
            }
            PlaybackSpeed = newSpeed;
            this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
            this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        }
    }

    private void RefreshPorts() { }

    private async Task ToggleStreamingAsync()
    {
        if (SelectedPlot?.SourceType == PlotSourceType.Serial)
        {
            if (IsSerialPaused)
            {
                await ResumeSerialAsync();
            }
            else if (IsSerialStreaming)
            {
                await PauseSerialAsync();
            }
            else
            {
                await ConfigureAndStartSerialAsync();
            }
        }
        else
        {
            await ConfigureAndStartSerialAsync();
        }
    }

    private async Task ToggleUdpStreamingAsync()
    {
        if (SelectedPlot?.SourceType == PlotSourceType.Network)
        {
            if (IsNetworkPaused)
            {
                await ResumeNetworkAsync();
            }
            else if (IsNetworkStreaming)
            {
                await PauseNetworkAsync();
            }
            else
            {
                await ConfigureAndStartNetworkAsync();
            }
        }
        else
        {
            await ConfigureAndStartNetworkAsync();
        }
    }

    private async Task ConfigureAndStartSerialAsync()
    {
        if (!await OpenSerialSettingsAsync()) return;
        await StartStreamingAsync();
    }

    private async Task ConfigureAndStartNetworkAsync()
    {
        if (!await OpenNetworkSettingsAsync()) return;
        await StartNetworkStreamingAsync();
    }

    private async Task PauseSerialAsync()
    {
        if (SelectedPlot?.ActiveSource != null)
        {
            await Task.Run(() => SelectedPlot.ActiveSource.Stop());
            SelectedPlot.IsStreaming = false;
            SelectedPlot.IsPaused = true;
            StatusText = "Serial stream paused.";
            NotifySourceStateChanged();
        }
    }

    private async Task ResumeSerialAsync()
    {
        if (SelectedPlot?.ActiveSource != null && IsSerialPaused)
        {
            await Task.Run(() => SelectedPlot.ActiveSource.Start());
            SelectedPlot.IsStreaming = true;
            SelectedPlot.IsPaused = false;
            StatusText = "Serial stream resumed.";
            NotifySourceStateChanged();
        }
    }

    private async Task PauseNetworkAsync()
    {
        if (SelectedPlot?.ActiveSource != null)
        {
            await Task.Run(() => SelectedPlot.ActiveSource.Stop());
            SelectedPlot.IsStreaming = false;
            SelectedPlot.IsPaused = true;
            StatusText = "Network stream paused.";
            NotifySourceStateChanged();
        }
    }

    private async Task ResumeNetworkAsync()
    {
        if (SelectedPlot?.ActiveSource != null && IsNetworkPaused)
        {
            await Task.Run(() => SelectedPlot.ActiveSource.Start());
            SelectedPlot.IsStreaming = true;
            SelectedPlot.IsPaused = false;
            StatusText = "Network stream resumed.";
            NotifySourceStateChanged();
        }
    }

    private async Task StartNetworkStreamingAsync()
    {
        if (SelectedPlot == null) return;
        var settings = SelectedPlot.NetworkSettings;
        var schema = SelectedPlot.Schema;
        
        if (string.IsNullOrEmpty(settings.IpAddress) || settings.Port <= 0 || schema == null)
        {
            await ShowError("Configuration Error", "Please configure Network settings."); return;
        }

        try
        {
            // Stop any existing stream for THIS plot
            if (SelectedPlot.ActiveSource != null)
            {
                var s = SelectedPlot.ActiveSource; SelectedPlot.ActiveSource = null;
                await Task.Run(() => s.Stop());
            }

            var protocol = settings.Protocol == "UDP" ? SignalBench.Core.Ingestion.NetworkProtocol.Udp : SignalBench.Core.Ingestion.NetworkProtocol.Tcp;
            var plotName = !string.IsNullOrEmpty(schema.Name) ? schema.Name : (protocol == SignalBench.Core.Ingestion.NetworkProtocol.Udp 
                ? $"UDP:{settings.Port}" 
                : $"TCP:{settings.IpAddress}:{settings.Port}");
            
            SelectedPlot.Name = plotName;
            var targetPlot = SelectedPlot;
            var targetStore = targetPlot.DataStore;
            
            // Clear old state
            targetStore.Clear();
            targetPlot.TotalRecords = 0;
            targetPlot.RequestPlotClear?.Invoke();
            _totalRecords = 0;
            targetPlot.TelemetryPath = null;
            _playbackTimestamps = [];
            _playbackSignalData = [];
            this.RaisePropertyChanged(nameof(TotalRecords));

            // CRITICAL: Clear old signals so new ones from schema can be added
            targetPlot.AvailableSignals.Clear();
            targetPlot.RegularSignals.Clear();
            targetPlot.SelectedSignalNames.Clear();
            targetPlot.DerivedSignals.Clear();

            targetPlot.IsStreaming = true;
            targetPlot.IsPaused = false;
            targetPlot.SourceType = PlotSourceType.Network;
            targetStore.InitializeSchema(schema);

            // Populate signals IMMEDIATELY before starting the source
            foreach (var field in schema.Fields)
            {
                if (field.Name.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) continue;
                
                // Only select the first 3 signals
                bool shouldSelect = targetPlot.AvailableSignals.Count < 2;
                var item = new SignalItemViewModel { Name = field.Name, IsSelected = shouldSelect, ColorIndex = targetPlot.AvailableSignals.Count };
                if (shouldSelect) targetPlot.SelectedSignalNames.Add(field.Name);
                
                targetPlot.AvailableSignals.Add(item);
                targetPlot.RegularSignals.Add(item);
            }

            if (targetPlot == SelectedPlot)
            {
                SyncSignalCheckboxes();
                this.RaisePropertyChanged(nameof(HasData));
                this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
            }

            var source = new SignalBench.Core.Ingestion.NetworkTelemetrySource(settings.IpAddress, settings.Port, schema, protocol);
            SelectedPlot.ActiveSource = source;
            source.PacketReceived += (p) => HandleLivePacket(targetPlot, p);
            source.ErrorReceived += msg =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => { StatusText = $"Network Error: {msg}"; });
            };

            await Task.Run(() => source.Start());
            NotifySourceStateChanged();
            
            // Force status bar info refresh
            if (SelectedPlot != null)
            {
                SelectedPlot.RaisePropertyChanged(nameof(PlotViewModel.ConnectionInfo));
                SelectedPlot.RaisePropertyChanged(nameof(PlotViewModel.ConnectionIcon));
            }

            StatusText = protocol == SignalBench.Core.Ingestion.NetworkProtocol.Udp
                ? $"Listening on UDP port {settings.Port}..." 
                : $"Connected to TCP {settings.IpAddress}:{settings.Port}...";
        }
        catch (Exception ex)
        {
            await ShowError("Connection Error", $"Failed to start network stream: {ex.Message}");
        }
    }

    private async Task StartStreamingAsync()
    {
        if (SelectedPlot == null) return;
        var settings = SelectedPlot.SerialSettings;
        var schema = SelectedPlot.Schema;

        if (string.IsNullOrEmpty(settings.Port) || schema == null) {
            await ShowError("Configuration Error", "Please configure Serial settings."); return;
        }

        try {
            // Stop any existing stream for THIS plot
            if (SelectedPlot.ActiveSource != null)
            {
                var s = SelectedPlot.ActiveSource; SelectedPlot.ActiveSource = null;
                await Task.Run(() => s.Stop());
            }

            var plotName = !string.IsNullOrEmpty(schema.Name) ? schema.Name : $"{settings.Port}";
            SelectedPlot.Name = plotName;
            var targetPlot = SelectedPlot;
            var targetStore = targetPlot.DataStore;

            // Clear old state
            targetStore.Clear();
            targetPlot.TotalRecords = 0;
            targetPlot.RequestPlotClear?.Invoke();
            _totalRecords = 0;
            targetPlot.TelemetryPath = null;
            _playbackTimestamps = [];
            _playbackSignalData = [];
            this.RaisePropertyChanged(nameof(TotalRecords));

            // CRITICAL: Clear old signals so new ones from schema can be added
            targetPlot.AvailableSignals.Clear();
            targetPlot.RegularSignals.Clear();
            targetPlot.SelectedSignalNames.Clear();
            targetPlot.DerivedSignals.Clear();

            SelectedPlot.IsStreaming = true;
            SelectedPlot.IsPaused = false;
            SelectedPlot.SourceType = PlotSourceType.Serial;
            targetStore.InitializeSchema(schema);

            // Populate signals IMMEDIATELY before starting the source
            foreach (var field in schema.Fields)
            {
                if (field.Name.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) continue;
                
                // Only select the first 3 signals
                bool shouldSelect = targetPlot.AvailableSignals.Count < 2;
                var item = new SignalItemViewModel { Name = field.Name, IsSelected = shouldSelect, ColorIndex = targetPlot.AvailableSignals.Count };
                if (shouldSelect) targetPlot.SelectedSignalNames.Add(field.Name);
                
                targetPlot.AvailableSignals.Add(item);
                targetPlot.RegularSignals.Add(item);
            }

            if (targetPlot == SelectedPlot)
            {
                SyncSignalCheckboxes();
                this.RaisePropertyChanged(nameof(HasData));
                this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
            }

            var parity = Enum.Parse<System.IO.Ports.Parity>(settings.Parity);
            var stopBits = Enum.Parse<System.IO.Ports.StopBits>(settings.StopBits);

            var source = new SignalBench.Core.Ingestion.SerialTelemetrySource(settings.Port, settings.BaudRate, schema, parity, settings.DataBits, stopBits);
            SelectedPlot.ActiveSource = source;
            source.PacketReceived += (p) => HandleLivePacket(targetPlot, p);
            source.ErrorReceived += msg => {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => { StatusText = $"Serial Error: {msg}"; targetPlot.IsStreaming = false; NotifySourceStateChanged(); });
            };
            
            await Task.Run(() => source.Start());
            NotifySourceStateChanged();

            // Force status bar info refresh
            if (SelectedPlot != null)
            {
                SelectedPlot.RaisePropertyChanged(nameof(PlotViewModel.ConnectionInfo));
                SelectedPlot.RaisePropertyChanged(nameof(PlotViewModel.ConnectionIcon));
            }

            StatusText = $"Streaming from {settings.Port}...";
        } catch (Exception ex) { await ShowError("Connection Error", $"Failed to start streaming: {ex.Message}"); }
    }

    private DateTime _lastLivePlotUpdate = DateTime.MinValue;
    private readonly object _liveDataLock = new();
    private Dictionary<PlotViewModel, List<DecodedPacket>> _livePacketBuffers = new();

    private void HandleLivePacket(PlotViewModel plot, DecodedPacket packet)
    {
        lock (_liveDataLock) { 
            if (!_livePacketBuffers.ContainsKey(plot)) _livePacketBuffers[plot] = new();
            _livePacketBuffers[plot].Add(packet); 
        }
        if ((DateTime.Now - _lastLivePlotUpdate).TotalMilliseconds > 100) {
            _lastLivePlotUpdate = DateTime.Now;
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateLivePlot);
        }
    }

    private void UpdateLivePlot()
    {
        Dictionary<PlotViewModel, List<DecodedPacket>> batches;
        lock (_liveDataLock) { 
            batches = new Dictionary<PlotViewModel, List<DecodedPacket>>(_livePacketBuffers);
            _livePacketBuffers.Clear();
        }

        foreach (var kvp in batches)
        {
            var targetPlot = kvp.Key;
            var batch = kvp.Value;
            if (batch.Count == 0) continue;

            var targetStore = targetPlot.DataStore;
            bool wasEmpty = targetPlot.TotalRecords == 0;
            targetStore.InsertPackets(batch);

            // Re-compute derived signals if any exist for this plot
            if (targetPlot.DerivedSignals.Count > 0)
            {
                foreach (var ds in targetPlot.DerivedSignals)
                {
                    // Pass specific store to ComputeDerivedSignal
                    targetStore.InsertDerivedSignal(ds.Name, ComputeDerivedSignal(ds, targetStore, targetPlot));
                }
            }

            int rowCount = targetStore.GetRowCount();
            targetPlot.TotalRecords = rowCount;
            if (targetPlot == SelectedPlot)
            {
                _totalRecords = rowCount;
                this.RaisePropertyChanged(nameof(TotalRecords));
                this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
                if (wasEmpty)
                {
                    this.RaisePropertyChanged(nameof(HasData));
                    this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
                }
            }

            // Determine rolling window in seconds
            int windowSeconds = targetPlot.SourceType == PlotSourceType.Serial 
                ? targetPlot.SerialSettings.RollingWindowSeconds 
                : targetPlot.NetworkSettings.RollingWindowSeconds;

            DateTime latestTs = targetStore.GetTimestamp(targetPlot.TotalRecords - 1);
            DateTime startTs = latestTs.AddSeconds(-windowSeconds);

            var timestamps = targetStore.GetTimestamps(startTs);
            var plotData = new Dictionary<string, List<double>>();
            foreach (var signalName in targetPlot.SelectedSignalNames)
                plotData[signalName] = targetStore.GetSignalData(signalName, startTs);

            if (timestamps.Count > 0)
            {
                double? forceXMax = timestamps[^1].ToOADate();
                targetPlot.RequestPlotUpdate?.Invoke(timestamps, plotData, null, forceXMax, windowSeconds);
            }
        }
    }

    private void Seek(double progress)
    {
        var newIndex = (int)(TotalRecords * progress / 100.0);
        newIndex = Math.Clamp(newIndex, 0, Math.Max(0, TotalRecords - 1));
        
        lock (_playbackLock) {
            _currentPlaybackIndex = newIndex;
            if (SelectedPlot != null) SelectedPlot.CurrentPlaybackIndex = newIndex;
            _playbackProgressValue = progress;
            if (TotalRecords > 1 && _fullDuration > 0) _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            else _savedElapsedSeconds = 0;
            if (SelectedPlot != null) SelectedPlot.SavedElapsedSeconds = _savedElapsedSeconds;
            if (IsPlaying && _playbackStopwatch != null) _playbackStopwatch.Restart();
        }

        this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
        this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
        this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        this.RaisePropertyChanged(nameof(PlaybackProgress));
        UpdateCursorPosition();
    }

    private void StepForward()
    {
        if (TotalRecords == 0) return;
        lock (_playbackLock) {
            _currentPlaybackIndex = Math.Min(_currentPlaybackIndex + 1, TotalRecords - 1);
            if (SelectedPlot != null) SelectedPlot.CurrentPlaybackIndex = _currentPlaybackIndex;
            _playbackProgressValue = TotalRecords > 1 ? (double)_currentPlaybackIndex / (TotalRecords - 1) * 100 : 0;
            if (TotalRecords > 1 && _fullDuration > 0) _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            else _savedElapsedSeconds = 0;
            if (SelectedPlot != null) SelectedPlot.SavedElapsedSeconds = _savedElapsedSeconds;
            if (IsPlaying && _playbackStopwatch != null) _playbackStopwatch.Restart();
        }
        this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
        this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
        this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        this.RaisePropertyChanged(nameof(PlaybackProgress));
        UpdateCursorPosition();
    }

    private void StepBackward()
    {
        if (TotalRecords == 0) return;
        lock (_playbackLock) {
            _currentPlaybackIndex = Math.Max(_currentPlaybackIndex - 1, 0);
            if (SelectedPlot != null) SelectedPlot.CurrentPlaybackIndex = _currentPlaybackIndex;
            _playbackProgressValue = TotalRecords > 1 ? (double)_currentPlaybackIndex / (TotalRecords - 1) * 100 : 0;
            if (TotalRecords > 1 && _fullDuration > 0) _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            else _savedElapsedSeconds = 0;
            if (SelectedPlot != null) SelectedPlot.SavedElapsedSeconds = _savedElapsedSeconds;
            if (IsPlaying && _playbackStopwatch != null) _playbackStopwatch.Restart();
        }
        this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
        this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
        this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        this.RaisePropertyChanged(nameof(PlaybackProgress));
        UpdateCursorPosition();
    }

    private void FastForward()
    {
        if (TotalRecords == 0) return;
        lock (_playbackLock) {
            var step = Math.Max(1, TotalRecords / 100);
            _currentPlaybackIndex = Math.Min(_currentPlaybackIndex + step, TotalRecords - 1);
            if (SelectedPlot != null) SelectedPlot.CurrentPlaybackIndex = _currentPlaybackIndex;
            _playbackProgressValue = TotalRecords > 1 ? (double)_currentPlaybackIndex / (TotalRecords - 1) * 100 : 0;
            if (TotalRecords > 1 && _fullDuration > 0) _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            if (SelectedPlot != null) SelectedPlot.SavedElapsedSeconds = _savedElapsedSeconds;
            if (IsPlaying && _playbackStopwatch != null) _playbackStopwatch.Restart();
        }
        this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
        this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
        this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        this.RaisePropertyChanged(nameof(PlaybackProgress));
        UpdateCursorPosition();
    }

    private void FastBackward()
    {
        if (TotalRecords == 0) return;
        lock (_playbackLock) {
            var step = Math.Max(1, TotalRecords / 100);
            _currentPlaybackIndex = Math.Max(_currentPlaybackIndex - step, 0);
            if (SelectedPlot != null) SelectedPlot.CurrentPlaybackIndex = _currentPlaybackIndex;
            _playbackProgressValue = TotalRecords > 1 ? (double)_currentPlaybackIndex / (TotalRecords - 1) * 100 : 0;
            if (TotalRecords > 1 && _fullDuration > 0) _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            else _savedElapsedSeconds = 0;
            if (SelectedPlot != null) SelectedPlot.SavedElapsedSeconds = _savedElapsedSeconds;
            if (IsPlaying && _playbackStopwatch != null) _playbackStopwatch.Restart();
        }
        this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
        this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
        this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        this.RaisePropertyChanged(nameof(PlaybackProgress));
        UpdateCursorPosition();
    }

    private void Restart()
    {
        if (TotalRecords == 0 || SelectedPlot == null) return;
        _currentPlaybackIndex = 0; _savedElapsedSeconds = 0;
        if (TotalRecords > 1) {
            var firstTs = _dataStore.GetTimestamp(0);
            var lastTs = _dataStore.GetTimestamp(TotalRecords - 1);
            _fullDuration = (lastTs - firstTs).TotalSeconds;
            if (_fullDuration < 0.1) _fullDuration = TotalRecords - 1;
        } else { _fullDuration = 0; }
        _playbackProgressValue = 0;
        
        var maxPoints = 10000;
        _playbackTimestamps = _dataStore.GetTimestamps(maxPoints);
        _playbackSignalData = [];
        foreach (var signalName in SelectedPlot.SelectedSignalNames) {
            var data = _dataStore.GetSignalData(signalName, maxPoints);
            if (data.Count == _playbackTimestamps.Count) _playbackSignalData[signalName] = data;
        }
        
        this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
        this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
        this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        this.RaisePropertyChanged(nameof(PlaybackProgress));
        UpdatePlaybackView();
    }

    private async Task OpenCsvAsync()
    {
        try {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var dialog = new SignalBench.Views.CsvImport { DataContext = new CsvImportViewModel() };
            var result = await dialog.ShowDialog<CsvImportResult?>(topLevel);
            if (result != null && !string.IsNullOrEmpty(result.FilePath))
            {
                var settings = new SignalBench.Core.Session.CsvSettings { 
                    Delimiter = result.Delimiter, 
                    TimestampColumn = result.TimestampColumn, 
                    HasHeader = result.HasHeader 
                };
                await LoadTelemetryFileAsync(result.FilePath, null, null, settings);
            }
        } catch (Exception ex) { await ShowError("CSV Error", "Failed to open CSV import.", ex); }
    }

    private async Task OpenBinaryAsync()
    {
        try {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var dialog = new SignalBench.Views.BinaryImport { DataContext = new BinaryImportViewModel(null, _loggerFactory.CreateLogger<BinaryImportViewModel>()) };
            var result = await dialog.ShowDialog<BinaryImportResult?>(topLevel);
            if (result != null && !string.IsNullOrEmpty(result.TelemetryPath))
            {
                await LoadTelemetryFileAsync(result.TelemetryPath, null, result.Schema, null, result.TimestampField);
            }
        } catch (Exception ex) { await ShowError("Binary Error", "Failed to open binary import.", ex); }
    }

    private async Task LoadTelemetryFileAsync(string path, string? schemaPath = null, PacketSchema? existingSchema = null, CsvSettings? existingCsvSettings = null, string? timestampField = null)
    {
        if (IsStreaming) {
            await StopStreamingAsync();
        }

        _statusText = $"Loading {path}..."; this.RaisePropertyChanged(nameof(StatusText));
        var startTime = DateTime.Now;
        
        if (path.EndsWith(".csv")) {
            CsvSettings csvSettings;
            if (existingCsvSettings != null) {
                csvSettings = existingCsvSettings;
            } else {
                var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                if (topLevel == null) return;
                var dialog = new SignalBench.Views.CsvImport { DataContext = new CsvImportViewModel(path) };
                var result = await dialog.ShowDialog<CsvImportResult?>(topLevel);
                if (result == null) return;
                csvSettings = new CsvSettings { 
                    Delimiter = result.Delimiter, 
                    TimestampColumn = result.TimestampColumn, 
                    HasHeader = result.HasHeader 
                };
            }

            AddPlot(Path.GetFileName(path), path);
            var targetPlot = SelectedPlot;
            var targetStore = targetPlot!.DataStore;
            targetPlot.CsvSettings = csvSettings;

            await Task.Run(async () => {
                try {
                    var lineCount = File.ReadLines(path).Count();
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => { IsLoading = true; LoadProgress = 0; LoadElapsed = "00:00"; });
                    
                    var source = new SignalBench.Core.Ingestion.CsvTelemetrySource(path, csvSettings.Delimiter, csvSettings.TimestampColumn, csvSettings.HasHeader);
                    var packets = new List<DecodedPacket>();
                    var processed = 0; var lastUpdate = DateTime.Now;
                    foreach (var packet in source.ReadPackets()) {
                        packets.Add(packet); processed++;
                        if ((DateTime.Now - lastUpdate).TotalMilliseconds > 100) {
                            var elapsed = DateTime.Now - startTime;
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => { LoadProgress = (double)processed / lineCount * 100; LoadElapsed = elapsed.ToString(@"mm\:ss"); });
                            lastUpdate = DateTime.Now; await Task.Delay(1);
                        }
                    }
                    if (packets.Count > 0) {
                        var fields = new List<string>(packets[0].Fields.Keys);
                        var timestampCol = csvSettings.TimestampColumn;
                        var schema = new PacketSchema { Name = "CSV Import", Type = SchemaType.Csv };
                        foreach (var field in fields) schema.Fields.Add(new FieldDefinition { Name = field });
                        
                        targetStore.InitializeSchema(schema); 
                        targetStore.InsertPackets(packets);

                        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                            targetPlot.TelemetryPath = path; targetPlot.Schema = schema;
                            targetPlot.SourceType = PlotSourceType.File;
                            targetPlot.CsvSettings = csvSettings;
                            
                            // Always populate the targetPlot's signals (excluding timestamp column)
                            targetPlot.AvailableSignals.Clear();
                            targetPlot.RegularSignals.Clear();
                            foreach (var field in fields) {
                                if (field.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) continue;
                                if (!string.IsNullOrEmpty(timestampCol) && field.Equals(timestampCol, StringComparison.OrdinalIgnoreCase)) continue;
                                bool shouldSelect = targetPlot.RegularSignals.Count < 2;
                                var signalItem = new SignalItemViewModel { Name = field, IsSelected = shouldSelect, ColorIndex = targetPlot.AvailableSignals.Count };
                                targetPlot.AvailableSignals.Add(signalItem); targetPlot.RegularSignals.Add(signalItem);
                                if (shouldSelect) targetPlot.SelectedSignalNames.Add(field);
                            }

                            if (targetPlot == SelectedPlot)
                            {
                                this.RaisePropertyChanged(nameof(HasData));
                                AddToRecentFiles(path); UpdatePlot(targetPlot);
                                this.RaisePropertyChanged(nameof(CurrentPlaybackIndex)); this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                                this.RaisePropertyChanged(nameof(FormattedPlaybackTime)); this.RaisePropertyChanged(nameof(PlaybackProgress));
                                this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
                                
                                SyncSignalCheckboxes();
                            }
                            IsLoading = false;
                            StatusText = $"Loaded {packets.Count:N0} records in {(DateTime.Now - startTime).TotalSeconds:F1}s";
                        });
                    }
                } catch (Exception ex) { Avalonia.Threading.Dispatcher.UIThread.Post(() => IsLoading = false); await ShowError("Load Error", "Failed to load CSV telemetry.", ex); }
            });
        } else {
            PacketSchema? schema = existingSchema;
            if (schema == null && !string.IsNullOrEmpty(schemaPath)) {
                try { var yaml = await File.ReadAllTextAsync(schemaPath); schema = new SchemaLoader().Load(yaml); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not load schema."); }
            }
            if (schema == null) schema = await PromptForSchemaAsync(path);
            if (schema == null) return;
            schema.Type = SchemaType.Binary;

            AddPlot(Path.GetFileName(path), path, schema);
            var targetPlot = SelectedPlot;
            targetPlot!.SchemaPath = schemaPath; // Store the actual file path
            var targetStore = targetPlot!.DataStore;

            await Task.Run(async () => {
                try {
                    targetStore.InitializeSchema(schema);
                    var source = new SignalBench.Core.Ingestion.BinaryTelemetrySource(path, schema);
                    var packets = source.ReadPackets().ToList();
                    
                    // Update packet timestamps if a specific field was chosen
                    if (!string.IsNullOrEmpty(timestampField))
                    {
                        foreach (var packet in packets)
                        {
                            if (packet.Fields.TryGetValue(timestampField, out var val))
                            {
                                try
                                {
                                    // Handle numeric timestamp (seconds or milliseconds since some epoch, or just raw increment)
                                    // For now, we'll treat it as seconds if it's large, or just a relative offset.
                                    // Let's assume the value is seconds for now if it's a number.
                                    double seconds = Convert.ToDouble(val);
                                    packet.Timestamp = DateTime.UnixEpoch.AddSeconds(seconds);
                                }
                                catch { /* fallback to default */ }
                            }
                        }
                    }

                    targetStore.InsertPackets(packets);
                    var fields = new List<string>(schema.Fields.Select(f => f.Name));
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        targetPlot.TelemetryPath = path; targetPlot.Schema = schema;
                        targetPlot.SourceType = PlotSourceType.File;
                        
                        // Always populate the targetPlot's signals
                        targetPlot.AvailableSignals.Clear();
                        targetPlot.RegularSignals.Clear();
                        foreach (var field in fields) {
                            if (field.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) continue;
                            bool shouldSelect = targetPlot.RegularSignals.Count < 2;
                            var signalItem = new SignalItemViewModel { Name = field, IsSelected = shouldSelect, ColorIndex = targetPlot.AvailableSignals.Count };
                            targetPlot.AvailableSignals.Add(signalItem); targetPlot.RegularSignals.Add(signalItem);
                            if (shouldSelect) targetPlot.SelectedSignalNames.Add(field);
                        }

                        if (SelectedPlot == targetPlot) {
                            this.RaisePropertyChanged(nameof(HasData));
                            AddToRecentFiles(path); UpdatePlot(targetPlot);
                            this.RaisePropertyChanged(nameof(CurrentPlaybackIndex)); this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                            this.RaisePropertyChanged(nameof(FormattedPlaybackTime)); this.RaisePropertyChanged(nameof(PlaybackProgress));
                            this.RaisePropertyChanged(nameof(IsPlaybackBarVisible));
                            
                            SyncSignalCheckboxes();
                        }
                        StatusText = $"Loaded {packets.Count} records from {Path.GetFileName(path)}";
                    });
                } catch (Exception ex) { await ShowError("Load Error", "Failed to load binary telemetry.", ex); }
            });
        }
    }

    private async Task<PacketSchema?> PromptForSchemaAsync(string telemetryPath)
    {
        try {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return null;
            var dialog = new SignalBench.Views.BinaryImport { DataContext = new BinaryImportViewModel(telemetryPath, _loggerFactory.CreateLogger<BinaryImportViewModel>()) };
            var result = await dialog.ShowDialog<BinaryImportResult?>(topLevel);
            return result?.Schema;
        } catch (Exception ex) { await ShowError("Schema Error", "Failed to prompt for schema.", ex); return null; }
    }

    private async Task EditSchemaAsync()
    {
        try {
            if (SelectedSchema == null) return;
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var dialog = new SignalBench.Views.SchemaEditor { DataContext = new SchemaEditorViewModel(SelectedSchema) };
            var result = await dialog.ShowDialog<SchemaEditorResult?>(topLevel);
            if (result != null) { SelectedSchema = result.Schema; }
        } catch (Exception ex) { await ShowError("Editor Error", "Failed to open schema editor.", ex); }
    }

    private async Task OpenSchemaAsync()
    {
        try {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions {
                Title = "Open Packet Schema", AllowMultiple = false,
                FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("YAML Files") { Patterns = ["*.yaml", "*.yml"] }]
            });
            if (files.Count > 0) {
                var path = files[0].Path.LocalPath;
                var yaml = await File.ReadAllTextAsync(path);
                SelectedSchema = new SchemaLoader().Load(yaml);
                if (SelectedSchema != null) SelectedSchema.Type = SchemaType.Streaming;
                StatusText = $"Schema loaded: {SelectedSchema?.Name}";
            }
        } catch (Exception ex) { await ShowError("Schema Error", "Failed to open schema.", ex); }
    }

    private async Task CreateSchemaAsync()
    {
        try {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var dialog = new SignalBench.Views.SchemaEditor { DataContext = new SchemaEditorViewModel() };
            await dialog.ShowDialog<SchemaEditorResult?>(topLevel);
        } catch (Exception ex) { await ShowError("Editor Error", "Failed to open schema editor.", ex); }
    }

    private async Task CreateDerivedSignalAsync()
    {
        try {
            if (SelectedPlot == null) return;
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var availableFields = SelectedPlot.RegularSignals.Select(s => s.Name).ToList();
            var allSignalNames = SelectedPlot.AvailableSignals.Select(s => s.Name).ToList();
            var dialog = new SignalBench.Views.DerivedSignalDialog { DataContext = new DerivedSignalViewModel(availableFields, allSignalNames) };
            var result = await dialog.ShowDialog<DerivedSignalResult?>(topLevel);
            if (result != null) {
                var ds = new DerivedSignalDefinition { Name = result.Name, Formula = result.Formula };
                SelectedPlot.DerivedSignals.Add(ds);
                _dataStore.InsertDerivedSignal(result.Name, ComputeDerivedSignal(ds, _dataStore, SelectedPlot));
                var item = new SignalItemViewModel { Name = result.Name, IsSelected = true, IsDerived = true };
                
                // Add to plot's collection
                SelectedPlot.AvailableSignals.Add(item);
                SelectedPlot.SelectedSignalNames.Add(result.Name);
                
                SyncSignalCheckboxes();
                UpdatePlot(SelectedPlot);
            }
        } catch (Exception ex) { await ShowError("Derived Signal Error", "Failed to create derived signal.", ex); }
    }

    private async Task EditDerivedSignalAsync(string name)
    {
        try {
            if (SelectedPlot == null) return;
            var ds = SelectedPlot.DerivedSignals.FirstOrDefault(d => d.Name == name);
            if (ds == null) return;

            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var availableFields = SelectedPlot.RegularSignals.Select(s => s.Name).ToList();
            var allSignalNames = SelectedPlot.AvailableSignals.Select(s => s.Name).ToList();
            var dialog = new SignalBench.Views.DerivedSignalDialog { 
                DataContext = new DerivedSignalViewModel(availableFields, allSignalNames, ds) { IsEditMode = true } 
            };
            var result = await dialog.ShowDialog<DerivedSignalResult?>(topLevel);

            if (result != null) {
                if (result.IsDeleted) {
                    await RemoveDerivedSignalAsync(name);
                    return;
                }

                ds.Name = result.Name;
                ds.Formula = result.Formula;
                
                // Recompute and update UI
                _dataStore.InsertDerivedSignal(ds.Name, ComputeDerivedSignal(ds, _dataStore, SelectedPlot));
                var item = SelectedPlot.AvailableSignals.FirstOrDefault(s => s.Name == name);
                if (item != null) item.Name = ds.Name;

                UpdatePlot(SelectedPlot);
            }
        } catch (Exception ex) { await ShowError("Edit Error", "Failed to edit derived signal.", ex); }
    }

    private async Task RemoveDerivedSignalAsync(string name)
    {
        try {
            if (SelectedPlot == null) return;
            var ds = SelectedPlot.DerivedSignals.FirstOrDefault(d => d.Name == name);
            if (ds != null) SelectedPlot.DerivedSignals.Remove(ds);

            var pItem = SelectedPlot.AvailableSignals.FirstOrDefault(s => s.Name == name);
            if (pItem != null) SelectedPlot.AvailableSignals.Remove(pItem);

            SelectedPlot.SelectedSignalNames.Remove(name);
            
            _dataStore.DeleteSignal(name);
            UpdatePlot(SelectedPlot);
        } catch (Exception ex) { await ShowError("Remove Error", "Failed to remove derived signal.", ex); }
    }

    private List<double> ComputeDerivedSignal(DerivedSignalDefinition derived, IDataStore? targetStore = null, PlotViewModel? targetPlot = null)
    {
        var result = new List<double>();
        var store = targetStore ?? _dataStore;
        var plot = targetPlot ?? SelectedPlot;
        if (plot == null) return result;

        var timestamps = store.GetTimestamps();
        var schemaSignals = plot.RegularSignals.ToList();
        var data = new Dictionary<string, List<double>>();
        foreach (var s in schemaSignals) data[s.Name] = store.GetSignalData(s.Name);
        
        for (int i = 0; i < timestamps.Count; i++) {
            var vars = new Dictionary<string, object>();
            foreach (var s in schemaSignals) {
                if (i < data[s.Name].Count)
                    vars[s.Name] = data[s.Name][i];
                else
                    vars[s.Name] = 0.0;
            }
            try { 
                var val = _formulaEngine.Evaluate(derived.Formula, vars);
                result.Add(Convert.ToDouble(val)); 
            }
            catch { result.Add(double.NaN); }
        }
        return result;
    }

    private async Task SaveSessionAsync()
    {
        try {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions {
                Title = "Save Session", DefaultExtension = "sbs",
                FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("SignalBench Session") { Patterns = ["*.sbs"] }]
            });
            if (file != null) {
                var tabs = new List<TabSession>();
                foreach (var p in Plots)
                {
                    var tab = new TabSession {
                        Name = p.Name,
                        SourceType = p.SourceType.ToString(),
                        TelemetryPath = p.TelemetryPath,
                        SelectedSignalNames = [.. p.SelectedSignalNames],
                        DerivedSignals = [.. p.DerivedSignals]
                    };

                    // Embed schema YAML if available
                    if (p.Schema != null)
                    {
                        try { tab.SchemaYaml = new SchemaLoader().Save(p.Schema); } catch { }
                    }

                    // Only save settings relevant to the source type
                    if (p.SourceType == PlotSourceType.Serial) tab.SerialSettings = p.SerialSettings;
                    if (p.SourceType == PlotSourceType.Network) tab.NetworkSettings = p.NetworkSettings;
                    if (p.SourceType == PlotSourceType.File && p.Schema?.Type == SchemaType.Csv)
                    {
                        tab.CsvSettings = new CsvSettings
                        {
                            Delimiter = p.CsvSettings.Delimiter,
                            TimestampColumn = p.CsvSettings.TimestampColumn,
                            HasHeader = p.CsvSettings.HasHeader
                        };
                    }

                    tabs.Add(tab);
                }

                var session = new ProjectSession {
                    SelectedTabIndex = SelectedPlot != null ? Plots.IndexOf(SelectedPlot) : 0,
                    Tabs = tabs
                };
                
                _sessionManager.SaveSession(file.Path.LocalPath, session);
                StatusText = "Session saved.";
            }
        } catch (Exception ex) { await ShowError("Save Error", "Failed to save session.", ex); }
    }

    private async Task LoadSessionInternalAsync(string path)
    {
        try {
            var session = _sessionManager.LoadSession(path);
            
            // Clear existing plots
            if (IsStreaming) await StopStreamingAsync();
            foreach(var p in Plots) p.Dispose();
            Plots.Clear();
            
            foreach (var tab in session.Tabs)
            {
                // Reconstruct PlotViewModel
                var mode = _settingsService.Current.StorageMode == "Sqlite" ? StorageMode.Sqlite : StorageMode.InMemory;
                IDataStore store;
                if (mode == StorageMode.Sqlite) {
                    store = new SqliteDataStore(Path.Combine(Path.GetTempPath(), $"signalbench_{Guid.NewGuid():N}.db"));
                } else {
                    store = new InMemoryDataStore();
                }

                var plot = new PlotViewModel(tab.Name, store);
                plot.SourceType = Enum.Parse<PlotSourceType>(tab.SourceType);
                plot.TelemetryPath = tab.TelemetryPath;
                
                // Restore settings if provided
                if (tab.SerialSettings != null)
                {
                    plot.SerialSettings.Port = tab.SerialSettings.Port;
                    plot.SerialSettings.BaudRate = tab.SerialSettings.BaudRate;
                    plot.SerialSettings.Parity = tab.SerialSettings.Parity;
                    plot.SerialSettings.DataBits = tab.SerialSettings.DataBits;
                    plot.SerialSettings.StopBits = tab.SerialSettings.StopBits;
                    plot.SerialSettings.RollingWindowSeconds = tab.SerialSettings.RollingWindowSeconds;
                }

                if (tab.NetworkSettings != null)
                {
                    plot.NetworkSettings.Protocol = tab.NetworkSettings.Protocol;
                    plot.NetworkSettings.IpAddress = tab.NetworkSettings.IpAddress;
                    plot.NetworkSettings.Port = tab.NetworkSettings.Port;
                    plot.NetworkSettings.RollingWindowSeconds = tab.NetworkSettings.RollingWindowSeconds;
                }

                // Load Schema (prefer embedded YAML)
                PacketSchema? schema = null;
                if (!string.IsNullOrEmpty(tab.SchemaYaml))
                {
                    schema = new SchemaLoader().Load(tab.SchemaYaml);
                    plot.Schema = schema;
                }

                Plots.Add(plot);
                SelectedPlot = plot;

                // Load data if it was a file
                if (plot.SourceType == PlotSourceType.File && !string.IsNullOrEmpty(tab.TelemetryPath) && File.Exists(tab.TelemetryPath))
                {
                    await LoadTelemetryFileAsync(tab.TelemetryPath, null, schema, tab.CsvSettings);
                }
                
                // Restart streaming if it was a stream
                if (plot.SourceType == PlotSourceType.Serial && plot.IsSerialConfigured && plot.Schema != null)
                {
                    await StartStreamingAsync();
                }
                else if (plot.SourceType == PlotSourceType.Network && plot.IsNetworkConfigured && plot.Schema != null)
                {
                    await StartNetworkStreamingAsync();
                }
                
                var targetPlot = Plots.Last();
                targetPlot.SelectedSignalNames.Clear();
                foreach (var sName in tab.SelectedSignalNames) targetPlot.SelectedSignalNames.Add(sName);
                
                foreach (var ds in tab.DerivedSignals)
                {
                    if (!targetPlot.DerivedSignals.Any(d => d.Name == ds.Name))
                        targetPlot.DerivedSignals.Add(ds);
                }
            }

            if (session.SelectedTabIndex >= 0 && session.SelectedTabIndex < Plots.Count)
                SelectedPlot = Plots[session.SelectedTabIndex];
            
            StatusText = $"Session loaded: {Path.GetFileName(path)}";
            
            // Persist as last session
            _settingsService.Current.LastSessionPath = path;
            _settingsService.Save();
        } catch (Exception ex) { await ShowError("Load Error", "Failed to load session.", ex); }
    }

    private async Task OpenSessionAsync()
    {
        try {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions {
                Title = "Open Session", AllowMultiple = false,
                FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("SignalBench Session") { Patterns = ["*.sbs"] }]
            });
            
            if (files.Count > 0) {
                var session = _sessionManager.LoadSession(files[0].Path.LocalPath);
                
                // Clear existing plots
                if (IsStreaming) await StopStreamingAsync();
                foreach(var p in Plots) p.Dispose();
                Plots.Clear();
                
                foreach (var tab in session.Tabs)
                {
                    // Reconstruct PlotViewModel
                    var mode = _settingsService.Current.StorageMode == "Sqlite" ? StorageMode.Sqlite : StorageMode.InMemory;
                    IDataStore store;
                    if (mode == StorageMode.Sqlite) {
                        store = new SqliteDataStore(Path.Combine(Path.GetTempPath(), $"signalbench_{Guid.NewGuid():N}.db"));
                    } else {
                        store = new InMemoryDataStore();
                    }

                    var plot = new PlotViewModel(tab.Name, store);
                    plot.SourceType = Enum.Parse<PlotSourceType>(tab.SourceType);
                    plot.TelemetryPath = tab.TelemetryPath;
                    
                    // Restore settings if provided
                    if (tab.SerialSettings != null)
                    {
                        plot.SerialSettings.Port = tab.SerialSettings.Port;
                        plot.SerialSettings.BaudRate = tab.SerialSettings.BaudRate;
                        plot.SerialSettings.Parity = tab.SerialSettings.Parity;
                        plot.SerialSettings.DataBits = tab.SerialSettings.DataBits;
                        plot.SerialSettings.StopBits = tab.SerialSettings.StopBits;
                        plot.SerialSettings.RollingWindowSeconds = tab.SerialSettings.RollingWindowSeconds;
                    }

                    if (tab.NetworkSettings != null)
                    {
                        plot.NetworkSettings.Protocol = tab.NetworkSettings.Protocol;
                        plot.NetworkSettings.IpAddress = tab.NetworkSettings.IpAddress;
                        plot.NetworkSettings.Port = tab.NetworkSettings.Port;
                        plot.NetworkSettings.RollingWindowSeconds = tab.NetworkSettings.RollingWindowSeconds;
                    }

                    // Load Schema from embedded YAML
                    PacketSchema? schema = null;
                    if (!string.IsNullOrEmpty(tab.SchemaYaml))
                    {
                        schema = new SchemaLoader().Load(tab.SchemaYaml);
                        plot.Schema = schema;
                    }

                    Plots.Add(plot);
                    SelectedPlot = plot;

                    // Load data if it was a file
                    if (plot.SourceType == PlotSourceType.File && !string.IsNullOrEmpty(tab.TelemetryPath) && File.Exists(tab.TelemetryPath))
                    {
                        await LoadTelemetryFileAsync(tab.TelemetryPath, null, schema, tab.CsvSettings);
                    }
                    
                    // Restart streaming if it was a stream
                    if (plot.SourceType == PlotSourceType.Serial && plot.IsSerialConfigured && plot.Schema != null)
                    {
                        await StartStreamingAsync();
                    }
                    else if (plot.SourceType == PlotSourceType.Network && plot.IsNetworkConfigured && plot.Schema != null)
                    {
                        await StartNetworkStreamingAsync();
                    }
                    
                    var targetPlot = Plots.Last();
                    targetPlot.SelectedSignalNames.Clear();
                    foreach (var sName in tab.SelectedSignalNames) targetPlot.SelectedSignalNames.Add(sName);
                    
                    foreach (var ds in tab.DerivedSignals)
                    {
                        if (!targetPlot.DerivedSignals.Any(d => d.Name == ds.Name))
                            targetPlot.DerivedSignals.Add(ds);
                    }
                }

                if (session.SelectedTabIndex >= 0 && session.SelectedTabIndex < Plots.Count)
                    SelectedPlot = Plots[session.SelectedTabIndex];
                
                StatusText = "Session loaded.";
            }
        } catch (Exception ex) { await ShowError("Load Error", "Failed to load session.", ex); }
    }
    public void AutoSaveSession()
    {
        try {
            string autoSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SignalBench", "autosave.sbs");
            Directory.CreateDirectory(Path.GetDirectoryName(autoSavePath)!);

            var tabs = new List<TabSession>();
            foreach (var p in Plots) {
                var tab = new TabSession {
                    Name = p.Name, SourceType = p.SourceType.ToString(), TelemetryPath = p.TelemetryPath,
                    SelectedSignalNames = [.. p.SelectedSignalNames], DerivedSignals = [.. p.DerivedSignals],
                    SerialSettings = p.SerialSettings, NetworkSettings = p.NetworkSettings, CsvSettings = p.CsvSettings
                };
                if (p.Schema != null) { try { tab.SchemaYaml = new SchemaLoader().Save(p.Schema); } catch { } }
                tabs.Add(tab);
            }

            var session = new ProjectSession { SelectedTabIndex = SelectedPlot != null ? Plots.IndexOf(SelectedPlot) : 0, Tabs = tabs };
            _sessionManager.SaveSession(autoSavePath, session);
            
            _settingsService.Current.LastSessionPath = autoSavePath;
            _settingsService.Save();
        } catch (Exception ex) { _logger.LogWarning(ex, "Auto-save failed."); }
    }

    private async Task OpenAboutAsync() {
        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel != null) await new SignalBench.Views.AboutWindow().ShowDialog(topLevel);
    }

    private async Task ExportCsv()
    {
        try {
            if (SelectedPlot == null) return;
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions {
                Title = "Export CSV", DefaultExtension = "csv",
                FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("CSV Files") { Patterns = ["*.csv"] }]
            });
            if (file != null) {
                StatusText = "Exporting CSV...";
                await Task.Run(async () => {
                    using var writer = new StreamWriter(file.Path.LocalPath);
                    var selectedSignals = SelectedPlot.AvailableSignals.Where(s => s.IsSelected).ToList();
                    var headers = new List<string> { "Timestamp" };
                    headers.AddRange(selectedSignals.Select(s => s.Name));
                    await writer.WriteLineAsync(string.Join(",", headers));
                    var timestamps = _dataStore.GetTimestamps();
                    var signalData = new Dictionary<string, List<double>>();
                    foreach (var signal in selectedSignals) signalData[signal.Name] = _dataStore.GetSignalData(signal.Name);
                    for (int i = 0; i < timestamps.Count; i++) {
                        var row = new List<string> { timestamps[i].ToString("yyyy-MM-dd HH:mm:ss.fff") };
                        foreach (var signal in selectedSignals) row.Add(signalData[signal.Name][i].ToString());
                        await writer.WriteLineAsync(string.Join(",", row));
                    }
                });
                StatusText = "Export complete.";
            }
        } catch (Exception ex) { await ShowError("Export Error", "Failed to export CSV.", ex); }
    }

    private async Task StopStreamingAsync()
    {
        foreach (var p in Plots) {
            p.IsStreaming = false;
            p.IsPaused = false;
            if (p.ActiveSource != null)
            {
                var s = p.ActiveSource; p.ActiveSource = null;
                await Task.Run(() => s.Stop());
            }
        }
        
        NotifySourceStateChanged();
        StatusText = "Streaming stopped.";
    }

    private async Task ToggleRecording()
    {
        if (SelectedPlot?.ActiveSource == null) return;
        var source = SelectedPlot.ActiveSource;

        if (SelectedPlot.IsRecording)
        {
            source.StopRecording(); 
            SelectedPlot.IsRecording = false;
            if (SelectedPlot == SelectedPlot) IsRecording = false;
            StatusText = "Recording stopped.";
        }
        else
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save Raw Stream",
                DefaultExtension = "bin",
                FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("Binary Files") { Patterns = ["*.bin", "*.dat"] }]
            });
            if (file != null)
            {
                source.StartRecording(file.Path.LocalPath); 
                SelectedPlot.IsRecording = true;
                if (SelectedPlot == SelectedPlot) IsRecording = true;
                StatusText = $"Recording to {file.Name}...";
            }
        }
    }
}
