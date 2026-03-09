using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalBench.Core.Ingestion;
using SignalBench.SDK.Models;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SignalBench.ViewModels;

public partial class CsvImportViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _delimiter = "Comma (,)";

    partial void OnDelimiterChanged(string value) => LoadPreview();

    [ObservableProperty]
    private bool _hasHeader = true;

    partial void OnHasHeaderChanged(bool value) => LoadPreview();

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

    [ObservableProperty]
    private string? _timestampColumn;

    public ObservableCollection<string> AvailableColumns { get; } = [];
    public ObservableCollection<string> TimestampColumnOptions { get; } = [];

    public ObservableCollection<IDictionary<string, object>> PreviewRecords { get; } = [];

    [ObservableProperty]
    private bool _isPreviewLoaded;

    [ObservableProperty]
    private string? _filePath;

    partial void OnFilePathChanged(string? value) => LoadPreview();

    public event Action<CsvImportResult?>? RequestClose;

    public CsvImportViewModel() { }

    public CsvImportViewModel(string? filePath)
    {
        FilePath = filePath;
    }

    [RelayCommand(CanExecute = nameof(IsPreviewLoaded))]
    private void Import() => RequestClose?.Invoke(new CsvImportResult
    {
        FilePath = FilePath ?? string.Empty,
        Delimiter = GetActualDelimiter(),
        TimestampColumn = TimestampColumn,
        HasHeader = HasHeader
    });

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(null);

    [RelayCommand]
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
        ImportCommand.NotifyCanExecuteChanged();
    }
}

public class CsvImportResult
{
    public string FilePath { get; set; } = string.Empty;
    public string Delimiter { get; set; } = ",";
    public string? TimestampColumn { get; set; }
    public bool HasHeader { get; set; } = true;
}
