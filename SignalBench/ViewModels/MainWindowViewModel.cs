using ReactiveUI;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Reactive;

namespace SignalBench.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    private PacketSchema? _selectedSchema;
    public PacketSchema? SelectedSchema
    {
        get => _selectedSchema;
        set => this.RaiseAndSetIfChanged(ref _selectedSchema, value);
    }

    public ObservableCollection<PacketSchema> Schemas { get; } = [];
    public ObservableCollection<SignalItemViewModel> AvailableSignals { get; } = [];
    public ObservableCollection<dynamic> DecodedRecords { get; } = [];

    private SignalBench.Core.Data.SqliteDataStore? _dataStore;

    public Action<List<DateTime>, Dictionary<string, List<double>>>? RequestPlotUpdate { get; set; }

    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSessionCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportCsvCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadSchemaCommand { get; }

    public MainWindowViewModel()
    {
        OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFileAsync);
        SaveSessionCommand = ReactiveCommand.Create(SaveSession);
        ExportCsvCommand = ReactiveCommand.Create(ExportCsv);
        LoadSchemaCommand = ReactiveCommand.CreateFromTask(LoadSchemaAsync);

        AvailableSignals.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (SignalItemViewModel item in e.NewItems)
                {
                    item.PropertyChanged += (s2, e2) =>
                    {
                        if (e2.PropertyName == nameof(SignalItemViewModel.IsSelected))
                            UpdatePlot();
                    };
                }
            }
        };

        // Dummy data for V1 preview
        Schemas.Add(new PacketSchema { Name = "EPS Telemetry" });
    }

    private void UpdatePlot()
    {
        if (_dataStore == null) return;
        var timestamps = _dataStore.GetTimestamps();
        var selectedSignals = AvailableSignals.Where(s => s.IsSelected).ToList();
        var plotData = new Dictionary<string, List<double>>();
        foreach (var signal in selectedSignals)
        {
            plotData[signal.Name] = _dataStore.GetSignalData(signal.Name);
        }
        RequestPlotUpdate?.Invoke(timestamps, plotData);
    }

    private async Task OpenFileAsync()
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
            var path = files[0].Path.LocalPath;
            
            if (path.EndsWith(".csv"))
            {
                var dialog = new SignalBench.Views.CsvImport
                {
                    DataContext = new CsvImportViewModel(path)
                };
                var result = await dialog.ShowDialog<CsvImportResult?>(topLevel);
                if (result == null) return;

                StatusText = $"Loading {path}...";
                await Task.Run(() => {
                    try 
                    {
                        _dataStore?.Dispose();
                        var dbPath = Path.Combine(Path.GetTempPath(), "signalbench_temp.db");
                        if (File.Exists(dbPath)) File.Delete(dbPath);
                        _dataStore = new SignalBench.Core.Data.SqliteDataStore(dbPath);

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

                            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                AvailableSignals.Clear();
                                foreach (var field in fields)
                                {
                                    if (field.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) continue;
                                    bool shouldSelect = AvailableSignals.Count < 3;
                                    AvailableSignals.Add(new SignalItemViewModel { Name = field, IsSelected = shouldSelect });
                                }
                                UpdatePlot();
                                StatusText = $"Loaded {packets.Count} records from {Path.GetFileName(path)}";
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = $"Error: {ex.Message}");
                    }
                });
            }
            else
            {
                StatusText = $"Loading {path}...";
                await Task.Run(() => {
                    try 
                    {
                        _dataStore?.Dispose();
                        var dbPath = Path.Combine(Path.GetTempPath(), "signalbench_temp.db");
                        if (File.Exists(dbPath)) File.Delete(dbPath);
                        _dataStore = new SignalBench.Core.Data.SqliteDataStore(dbPath);

                        if (SelectedSchema == null)
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = "Please select a schema first for binary files.");
                            return;
                        }

                        _dataStore.InitializeSchema(SelectedSchema);
                        var source = new SignalBench.Core.Ingestion.BinaryTelemetrySource(path, SelectedSchema);
                        var packets = source.ReadPackets().ToList();
                        _dataStore.InsertPackets(packets);
                        var fields = new List<string>(SelectedSchema.Fields.Select(f => f.Name));

                        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                            AvailableSignals.Clear();
                            foreach (var field in fields)
                            {
                                if (field.Equals("timestamp", StringComparison.OrdinalIgnoreCase)) continue;
                                bool shouldSelect = AvailableSignals.Count < 3;
                                AvailableSignals.Add(new SignalItemViewModel { Name = field, IsSelected = shouldSelect });
                            }
                            UpdatePlot();
                            StatusText = $"Loaded {packets.Count} records from {Path.GetFileName(path)}";
                        });
                    }
                    catch (Exception ex)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = $"Error: {ex.Message}");
                    }
                });
            }
        }
    }

    private void SaveSession()
    {
        StatusText = "Session saved.";
    }

    private async void ExportCsv()
    {
        if (_dataStore == null) return;

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

    private async Task LoadSchemaAsync()
    {
        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Schema File",
            AllowMultiple = false,
            FileTypeFilter = [
                new Avalonia.Platform.Storage.FilePickerFileType("YAML Schema") { Patterns = ["*.yaml", "*.yml"] }
            ]
        });

        if (files.Count > 0)
        {
            var yaml = await File.ReadAllTextAsync(files[0].Path.LocalPath);
            var loader = new SignalBench.Core.Services.SchemaLoader();
            try
            {
                var schema = loader.Load(yaml);
                if (schema != null)
                {
                    SelectedSchema = schema;
                    if (!Schemas.Any(s => s.Name == schema.Name))
                    {
                        Schemas.Add(schema);
                    }
                    StatusText = $"Loaded schema: {schema.Name}";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading schema: {ex.Message}";
            }
        }
    }
}
