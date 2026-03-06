using Microsoft.Extensions.Logging;
using ReactiveUI;
using SignalBench.Core.Ingestion;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;
using System.Collections.ObjectModel;
using System.Reactive;

namespace SignalBench.ViewModels;

public class BinaryImportResult
{
    public string TelemetryPath { get; set; } = string.Empty;
    public PacketSchema? Schema { get; set; }
    public string? TimestampField { get; set; }
}

public class BinaryImportViewModel : ViewModelBase
{
    private PacketSchema? _selectedSchema;
    public PacketSchema? SelectedSchema
    {
        get => _selectedSchema;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSchema, value);
            LoadPreview();
            this.RaisePropertyChanged(nameof(IsImportEnabled));
        }
    }

    private string? _timestampField;
    public string? TimestampField
    {
        get => _timestampField;
        set => this.RaiseAndSetIfChanged(ref _timestampField, value);
    }

    private string _schemaFilePath = string.Empty;
    public string SchemaFilePath
    {
        get => _schemaFilePath;
        set => this.RaiseAndSetIfChanged(ref _schemaFilePath, value);
    }

    private bool _isPreviewLoaded;
    public bool IsPreviewLoaded
    {
        get => _isPreviewLoaded;
        set => this.RaiseAndSetIfChanged(ref _isPreviewLoaded, value);
    }

    private string _telemetryPath = string.Empty;
    public string TelemetryPath
    {
        get => _telemetryPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _telemetryPath, value);
            LoadPreview();
            this.RaisePropertyChanged(nameof(IsImportEnabled));
        }
    }

    public ObservableCollection<IDictionary<string, object>> PreviewRecords { get; } = [];
    public ObservableCollection<string> AvailableColumns { get; } = [];

    private readonly ILogger<BinaryImportViewModel> _logger;
    public bool IsImportEnabled => SelectedSchema != null && !string.IsNullOrEmpty(TelemetryPath) && IsPreviewLoaded;

    public ReactiveCommand<Unit, BinaryImportResult?> ImportCommand { get; }
    public ReactiveCommand<Unit, BinaryImportResult?> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseSchemaCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseTelemetryCommand { get; }

    public BinaryImportViewModel(string? telemetryPath, ILogger<BinaryImportViewModel> logger)
    {
        _telemetryPath = telemetryPath ?? string.Empty;
        _logger = logger;

        ImportCommand = ReactiveCommand.Create(() => (BinaryImportResult?)new BinaryImportResult
        {
            TelemetryPath = TelemetryPath,
            Schema = SelectedSchema,
            TimestampField = TimestampField
        }, this.WhenAnyValue(x => x.IsImportEnabled));

        CancelCommand = ReactiveCommand.Create(() => (BinaryImportResult?)null);
        BrowseSchemaCommand = ReactiveCommand.CreateFromTask(BrowseSchemaAsync);
        BrowseTelemetryCommand = ReactiveCommand.CreateFromTask(BrowseTelemetryAsync);

        if (!string.IsNullOrEmpty(_telemetryPath)) LoadPreview();
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

    private async Task BrowseTelemetryAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Binary Telemetry",
                AllowMultiple = false,
                FileTypeFilter = [
                    new Avalonia.Platform.Storage.FilePickerFileType("Binary Files") { Patterns = ["*.bin", "*.dat", "*.raw"] },
                    Avalonia.Platform.Storage.FilePickerFileTypes.All
                ]
            });

            if (files.Count > 0)
            {
                TelemetryPath = files[0].Path.LocalPath;
            }
        }
        catch (Exception ex)
        {
            await ShowError("File Error", "Could not select telemetry file.", ex);
        }
    }

    private async Task BrowseSchemaAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Schema File",
                AllowMultiple = false,
                FileTypeFilter = [
                    new Avalonia.Platform.Storage.FilePickerFileType("YAML Schema") { Patterns = ["*.yaml", "*.yml"] }
                ]
            });

            if (files.Count > 0)
            {
                SchemaFilePath = files[0].Path.LocalPath;
                try
                {
                    var yaml = await File.ReadAllTextAsync(SchemaFilePath);
                    var loader = new SchemaLoader();
                    SelectedSchema = loader.Load(yaml);
                }
                catch (Exception ex)
                {
                    await ShowError("Schema Error", "Failed to load the selected schema file.", ex);
                }
            }
        }
        catch (Exception ex)
        {
            await ShowError("File Error", "Could not select schema file.", ex);
        }
    }

    private void LoadPreview()
    {
        IsPreviewLoaded = false;
        PreviewRecords.Clear();
        AvailableColumns.Clear();

        if (SelectedSchema == null) return;

        // Populate available columns from the schema immediately
        foreach (var field in SelectedSchema.Fields)
        {
            AvailableColumns.Add(field.Name);
            // Auto-select 'timestamp' if found and not already set
            if (string.IsNullOrEmpty(TimestampField) && field.Name.Equals("timestamp", StringComparison.OrdinalIgnoreCase))
            {
                TimestampField = field.Name;
            }
        }

        if (string.IsNullOrEmpty(TelemetryPath) || !System.IO.File.Exists(TelemetryPath)) return;

        try
        {
            var source = new BinaryTelemetrySource(TelemetryPath, SelectedSchema);
            var packets = source.ReadPackets().Take(50).ToList();

            if (packets.Count > 0)
            {
                foreach (var p in packets)
                {
                    PreviewRecords.Add(p.Fields);
                }
                IsPreviewLoaded = true;
            }
        }
        catch (Exception ex)
        {
            PreviewRecords.Clear();
            _logger.LogError(ex, "Binary Preview Error");
        }
    }
}

