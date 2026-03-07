using ReactiveUI;
using SignalBench.Core.Ingestion;
using SignalBench.SDK.Models;
using System.Collections.ObjectModel;
using System.Reactive;

namespace SignalBench.ViewModels;

public class CsvImportViewModel : ViewModelBase
{
    private string _delimiter = ",";
    public string Delimiter
    {
        get => _delimiter;
        set
        {
            this.RaiseAndSetIfChanged(ref _delimiter, value);
            LoadPreview();
        }
    }

    private bool _hasHeader = true;
    public bool HasHeader
    {
        get => _hasHeader;
        set
        {
            this.RaiseAndSetIfChanged(ref _hasHeader, value);
            LoadPreview();
        }
    }

    private string GetActualDelimiter()
    {
        return Delimiter switch
        {
            "Comma (,)" => ",",
            "Semicolon (;)" => ";",
            "Pipe (|)" => "|",
            "Tab" => "\t",
            _ => ","
        };
    }

    public string[] Delimiters { get; } = ["Comma (,)", "Semicolon (;)", "Pipe (|)", "Tab"];

    private string? _timestampColumn;
    public string? TimestampColumn
    {
        get => _timestampColumn;
        set => this.RaiseAndSetIfChanged(ref _timestampColumn, value);
    }

    public ObservableCollection<string> AvailableColumns { get; } = [];
    public ObservableCollection<string> TimestampColumnOptions { get; } = [];

    public ObservableCollection<IDictionary<string, object>> PreviewRecords { get; } = [];

    private bool _isPreviewLoaded;
    public bool IsPreviewLoaded
    {
        get => _isPreviewLoaded;
        set => this.RaiseAndSetIfChanged(ref _isPreviewLoaded, value);
    }

    private string? _filePath;
    public string? FilePath
    {
        get => _filePath;
        set
        {
            this.RaiseAndSetIfChanged(ref _filePath, value);
            LoadPreview();
        }
    }

    public ReactiveCommand<Unit, CsvImportResult?> ImportCommand { get; }
    public ReactiveCommand<Unit, CsvImportResult?> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }

    public CsvImportViewModel(string? filePath = null)
    {
        _filePath = filePath;
        _delimiter = "Comma (,)";
        _hasHeader = true;
        ImportCommand = ReactiveCommand.Create(() => (CsvImportResult?)new CsvImportResult
        {
            FilePath = FilePath ?? string.Empty,
            Delimiter = GetActualDelimiter(),
            TimestampColumn = TimestampColumn,
            HasHeader = HasHeader
        }, this.WhenAnyValue(x => x.IsPreviewLoaded));
        
        CancelCommand = ReactiveCommand.Create(() => (CsvImportResult?)null);
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseAsync);

        if (!string.IsNullOrEmpty(_filePath)) LoadPreview();
    }

    private async Task BrowseAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select CSV Telemetry",
                AllowMultiple = false,
                FileTypeFilter = [
                    new Avalonia.Platform.Storage.FilePickerFileType("CSV Files") { Patterns = ["*.csv", "*.tsv", "*.txt"] },
                    Avalonia.Platform.Storage.FilePickerFileTypes.All
                ]
            });

            if (files.Count > 0)
            {
                FilePath = files[0].Path.LocalPath;
            }
        }
        catch (Exception) { /* Handle or log */ }
    }

    private void LoadPreview()
    {
        IsPreviewLoaded = false;
        if (string.IsNullOrEmpty(FilePath) || !System.IO.File.Exists(FilePath))
        {
            PreviewRecords.Clear();
            AvailableColumns.Clear();
            return;
        }

        try
        {
            var source = new CsvTelemetrySource(FilePath, GetActualDelimiter(), hasHeader: HasHeader);
            var packets = source.ReadPackets().Take(10).ToList();

            var newRecords = new List<IDictionary<string, object>>();
            var newColumns = new List<string>();

            if (packets.Count > 0)
            {
                newColumns.AddRange(packets[0].Fields.Keys);
                foreach (var p in packets)
                {
                    newRecords.Add(p.Fields);
                }
            }

            PreviewRecords.Clear();
            AvailableColumns.Clear();

            foreach (var col in newColumns) AvailableColumns.Add(col);
            foreach (var rec in newRecords) PreviewRecords.Add(rec);

            if (newColumns.Count > 0)
            {
                if (string.IsNullOrEmpty(TimestampColumn) || !AvailableColumns.Contains(TimestampColumn))
                {
                    TimestampColumn = AvailableColumns.FirstOrDefault(c => c.Contains("time", StringComparison.OrdinalIgnoreCase))
                                      ?? AvailableColumns.FirstOrDefault();
                }
                IsPreviewLoaded = true;
            }
        }
        catch (Exception ex)
        {
            PreviewRecords.Clear();
            AvailableColumns.Clear();
            System.Diagnostics.Debug.WriteLine($"CSV Preview Error: {ex.Message} (File: {FilePath}, Delimiter: {GetActualDelimiter()})");
        }
    }
}

public class CsvImportResult
{
    public string FilePath { get; set; } = string.Empty;
    public string Delimiter { get; set; } = ",";
    public string? TimestampColumn { get; set; }
    public bool HasHeader { get; set; } = true;
}
