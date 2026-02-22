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
using System.Dynamic;
using System.Reactive;
using Avalonia.Controls;

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

    private string? _currentTelemetryPath;
    private string? _currentSchemaPath;

    private PacketSchema? _selectedSchema;
    public PacketSchema? SelectedSchema
    {
        get => _selectedSchema;
        set => this.RaiseAndSetIfChanged(ref _selectedSchema, value);
    }

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
    public ObservableCollection<dynamic> DecodedRecords { get; } = [];
    public ObservableCollection<RecentFileViewModel> RecentFiles { get; } = [];

    private readonly IDataStore _dataStore;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ISettingsService _settingsService;
    private readonly SessionManager _sessionManager = new();

    public Action<List<DateTime>, Dictionary<string, List<double>>>? RequestPlotUpdate { get; set; }

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
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenAboutCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    public MainWindowViewModel(IDataStore dataStore, ILogger<MainWindowViewModel> logger, ILoggerFactory loggerFactory, ISettingsService settingsService)
    {
        _dataStore = dataStore;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _settingsService = settingsService;

        RefreshRecentFiles();

        OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFileAsync);
        OpenRecentFileCommand = ReactiveCommand.CreateFromTask<string>(path => LoadTelemetryFileAsync(path));
        SaveSessionCommand = ReactiveCommand.CreateFromTask(SaveSessionAsync);
        OpenSessionCommand = ReactiveCommand.CreateFromTask(OpenSessionAsync);
        CloseAllCommand = ReactiveCommand.Create(CloseAll);
        ExportCsvCommand = ReactiveCommand.Create(ExportCsv);
        ToggleSignalsPaneCommand = ReactiveCommand.Create(() => { IsSignalsPaneOpen = !IsSignalsPaneOpen; });
        ToggleToolbarCommand = ReactiveCommand.Create(() => { IsToolbarVisible = !IsToolbarVisible; });
        
        CreateSchemaCommand = ReactiveCommand.CreateFromTask(CreateSchemaAsync);
        
        var canEditSchema = this.WhenAnyValue(x => x.SelectedSchema, (PacketSchema? s) => s != null);
        EditSchemaCommand = ReactiveCommand.CreateFromTask(EditSchemaAsync, canEditSchema);
        
        OpenSettingsCommand = ReactiveCommand.CreateFromTask(OpenSettingsAsync);
        OpenAboutCommand = ReactiveCommand.CreateFromTask(OpenAboutAsync);
        ExitCommand = ReactiveCommand.Create(() =>
        {
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        });

        AvailableSignals.CollectionChanged += (s, e) =>
        {
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
        _dataStore.Dispose();
        AvailableSignals.Clear();
        SelectedSchema = null;
        _currentTelemetryPath = null;
        _currentSchemaPath = null;
        StatusText = "Ready";
        RequestPlotUpdate?.Invoke([], []);
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

    private void SignalItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SignalItemViewModel.IsSelected))
            UpdatePlot();
    }

    private void UpdatePlot()
    {
        try
        {
            var timestamps = _dataStore.GetTimestamps();
            var selectedSignals = AvailableSignals.Where(s => s.IsSelected).ToList();
            var plotData = new Dictionary<string, List<double>>();
            foreach (var signal in selectedSignals)
            {
                plotData[signal.Name] = _dataStore.GetSignalData(signal.Name);
            }
            RequestPlotUpdate?.Invoke(timestamps, plotData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plot Error");
            StatusText = $"Plot Error: {ex.Message}";
        }
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
                SuggestedStartLocation = !string.IsNullOrEmpty(_settingsService.Current.DefaultTelemetryPath)
                    ? await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(_settingsService.Current.DefaultTelemetryPath))
                    : null,
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
            await Task.Run(async () =>
            {
                try
                {
                    var dbPath = Path.Combine(Path.GetTempPath(), "signalbench_temp.db");
                    _dataStore.Reset(dbPath);

                    var source = new SignalBench.Core.Ingestion.CsvTelemetrySource(path, result.Delimiter, result.TimestampColumn);
                    var packets = source.ReadPackets().ToList();

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
                            AvailableSignals.Clear();
                            foreach (var field in fields)
                            {
                                if (field.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) continue;
                                bool shouldSelect = AvailableSignals.Count < 3;
                                AvailableSignals.Add(new SignalItemViewModel { Name = field, IsSelected = shouldSelect });
                            }
                            SelectedSchema = schema;
                            AddToRecentFiles(path);
                            UpdatePlot();
                            StatusText = $"Loaded {packets.Count} records from {Path.GetFileName(path)}";
                        });
                    }
                }
                catch (Exception ex)
                {
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
                        AvailableSignals.Clear();
                        foreach (var field in fields)
                        {
                            if (field.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) continue;
                            bool shouldSelect = AvailableSignals.Count < 3;
                            AvailableSignals.Add(new SignalItemViewModel { Name = field, IsSelected = shouldSelect });
                        }
                        SelectedSchema = schema;
                        AddToRecentFiles(path);
                        UpdatePlot();
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
        var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard($"About {AppInfo.Name}", $"{AppInfo.Name}\nVersion: {AppInfo.Version}\n\n{AppInfo.Copyright}\n\nA tool for high-performance telemetry analysis.");

        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel != null)
        {
            await box.ShowWindowDialogAsync(topLevel);
        }
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
                    ActivePlotSignals = AvailableSignals.Where(s => s.IsSelected).Select(s => s.Name).ToList()
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
