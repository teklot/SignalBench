using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using SignalBench.Core;
using SignalBench.Core.Data;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;
using SignalBench.Core.Session;
using SignalBench.Views;
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
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
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

    private string? _currentTelemetryPath;
    private string? _currentSchemaPath;

    private PacketSchema? _selectedSchema;
    public PacketSchema? SelectedSchema
    {
        get => _selectedSchema;
        set => this.RaiseAndSetIfChanged(ref _selectedSchema, value);
    }

    public bool HasData => !string.IsNullOrEmpty(_currentTelemetryPath) || AvailableSignals.Count > 0;

    private bool _isSignalsPaneOpen = true;
    public bool IsSignalsPaneOpen
    {
        get => _isSignalsPaneOpen;
        set {
            this.RaiseAndSetIfChanged(ref _isSignalsPaneOpen, value);
            this.RaisePropertyChanged(nameof(SignalsPaneColumnWidth));
        }
    }

    private GridLength _signalsPaneColumnWidth = new GridLength(200);
    public GridLength SignalsPaneColumnWidth
    {
        get => IsSignalsPaneOpen ? _signalsPaneColumnWidth : new GridLength(0);
        set {
            if (IsSignalsPaneOpen && value.IsAbsolute)
            {
                if (value.Value < 150)
                {
                    IsSignalsPaneOpen = false;
                }
                else
                {
                    _signalsPaneColumnWidth = value;
                }
            }
            this.RaisePropertyChanged(nameof(SignalsPaneColumnWidth));
        }
    }

    private bool _isToolbarVisible = true;
    public bool IsToolbarVisible
    {
        get => _isToolbarVisible;
        set => this.RaiseAndSetIfChanged(ref _isToolbarVisible, value);
    }

    public ObservableCollection<SignalItemViewModel> AvailableSignals { get; } = [];
    public ObservableCollection<SignalItemViewModel> RegularSignals { get; } = [];
    public ObservableCollection<dynamic> DecodedRecords { get; } = [];
    public ObservableCollection<RecentFileViewModel> RecentFiles { get; } = [];
    public ObservableCollection<DerivedSignalDefinition> DerivedSignals { get; } = [];

    // Playback properties
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
                
                // Update virtual elapsed time for the new position
                if (TotalRecords > 1 && _fullDuration > 0)
                {
                    _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
                }
                else
                {
                    _savedElapsedSeconds = 0;
                }

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

    private readonly IDataStore _dataStore;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ISettingsService _settingsService;
    private readonly SessionManager _sessionManager = new();
    private readonly Core.DerivedSignals.FormulaEngine _formulaEngine = new();

    public Action<List<DateTime>, Dictionary<string, List<double>>, DateTime?>? RequestPlotUpdate { get; set; }
    public Action<DateTime?>? RequestCursorUpdate { get; set; }

    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }
    public ReactiveCommand<string, Unit> OpenRecentFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSessionCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSessionCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseAllCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportCsvCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSignalsPaneCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleToolbarCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateSchemaCommand { get; }
    public ReactiveCommand<Unit, Unit> EditSchemaCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateDerivedSignalCommand { get; }
    public ReactiveCommand<string, Unit> EditDerivedSignalCommand { get; }
    public ReactiveCommand<string, Unit> RemoveDerivedSignalCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
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

    public MainWindowViewModel() : this(null!, null!, null!, null!)
    {
    }

    public MainWindowViewModel(IDataStore dataStore, ILogger<MainWindowViewModel> logger, ILoggerFactory loggerFactory, ISettingsService settingsService)
    {
        _dataStore = dataStore;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _settingsService = settingsService;

        if (!Design.IsDesignMode)
        {
            RefreshRecentFiles();
        }

        OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFileAsync);
        OpenRecentFileCommand = ReactiveCommand.CreateFromTask<string>(path => LoadTelemetryFileAsync(path));
        
        var canExecuteSession = this.WhenAnyValue(x => x.HasData);
        SaveSessionCommand = ReactiveCommand.CreateFromTask(SaveSessionAsync, canExecuteSession);
        OpenSessionCommand = ReactiveCommand.CreateFromTask(OpenSessionAsync);
        CloseAllCommand = ReactiveCommand.Create(CloseAll, canExecuteSession);
        
        ExportCsvCommand = ReactiveCommand.Create(ExportCsv, canExecuteSession);
        ToggleSignalsPaneCommand = ReactiveCommand.Create(() => { IsSignalsPaneOpen = !IsSignalsPaneOpen; });
        ToggleToolbarCommand = ReactiveCommand.Create(() => { IsToolbarVisible = !IsToolbarVisible; });
        
        CreateSchemaCommand = ReactiveCommand.CreateFromTask(CreateSchemaAsync);
        
        var canEditSchema = this.WhenAnyValue(x => x.SelectedSchema, (PacketSchema? s) => s != null);
        EditSchemaCommand = ReactiveCommand.CreateFromTask(EditSchemaAsync, canEditSchema);
        
        var canCreateDerived = this.WhenAnyValue(x => x.AvailableSignals.Count, count => count > 0);
        CreateDerivedSignalCommand = ReactiveCommand.CreateFromTask(CreateDerivedSignalAsync, canCreateDerived);
        
        EditDerivedSignalCommand = ReactiveCommand.CreateFromTask<string>(EditDerivedSignalAsync);
        RemoveDerivedSignalCommand = ReactiveCommand.CreateFromTask<string>(RemoveDerivedSignalAsync);
        
        OpenSettingsCommand = ReactiveCommand.CreateFromTask(OpenSettingsAsync);
        OpenAboutCommand = ReactiveCommand.CreateFromTask(OpenAboutAsync);
        ExitCommand = ReactiveCommand.Create(() =>
        {
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
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

        AvailableSignals.CollectionChanged += (s, e) =>
        {
            this.RaisePropertyChanged(nameof(HasData));
            if (e.OldItems != null)
            {
                foreach (SignalItemViewModel item in e.OldItems)
                {
                    item.PropertyChanged -= SignalItem_PropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (SignalItemViewModel item in e.NewItems)
                {
                    item.PropertyChanged += SignalItem_PropertyChanged;
                }
            }
        };
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        int i = 1;
        foreach (var path in _settingsService.Current.RecentFiles)
        {
            RecentFiles.Add(new RecentFileViewModel { Index = i++, Path = path });
        }
    }

    private void AddToRecentFiles(string path)
    {
        var list = _settingsService.Current.RecentFiles;
        if (list.Contains(path))
        {
            list.Remove(path);
        }
        list.Insert(0, path);
        if (list.Count > _settingsService.Current.MaxRecentFiles)
        {
            list.RemoveAt(list.Count - 1);
        }

        _settingsService.Save();
        RefreshRecentFiles();
    }

    private void CloseAll()
    {
        // Stop playback if running
        IsPlaying = false;
        _playbackStopwatch = null;
        _playbackTimer?.Stop();
        _playbackTimer?.Dispose();
        _playbackTimer = null;
        
        // Reset playback state
        _playbackTimestamps = [];
        _playbackSignalData = [];
        _currentPlaybackIndex = 0;
        _savedElapsedSeconds = 0;
        _playbackProgressValue = 0;
        _totalRecords = 0;
        
        // Clear data store (don't dispose - just reset)
        try { _dataStore.Reset(Path.Combine(Path.GetTempPath(), "signalbench_temp.db")); } catch { }
        
        // Clear UI state
        AvailableSignals.Clear();
        RegularSignals.Clear();
        DerivedSignals.Clear();
        SelectedSchema = null;
        _currentTelemetryPath = null;
        _currentSchemaPath = null;
        StatusText = "Ready";
        
        // Reset plot to pristine state
        RequestPlotUpdate?.Invoke([], [], null);
        
        // Raise all property changes
        this.RaisePropertyChanged(nameof(HasData));
        this.RaisePropertyChanged(nameof(TotalRecords));
        this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
        this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
        this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        this.RaisePropertyChanged(nameof(PlaybackProgress));
    }

    private async Task ShowError(string title, string message, Exception? ex = null)
    {
        if (ex != null)
        {
            _logger.LogError(ex, "{Title}: {Message}", title, message);
        }
        else
        {
            _logger.LogError("{Title}: {Message}", title, message);
        }

        var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(title, message);

        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel != null)
        {
            await box.ShowWindowDialogAsync(topLevel);
        }
    }

    private async Task OpenSettingsAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var dialog = new SignalBench.Views.SettingsWindow
            {
                DataContext = new SettingsViewModel(_settingsService)
            };

            await dialog.ShowDialog(topLevel);
        }
        catch (Exception ex)
        {
            await ShowError("Settings Error", "Failed to open settings.", ex);
        }
    }

    private async Task EditSchemaAsync()
    {
        try
        {
            if (SelectedSchema == null) return;

            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var dialog = new SignalBench.Views.SchemaEditor
            {
                DataContext = new SchemaEditorViewModel(SelectedSchema)
            };

            var result = await dialog.ShowDialog<SchemaEditorResult?>(topLevel);
            if (result != null)
            {
                SelectedSchema = result.Schema;
                if (!string.IsNullOrEmpty(result.FilePath))
                {
                    _currentSchemaPath = result.FilePath;
                }
            }
        }
        catch (Exception ex)
        {
            await ShowError("Editor Error", "Failed to open schema editor.", ex);
        }
    }

    private async Task CreateSchemaAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var dialog = new SignalBench.Views.SchemaEditor
            {
                DataContext = new SchemaEditorViewModel()
            };

            await dialog.ShowDialog<SchemaEditorResult?>(topLevel);
        }
        catch (Exception ex)
        {
            await ShowError("Editor Error", "Failed to open schema editor.", ex);
        }
    }

    private async Task CreateDerivedSignalAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var availableFields = AvailableSignals.Select(s => s.Name).ToList();
            var dialog = new SignalBench.Views.DerivedSignalDialog
            {
                DataContext = new DerivedSignalViewModel(availableFields)
            };

            var result = await dialog.ShowDialog<DerivedSignalResult?>(topLevel);
            if (result != null)
            {
                var derivedSignal = new DerivedSignalDefinition
                {
                    Name = result.Name,
                    Formula = result.Formula
                };

                DerivedSignals.Add(derivedSignal);

                var signalData = ComputeDerivedSignal(derivedSignal);
                _dataStore.InsertDerivedSignal(result.Name, signalData);

                AvailableSignals.Add(new SignalItemViewModel { Name = result.Name, IsSelected = true, IsDerived = true });
                UpdatePlot();
                StatusText = $"Created derived signal: {result.Name}";
            }
        }
        catch (Exception ex)
        {
            await ShowError("Derived Signal Error", "Failed to create derived signal.", ex);
        }
    }

    private async Task EditDerivedSignalAsync(string signalName)
    {
        var existingSignal = DerivedSignals.FirstOrDefault(d => d.Name == signalName);
        if (existingSignal == null) return;
        
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var availableFields = AvailableSignals.Where(s => !s.IsDerived || s.Name == signalName).Select(s => s.Name).ToList();
            var dialog = new SignalBench.Views.DerivedSignalDialog
            {
                DataContext = new DerivedSignalViewModel(availableFields, existingSignal) { IsEditMode = true }
            };

            var result = await dialog.ShowDialog<DerivedSignalResult?>(topLevel);
            if (result != null && result.IsDeleted)
            {
                DerivedSignals.Remove(existingSignal);
                
                var signalItem = AvailableSignals.FirstOrDefault(s => s.Name == signalName);
                if (signalItem != null)
                {
                    AvailableSignals.Remove(signalItem);
                }

                _dataStore.DeleteSignal(signalName);
                UpdatePlot();
                StatusText = $"Removed derived signal: {signalName}";
            }
            else if (result != null)
            {
                var oldName = existingSignal.Name;
                existingSignal.Name = result.Name;
                existingSignal.Formula = result.Formula;

                if (oldName != result.Name)
                {
                    var signalItem = AvailableSignals.FirstOrDefault(s => s.Name == oldName);
                    if (signalItem != null)
                    {
                        signalItem.Name = result.Name;
                    }
                }

                var signalData = ComputeDerivedSignal(existingSignal);
                _dataStore.DeleteSignal(oldName);
                _dataStore.InsertDerivedSignal(result.Name, signalData);

                UpdatePlot();
                StatusText = $"Updated derived signal: {result.Name}";
            }
        }
        catch (Exception ex)
        {
            await ShowError("Derived Signal Error", "Failed to edit derived signal.", ex);
        }
    }

    private async Task RemoveDerivedSignalAsync(string signalName)
    {
        var signal = DerivedSignals.FirstOrDefault(d => d.Name == signalName);
        if (signal == null) return;
        
        try
        {
            DerivedSignals.Remove(signal);
            
            var signalItem = AvailableSignals.FirstOrDefault(s => s.Name == signalName);
            if (signalItem != null)
            {
                AvailableSignals.Remove(signalItem);
            }

            _dataStore.DeleteSignal(signalName);
            UpdatePlot();
            StatusText = $"Removed derived signal: {signalName}";
        }
        catch (Exception ex)
        {
            await ShowError("Derived Signal Error", "Failed to remove derived signal.", ex);
        }
    }

    private List<double> ComputeDerivedSignal(DerivedSignalDefinition derived)
    {
        var result = new List<double>();
        var timestamps = _dataStore.GetTimestamps();
        var signalCount = timestamps.Count;

        var availableSignals = AvailableSignals.Where(s => DerivedSignals.All(d => d.Name != s.Name)).ToList();

        for (int i = 0; i < signalCount; i++)
        {
            var parameters = new Dictionary<string, object>();
            foreach (var signal in availableSignals)
            {
                var data = _dataStore.GetSignalData(signal.Name);
                if (i < data.Count)
                {
                    parameters[signal.Name] = data[i];
                }
            }

            try
            {
                var value = _formulaEngine.Evaluate(derived.Formula, parameters);
                result.Add(value);
            }
            catch
            {
                result.Add(double.NaN);
            }
        }

        return result;
    }

    private void SignalItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SignalItemViewModel.IsSelected))
            UpdatePlot();
    }

    private void UpdatePlot()
    {
        try
        {
            _totalRecords = _dataStore.GetRowCount();
            this.RaisePropertyChanged(nameof(TotalRecords));
            
            var maxPlotPoints = 10000;
            var shouldDownsample = _totalRecords > maxPlotPoints;
            
            var timestamps = _dataStore.GetTimestamps(shouldDownsample ? maxPlotPoints : null);
            var selectedSignals = AvailableSignals.Where(s => s.IsSelected).ToList();
            var plotData = new Dictionary<string, List<double>>();
            foreach (var signal in selectedSignals)
            {
                plotData[signal.Name] = _dataStore.GetSignalData(signal.Name, shouldDownsample ? maxPlotPoints : null);
            }
            RequestPlotUpdate?.Invoke(timestamps, plotData, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plot Error");
            StatusText = $"Plot Error: {ex.Message}";
        }
    }

    private void PlayPause()
    {
        if (IsPlaying)
        {
            StopPlayback();
        }
        else
        {
            StartPlayback();
        }
    }

    private void StartPlayback()
    {
        if (TotalRecords == 0) return;

        lock (_playbackLock)
        {
            // Calculate elapsed time based on current position
            if (TotalRecords > 1)
            {
                var firstTs = _dataStore.GetTimestamp(0);
                var lastTs = _dataStore.GetTimestamp(TotalRecords - 1);
                _fullDuration = (lastTs - firstTs).TotalSeconds;
                if (_fullDuration < 0.1) _fullDuration = TotalRecords - 1;
                
                _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            }
            else
            {
                _fullDuration = 0;
                _savedElapsedSeconds = 0;
            }

            if (_currentPlaybackIndex >= TotalRecords - 1)
            {
                _currentPlaybackIndex = 0;
                _savedElapsedSeconds = 0;
                _playbackProgressValue = 0;
                this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
                this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
                this.RaisePropertyChanged(nameof(PlaybackProgress));
            }

            // Cache data for playback only if not already cached (for resume)
            if (_playbackTimestamps.Count == 0)
            {
                var maxPlaybackPoints = 10000; // Downsample for smooth playback
                _playbackTimestamps = _dataStore.GetTimestamps(maxPlaybackPoints);
                
                _playbackSignalData = [];
                foreach (var signal in AvailableSignals.Where(s => s.IsSelected))
                {
                    var data = _dataStore.GetSignalData(signal.Name, maxPlaybackPoints);
                    if (data.Count == _playbackTimestamps.Count)
                    {
                        _playbackSignalData[signal.Name] = data;
                    }
                }
            }

            IsPlaying = true;
            _playbackStopwatch = System.Diagnostics.Stopwatch.StartNew();
            _playbackTimer?.Stop();
            _playbackTimer?.Dispose();
            
            _playbackTimer = new System.Timers.Timer(100);
            _playbackTimer.Elapsed += PlaybackTimer_Elapsed;
            _playbackTimer.Start();
        }

        UpdatePlaybackView();
    }

    private void StopPlayback()
    {
        lock (_playbackLock)
        {
            IsPlaying = false;
            if (_playbackStopwatch != null)
            {
                _savedElapsedSeconds += _playbackStopwatch.Elapsed.TotalSeconds * PlaybackSpeed;
                _playbackStopwatch.Stop();
            }
            _playbackStopwatch = null;
            _playbackTimer?.Stop();
            _playbackTimer?.Dispose();
            _playbackTimer = null;
        }
    }

    private System.Diagnostics.Stopwatch? _playbackStopwatch;

    private void PlaybackTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        lock (_playbackLock)
        {
            if (!IsPlaying || _playbackStopwatch == null) 
            {
                StopPlayback();
                return;
            }

            if (TotalRecords <= 1 || _fullDuration <= 0)
            {
                StopPlayback();
                return;
            }

            var timestamps = _playbackTimestamps;
            if (timestamps.Count == 0 || _currentPlaybackIndex >= TotalRecords - 1)
            {
                _currentPlaybackIndex = TotalRecords - 1;
                _playbackTimestamps = [];
                _playbackSignalData = [];
                _savedElapsedSeconds = 0;
                _fullDuration = 0;
                StopPlayback();
                _playbackProgressValue = 100;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
                    this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                    this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
                    this.RaisePropertyChanged(nameof(PlaybackProgress));
                    UpdatePlaybackView();
                });
                return;
            }

            // targetSec is the "virtual" elapsed seconds (at 1x speed)
            var elapsedSinceLastCommit = _playbackStopwatch.Elapsed.TotalSeconds;
            var targetSec = _savedElapsedSeconds + (elapsedSinceLastCommit * PlaybackSpeed);
            
            var progress = Math.Min(targetSec / _fullDuration, 1.0);
            var newFullIndex = (int)(progress * (TotalRecords - 1));
            
            newFullIndex = Math.Clamp(newFullIndex, 0, TotalRecords - 1);
            
            if (newFullIndex == _currentPlaybackIndex)
            {
                return;
            }
            
            _currentPlaybackIndex = newFullIndex;
            _playbackProgressValue = TotalRecords > 1 ? (double)newFullIndex / (TotalRecords - 1) * 100 : 0;
            
            // Sync _savedElapsedSeconds to the position we just "committed" via index update
            _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            _playbackStopwatch.Restart();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
                this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
                this.RaisePropertyChanged(nameof(PlaybackProgress));
                
                UpdateCursorPosition();
            });
            
            if (_currentPlaybackIndex >= TotalRecords - 1)
            {
                StopPlayback();
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
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
        if (TotalRecords == 0) return;

        DateTime currentTime;
        if (_playbackTimestamps.Count > 0)
        {
            int mappedIndex = (int)((double)_currentPlaybackIndex / TotalRecords * _playbackTimestamps.Count);
            mappedIndex = Math.Clamp(mappedIndex, 0, _playbackTimestamps.Count - 1);
            currentTime = _playbackTimestamps[mappedIndex];
        }
        else
        {
            currentTime = _dataStore.GetTimestamp(_currentPlaybackIndex);
        }

        CursorPosition = currentTime;
        RequestCursorUpdate?.Invoke(currentTime);
    }

    private void UpdatePlaybackView()
    {
        try
        {
            // If no cached data, fetch directly from data store
            if (_playbackTimestamps.Count == 0)
            {
                var maxPoints = 10000;
                _playbackTimestamps = _dataStore.GetTimestamps(maxPoints);
                _playbackSignalData = [];
                foreach (var signal in AvailableSignals.Where(s => s.IsSelected))
                {
                    var data = _dataStore.GetSignalData(signal.Name, maxPoints);
                    if (data.Count == _playbackTimestamps.Count)
                    {
                        _playbackSignalData[signal.Name] = data;
                    }
                }
            }
            
            if (_playbackTimestamps.Count == 0) 
            {
                StopPlayback();
                return;
            }

            int mappedIndex = (int)((double)_currentPlaybackIndex / TotalRecords * _playbackTimestamps.Count);
            mappedIndex = Math.Clamp(mappedIndex, 0, _playbackTimestamps.Count - 1);

            var currentTime = _playbackTimestamps[mappedIndex];
            CursorPosition = currentTime;

            // Show full dataset (not zoomed in) - just with cursor
            // Use cached data
            RequestPlotUpdate?.Invoke(_playbackTimestamps, _playbackSignalData, currentTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playback Error");
            StopPlayback();
        }
    }

    private void SetSpeed(string speedStr)
    {
        var newSpeed = double.Parse(speedStr.Replace("x", ""));
        
        lock (_playbackLock)
        {
            if (IsPlaying && _playbackStopwatch != null)
            {
                // "Commit" the virtual elapsed seconds at the OLD speed before changing to the new one
                _savedElapsedSeconds += _playbackStopwatch.Elapsed.TotalSeconds * PlaybackSpeed;
                _playbackStopwatch.Restart();
            }
            
            PlaybackSpeed = newSpeed;
            this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
            this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        }
    }

    private void Seek(double progress)
    {
        var newIndex = (int)(TotalRecords * progress / 100.0);
        newIndex = Math.Clamp(newIndex, 0, Math.Max(0, TotalRecords - 1));
        
        lock (_playbackLock)
        {
            _currentPlaybackIndex = newIndex;
            _playbackProgressValue = progress;

            // Reset saved elapsed time to the new position's virtual 1x time
            if (TotalRecords > 1 && _fullDuration > 0)
            {
                _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            }
            else
            {
                _savedElapsedSeconds = 0;
            }

            if (IsPlaying && _playbackStopwatch != null)
            {
                _playbackStopwatch.Restart();
            }
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
        
        lock (_playbackLock)
        {
            _currentPlaybackIndex = Math.Min(_currentPlaybackIndex + 1, TotalRecords - 1);
            _playbackProgressValue = TotalRecords > 1 ? (double)_currentPlaybackIndex / (TotalRecords - 1) * 100 : 0;
            
            // Update virtual elapsed time
            if (TotalRecords > 1 && _fullDuration > 0)
            {
                _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            }
            else
            {
                _savedElapsedSeconds = 0;
            }

            if (IsPlaying && _playbackStopwatch != null)
            {
                _playbackStopwatch.Restart();
            }
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
        
        lock (_playbackLock)
        {
            _currentPlaybackIndex = Math.Max(_currentPlaybackIndex - 1, 0);
            _playbackProgressValue = TotalRecords > 1 ? (double)_currentPlaybackIndex / (TotalRecords - 1) * 100 : 0;

            // Update virtual elapsed time
            if (TotalRecords > 1 && _fullDuration > 0)
            {
                _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            }
            else
            {
                _savedElapsedSeconds = 0;
            }

            if (IsPlaying && _playbackStopwatch != null)
            {
                _playbackStopwatch.Restart();
            }
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
        
        lock (_playbackLock)
        {
            var step = Math.Max(1, TotalRecords / 100);
            _currentPlaybackIndex = Math.Min(_currentPlaybackIndex + step, TotalRecords - 1);
            _playbackProgressValue = TotalRecords > 1 ? (double)_currentPlaybackIndex / (TotalRecords - 1) * 100 : 0;
            
            // Update virtual elapsed time
            if (TotalRecords > 1 && _fullDuration > 0)
            {
                _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            }

            if (IsPlaying && _playbackStopwatch != null)
            {
                _playbackStopwatch.Restart();
            }
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
        
        lock (_playbackLock)
        {
            var step = Math.Max(1, TotalRecords / 100);
            _currentPlaybackIndex = Math.Max(_currentPlaybackIndex - step, 0);
            _playbackProgressValue = TotalRecords > 1 ? (double)_currentPlaybackIndex / (TotalRecords - 1) * 100 : 0;

            // Update virtual elapsed time
            if (TotalRecords > 1 && _fullDuration > 0)
            {
                _savedElapsedSeconds = (_fullDuration / (TotalRecords - 1)) * _currentPlaybackIndex;
            }
            else
            {
                _savedElapsedSeconds = 0;
            }

            if (IsPlaying && _playbackStopwatch != null)
            {
                _playbackStopwatch.Restart();
            }
        }

        this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
        this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
        this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        this.RaisePropertyChanged(nameof(PlaybackProgress));
        
        UpdateCursorPosition();
    }

    private void Restart()
    {
        if (TotalRecords == 0) return;
        
        _currentPlaybackIndex = 0;
        _savedElapsedSeconds = 0;
        
        if (TotalRecords > 1)
        {
            var firstTs = _dataStore.GetTimestamp(0);
            var lastTs = _dataStore.GetTimestamp(TotalRecords - 1);
            _fullDuration = (lastTs - firstTs).TotalSeconds;
            if (_fullDuration < 0.1) _fullDuration = TotalRecords - 1;
        }
        else
        {
            _fullDuration = 0;
        }

        _playbackProgressValue = 0;
        
        // Re-cache data
        var maxPoints = 10000;
        _playbackTimestamps = _dataStore.GetTimestamps(maxPoints);
        _playbackSignalData = [];
        foreach (var signal in AvailableSignals.Where(s => s.IsSelected))
        {
            var data = _dataStore.GetSignalData(signal.Name, maxPoints);
            if (data.Count == _playbackTimestamps.Count)
            {
                _playbackSignalData[signal.Name] = data;
            }
        }
        
        this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
        this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
        this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
        this.RaisePropertyChanged(nameof(PlaybackProgress));
        
        UpdatePlaybackView();
    }

    private async Task OpenFileAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open Telemetry File",
                AllowMultiple = false,
                FileTypeFilter = [
                    new Avalonia.Platform.Storage.FilePickerFileType("Telemetry Files") { Patterns = ["*.csv", "*.bin", "*.dat"] }
                ]
            });

            if (files.Count > 0)
            {
                await LoadTelemetryFileAsync(files[0].Path.LocalPath);
            }
        }
        catch (Exception ex)
        {
            await ShowError("File Error", "Could not select file.", ex);
        }
    }

    private async Task LoadTelemetryFileAsync(string path, string? schemaPath = null)
    {
        _currentTelemetryPath = path;
        _currentSchemaPath = schemaPath;

        if (path.EndsWith(".csv"))
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var dialog = new SignalBench.Views.CsvImport
            {
                DataContext = new CsvImportViewModel(path)
            };
            var result = await dialog.ShowDialog<CsvImportResult?>(topLevel);
            if (result == null) return;

            StatusText = $"Loading {path}...";
            var startTime = DateTime.Now;
            
            await Task.Run(async () =>
            {
                try
                {
                    // Count lines for progress
                    var lineCount = File.ReadLines(path).Count();
                    
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        IsLoading = true;
                        LoadProgress = 0;
                        LoadElapsed = "00:00";
                    });

                    var dbPath = Path.Combine(Path.GetTempPath(), "signalbench_temp.db");
                    _dataStore.Reset(dbPath);

                    var source = new SignalBench.Core.Ingestion.CsvTelemetrySource(path, result.Delimiter, result.TimestampColumn);
                    var packets = new List<DecodedPacket>();
                    var processed = 0;
                    var lastUpdate = DateTime.Now;

                    foreach (var packet in source.ReadPackets())
                    {
                        packets.Add(packet);
                        processed++;
                        
                        // Update progress every 100ms
                        if ((DateTime.Now - lastUpdate).TotalMilliseconds > 100)
                        {
                            var elapsed = DateTime.Now - startTime;
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                LoadProgress = (double)processed / lineCount * 100;
                                LoadElapsed = elapsed.ToString(@"mm\:ss");
                            });
                            lastUpdate = DateTime.Now;
                            await Task.Delay(1); // Allow UI refresh
                        }
                    }

                    if (packets.Count > 0)
                    {
                        var fields = new List<string>(packets[0].Fields.Keys);
                        var schema = new PacketSchema { Name = "CSV Import" };
                        foreach (var field in fields)
                            schema.Fields.Add(new FieldDefinition { Name = field });

                        _dataStore.InitializeSchema(schema);
                        _dataStore.InsertPackets(packets);

                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            // Reset playback state for new file
                            _currentPlaybackIndex = 0;
                            _playbackProgressValue = 0;
                            _savedElapsedSeconds = 0;
                            _playbackTimestamps = [];
                            _playbackSignalData = [];

                            AvailableSignals.Clear();
                            RegularSignals.Clear();
                            DerivedSignals.Clear();
                            foreach (var field in fields)
                            {
                                if (field.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) continue;
                                bool shouldSelect = RegularSignals.Count < 3;
                                var signalItem = new SignalItemViewModel { Name = field, IsSelected = shouldSelect };
                                AvailableSignals.Add(signalItem);
                                RegularSignals.Add(signalItem);
                            }
                            SelectedSchema = schema;
                            AddToRecentFiles(path);
                            UpdatePlot();
                            IsLoading = false;

                            this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
                            this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                            this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
                            this.RaisePropertyChanged(nameof(PlaybackProgress));

                            var elapsed = DateTime.Now - startTime;
                            StatusText = $"Loaded {packets.Count:N0} records in {elapsed.TotalSeconds:F1}s";
                        });
                    }
                }
                catch (Exception ex)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => IsLoading = false);
                    await ShowError("Load Error", $"Failed to load CSV telemetry from {Path.GetFileName(path)}.", ex);
                }
            });
        }
        else
        {
            PacketSchema? schema = null;
            if (!string.IsNullOrEmpty(schemaPath))
            {
                try
                {
                    var yaml = await File.ReadAllTextAsync(schemaPath);
                    schema = new SchemaLoader().Load(yaml);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load schema from session path: {Path}", schemaPath);
                }
            }

            if (schema == null)
            {
                schema = await PromptForSchemaAsync(path);
            }

            if (schema == null) return;

            StatusText = $"Loading {path}...";
            await Task.Run(async () =>
            {
                try
                {
                    var dbPath = Path.Combine(Path.GetTempPath(), "signalbench_temp.db");
                    _dataStore.Reset(dbPath);

                    _dataStore.InitializeSchema(schema);
                    var source = new SignalBench.Core.Ingestion.BinaryTelemetrySource(path, schema);
                    var packets = source.ReadPackets().ToList();
                    _dataStore.InsertPackets(packets);
                    var fields = new List<string>(schema.Fields.Select(f => f.Name));

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        // Reset playback state for new file
                        _currentPlaybackIndex = 0;
                        _playbackProgressValue = 0;
                        _savedElapsedSeconds = 0;
                        _playbackTimestamps = [];
                        _playbackSignalData = [];

                        AvailableSignals.Clear();
                        RegularSignals.Clear();
                        DerivedSignals.Clear();
                        foreach (var field in fields)
                        {
                            if (field.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) continue;
                            bool shouldSelect = RegularSignals.Count < 3;
                            var signalItem = new SignalItemViewModel { Name = field, IsSelected = shouldSelect };
                            AvailableSignals.Add(signalItem);
                            RegularSignals.Add(signalItem);
                        }
                        SelectedSchema = schema;
                        AddToRecentFiles(path);
                        UpdatePlot();

                        this.RaisePropertyChanged(nameof(CurrentPlaybackIndex));
                        this.RaisePropertyChanged(nameof(CurrentPlaybackTime));
                        this.RaisePropertyChanged(nameof(FormattedPlaybackTime));
                        this.RaisePropertyChanged(nameof(PlaybackProgress));

                        StatusText = $"Loaded {packets.Count} records from {Path.GetFileName(path)}";
                    });
                }
                catch (Exception ex)
                {
                    await ShowError("Load Error", $"Failed to load binary telemetry from {Path.GetFileName(path)}.", ex);
                }
            });
        }
    }

    private async Task<PacketSchema?> PromptForSchemaAsync(string telemetryPath)
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return null;

            var dialog = new BinaryImport
            {
                DataContext = new BinaryImportViewModel(telemetryPath, _loggerFactory.CreateLogger<BinaryImportViewModel>())
            };
            return await dialog.ShowDialog<PacketSchema?>(topLevel);
        }
        catch (Exception ex)
        {
            await ShowError("Import Error", "Failed to open import dialog.", ex);
            return null;
        }
    }

    private async Task OpenAboutAsync()
    {
        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel == null) return;

        var dialog = new SignalBench.Views.AboutWindow
        {
            DataContext = this
        };
        
        var versionText = dialog.FindControl<TextBlock>("VersionText");
        if (versionText != null) versionText.Text = $"Version {AppInfo.Version}";
        
        var taglineText = dialog.FindControl<TextBlock>("TaglineText");
        if (taglineText != null) taglineText.Text = AppInfo.Tagline;
        
        var descText = dialog.FindControl<TextBlock>("DescriptionText");
        if (descText != null) descText.Text = AppInfo.Description;
        
        var copyText = dialog.FindControl<TextBlock>("CopyrightText");
        if (copyText != null) copyText.Text = AppInfo.Copyright;

        await dialog.ShowDialog(topLevel);
    }

    private async Task SaveSessionAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save Session",
                DefaultExtension = "sbs",
                FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("SignalBench Session") { Patterns = ["*.sbs"] }]
            });

            if (file != null)
            {
                var session = new ProjectSession
                {
                    TelemetryFilePath = _currentTelemetryPath ?? string.Empty,
                    SchemaPath = _currentSchemaPath ?? string.Empty,
                    ActivePlotSignals = AvailableSignals.Where(s => s.IsSelected).Select(s => s.Name).ToList(),
                    DerivedSignals = DerivedSignals.ToList()
                };
                _sessionManager.SaveSession(file.Path.LocalPath, session);
                StatusText = $"Session saved to {file.Name}";
            }
        }
        catch (Exception ex)
        {
            await ShowError("Session Error", "Failed to save session.", ex);
        }
    }

    private async Task OpenSessionAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open Session",
                AllowMultiple = false,
                FileTypeFilter = [
                    new Avalonia.Platform.Storage.FilePickerFileType("SignalBench Session") { Patterns = ["*.sbs"] }
                ]
            });

            if (files.Count > 0)
            {
                var session = _sessionManager.LoadSession(files[0].Path.LocalPath);
                if (File.Exists(session.TelemetryFilePath))
                {
                    await LoadTelemetryFileAsync(session.TelemetryFilePath, session.SchemaPath);

                    // Restore active signals after load
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var signal in AvailableSignals)
                        {
                            signal.IsSelected = session.ActivePlotSignals.Contains(signal.Name);
                        }

                        // Restore derived signals
                        foreach (var derivedSignal in session.DerivedSignals)
                        {
                            DerivedSignals.Add(derivedSignal);
                            var signalData = ComputeDerivedSignal(derivedSignal);
                            _dataStore.InsertDerivedSignal(derivedSignal.Name, signalData);
                            AvailableSignals.Add(new SignalItemViewModel { Name = derivedSignal.Name, IsSelected = session.ActivePlotSignals.Contains(derivedSignal.Name), IsDerived = true });
                        }

                        UpdatePlot();
                    }, Avalonia.Threading.DispatcherPriority.Loaded);

                    StatusText = $"Session loaded from {files[0].Name}";
                }
                else
                {
                    await ShowError("Session Error", "Telemetry file referenced in session not found.");
                }
            }
        }
        catch (Exception ex)
        {
            await ShowError("Session Error", "Failed to load session.", ex);
        }
    }

    private async void ExportCsv()
    {
        if (_dataStore == null) return;

        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Export Decoded Data",
                DefaultExtension = "csv",
                FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("CSV Files") { Patterns = ["*.csv"] }]
            });

            if (file != null)
            {
                StatusText = "Exporting...";
                await Task.Run(() =>
                {
                    using var writer = new StreamWriter(file.Path.LocalPath);
                    var selectedSignals = AvailableSignals.Where(s => s.IsSelected).ToList();
                    if (selectedSignals.Count == 0) selectedSignals = [.. AvailableSignals];

                    // Header
                    writer.WriteLine(string.Join(",", selectedSignals.Select(s => s.Name)));

                    var allData = selectedSignals.Select(s => _dataStore.GetSignalData(s.Name)).ToList();
                    if (allData.Count > 0)
                    {
                        int rowCount = allData[0].Count;
                        for (int i = 0; i < rowCount; i++)
                        {
                            writer.WriteLine(string.Join(",", allData.Select(d => d[i])));
                        }
                    }
                });
                StatusText = "Export complete.";
            }
        }
        catch (Exception ex)
        {
            await ShowError("Export Error", "Failed to export CSV.", ex);
        }
    }
}
