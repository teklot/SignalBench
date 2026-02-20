using ReactiveUI;
using SignalBench.Core.Ingestion;
using System.Collections.ObjectModel;
using System.Reactive;

namespace SignalBench.ViewModels;

public class CsvImportViewModel : ViewModelBase
{
    private string _delimiter = ",";
    public string Delimiter
    {
        get => _delimiter;
        set {
            this.RaiseAndSetIfChanged(ref _delimiter, value);
            LoadPreview();
        }
    }

    private string GetActualDelimiter()
    {
        return Delimiter == "Tab" ? "\t" : Delimiter;
    }

    private string? _timestampColumn;
    public string? TimestampColumn
    {
        get => _timestampColumn;
        set => this.RaiseAndSetIfChanged(ref _timestampColumn, value);
    }

    public ObservableCollection<string> AvailableColumns { get; } = [];
    public ObservableCollection<IDictionary<string, object>> PreviewRecords { get; } = [];

    private readonly string _filePath;

    public ReactiveCommand<Unit, CsvImportResult?> ImportCommand { get; }
    public ReactiveCommand<Unit, CsvImportResult?> CancelCommand { get; }

    public CsvImportViewModel(string filePath)
    {
        _filePath = filePath;
        ImportCommand = ReactiveCommand.Create(() => (CsvImportResult?)new CsvImportResult 
        { 
            Delimiter = GetActualDelimiter(), 
            TimestampColumn = TimestampColumn 
        });
        CancelCommand = ReactiveCommand.Create(() => (CsvImportResult?)null);

        LoadPreview();
    }

    private void LoadPreview()
    {
        try
        {
            var source = new CsvTelemetrySource(_filePath, GetActualDelimiter());
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
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CSV Preview Error: {ex.Message} (File: {_filePath}, Delimiter: {GetActualDelimiter()})");
        }
    }
}

public class CsvImportResult
{
    public string Delimiter { get; set; } = ",";
    public string? TimestampColumn { get; set; }
}
