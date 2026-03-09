using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalBench.Core.Ingestion;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;
using SignalBench.SDK.Models;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace SignalBench.ViewModels;

public class BinaryImportResult
{
    public string TelemetryPath { get; set; } = string.Empty;
    public PacketSchema? Schema { get; set; }
    public string? TimestampField { get; set; }
}

public partial class BinaryImportViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImportEnabled))]
    private PacketSchema? _selectedSchema;

    partial void OnSelectedSchemaChanged(PacketSchema? value) => LoadPreview();

    [ObservableProperty]
    private string? _timestampField;

    [ObservableProperty]
    private string _schemaFilePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImportEnabled))]
    private bool _isPreviewLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImportEnabled))]
    private string _telemetryPath = string.Empty;

    partial void OnTelemetryPathChanged(string value) => LoadPreview();

    public ObservableCollection<IDictionary<string, object>> PreviewRecords { get; } = [];
    public ObservableCollection<string> AvailableColumns { get; } = [];

    public event Action<BinaryImportResult?>? RequestClose;

    private readonly ILogger<BinaryImportViewModel> _logger;
    public bool IsImportEnabled => SelectedSchema != null && !string.IsNullOrEmpty(TelemetryPath) && IsPreviewLoaded;

    [RelayCommand(CanExecute = nameof(IsImportEnabled))]
    private void Import() => RequestClose?.Invoke(new BinaryImportResult
    {
        TelemetryPath = TelemetryPath,
        Schema = SelectedSchema,
        TimestampField = TimestampField
    });

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(null);

    public BinaryImportViewModel(string? telemetryPath, ILogger<BinaryImportViewModel> logger)
    {
        _telemetryPath = telemetryPath ?? string.Empty;
        _logger = logger;

        if (!string.IsNullOrEmpty(_telemetryPath)) LoadPreview();
    }

    private async Task ShowError(string title, string message, Exception? ex = null)
    {
        if (ex != null) _logger.LogError(ex, "{Title}: {Message}", title, message);
        else _logger.LogError("{Title}: {Message}", title, message);

        var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(title, message);
        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel != null) await box.ShowWindowDialogAsync(topLevel);
    }

    [RelayCommand]
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

            if (files.Count > 0) TelemetryPath = files[0].Path.LocalPath;
        }
        catch (Exception ex) { await ShowError("File Error", "Could not select telemetry file.", ex); }
    }

    [RelayCommand]
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
                catch (Exception ex) { await ShowError("Schema Error", "Failed to load the selected schema file.", ex); }
            }
        }
        catch (Exception ex) { await ShowError("File Error", "Could not select schema file.", ex); }
    }

    private void LoadPreview()
    {
        IsPreviewLoaded = false;
        PreviewRecords.Clear();
        AvailableColumns.Clear();

        if (SelectedSchema == null) return;

        foreach (var field in SelectedSchema.Fields)
        {
            AvailableColumns.Add(field.Name);
            if (string.IsNullOrEmpty(TimestampField) && field.Name.Equals("timestamp", StringComparison.OrdinalIgnoreCase))
                TimestampField = field.Name;
        }

        if (string.IsNullOrEmpty(TelemetryPath) || !File.Exists(TelemetryPath)) return;

        try
        {
            var source = new BinaryTelemetrySource(TelemetryPath, SelectedSchema);
            var packets = source.ReadPackets().Take(50).ToList();

            if (packets.Count > 0)
            {
                foreach (var p in packets) PreviewRecords.Add(p.Fields);
                IsPreviewLoaded = true;
            }
        }
        catch (Exception ex)
        {
            PreviewRecords.Clear();
            _logger.LogError(ex, "Binary Preview Error");
        }
        
        ImportCommand.NotifyCanExecuteChanged();
    }
}