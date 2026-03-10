using Microsoft.Extensions.Logging;
using Avalonia.Platform.Storage;
using SignalBench.Core.Ingestion;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Session;
using SignalBench.SDK.Models;

namespace SignalBench.ViewModels;

public partial class MainWindowViewModel
{
    private async Task OpenCsvAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var dialog = new SignalBench.Views.TextFileImport { DataContext = new TextFileImportViewModel() };
            var result = await dialog.ShowDialog<TextFileImportResult?>(topLevel);
            if (result != null && !string.IsNullOrEmpty(result.FilePath))
            {
                var settings = new CsvSettings
                {
                    Delimiter = result.Delimiter,
                    TimestampColumn = result.TimestampColumn,
                    HasHeader = result.HasHeader
                };
                await LoadTelemetryFileAsync(result.FilePath, null, null, settings);
            }
        }
        catch (Exception ex) { await ShowError("Text File Error", "Failed to open text file import.", ex); }
    }

    private async Task OpenBinaryAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var dialog = new SignalBench.Views.BinaryFileImport { DataContext = new BinaryFileImportViewModel(null, _loggerFactory.CreateLogger<BinaryFileImportViewModel>()) };
            var result = await dialog.ShowDialog<BinaryFileImportResult?>(topLevel);
            if (result != null && !string.IsNullOrEmpty(result.TelemetryPath))
            {
                await LoadTelemetryFileAsync(result.TelemetryPath, null, result.Schema, null, result.TimestampField);
            }
        }
        catch (Exception ex) { await ShowError("Binary Error", "Failed to open binary import.", ex); }
    }

    private async Task LoadTelemetryFileAsync(string path, string? schemaPath = null, PacketSchema? existingSchema = null, CsvSettings? existingCsvSettings = null, string? timestampField = null)
    {
        if (IsStreaming)
        {
            await StopStreamingAsync();
        }

        IsLoading = true;
        LoadProgress = 0;
        LoadElapsed = "00:00";
        _statusText = $"Loading {path}...";
        OnPropertyChanged(nameof(StatusText));
        var startTime = DateTime.Now;

        if (path.EndsWith(".csv"))
        {
            CsvSettings csvSettings;
            if (existingCsvSettings != null)
            {
                csvSettings = existingCsvSettings;
            }
            else
            {
                var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                if (topLevel == null) 
                {
                    IsLoading = false;
                    return;
                }
                var dialog = new SignalBench.Views.TextFileImport { DataContext = new TextFileImportViewModel(path) };
                var result = await dialog.ShowDialog<TextFileImportResult?>(topLevel);
                if (result == null)
                {
                    IsLoading = false;
                    return;
                }
                csvSettings = new CsvSettings
                {
                    Delimiter = result.Delimiter,
                    TimestampColumn = result.TimestampColumn,
                    HasHeader = result.HasHeader
                };
            }

            AddPlot(Path.GetFileName(path), path);
            var targetPlot = SelectedPlot;
            var targetStore = targetPlot!.DataStore;
            targetPlot.CsvSettings = csvSettings;

            await Task.Run(async () =>
            {
                try
                {
                    var lineCount = File.ReadLines(path).Count();
                    
                    var source = new CsvTelemetrySource(path, csvSettings.Delimiter, csvSettings.TimestampColumn, csvSettings.HasHeader);
                    var packets = new List<DecodedPacket>();
                    var processed = 0;
                    var lastUpdate = DateTime.Now;
                    foreach (var packet in source.ReadPackets())
                    {
                        packets.Add(packet);
                        processed++;
                        if ((DateTime.Now - lastUpdate).TotalMilliseconds > 100)
                        {
                            var elapsed = DateTime.Now - startTime;
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => { LoadProgress = (double)processed / lineCount * 100; LoadElapsed = elapsed.ToString(@"mm\:ss"); });
                            lastUpdate = DateTime.Now; await Task.Delay(1);
                        }
                    }
                    if (packets.Count > 0)
                    {
                        var fields = new List<string>(packets[0].Fields.Keys);
                        var schema = new PacketSchema { Name = "CSV Import" };
                        foreach (var field in fields) schema.Fields.Add(new FieldDefinition { Name = field });

                        targetStore.InitializeSchema(schema);
                        targetStore.InsertPackets(packets);

                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            targetPlot.TelemetryPath = path; targetPlot.Schema = schema;
                            targetPlot.SourceType = PlotSourceType.File;
                            targetPlot.CsvSettings = csvSettings;

                            targetPlot.AvailableSignals.Clear();
                            targetPlot.RegularSignals.Clear();
                            PopulateSignals(targetPlot, schema.Fields);

                            if (targetPlot == SelectedPlot)
                            {
                                OnPropertyChanged(nameof(HasData));
                                AddToRecentFiles(path); UpdatePlot(targetPlot);
                                OnPropertyChanged(nameof(CurrentPlaybackIndex));
                                OnPropertyChanged(nameof(CurrentPlaybackTime));
                                OnPropertyChanged(nameof(FormattedPlaybackTime));
                                OnPropertyChanged(nameof(PlaybackProgress));
                                OnPropertyChanged(nameof(IsPlaybackBarVisible));
                                OnPropertyChanged(nameof(CanAddPlot));
                                SyncSignalCheckboxes();
                            }
                            IsLoading = false;
                            StatusText = $"Loaded {packets.Count:N0} records in {(DateTime.Now - startTime).TotalSeconds:F1}s";
                        });
                    }
                    else
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => { IsLoading = false; StatusText = "No records found in CSV."; });
                    }
                }
                catch (Exception ex)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => IsLoading = false);
                    await ShowError("Load Error", "Failed to load CSV telemetry.", ex);
                }
            });
        }
        else
        {
            PacketSchema? schema = existingSchema;
            if (schema == null && !string.IsNullOrEmpty(schemaPath))
            {
                try
                {
                    var yaml = await File.ReadAllTextAsync(schemaPath);
                    schema = new SignalBench.Core.Services.SchemaLoader().Load(yaml);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not load schema."); }
            }
            if (schema == null) 
            {
                schema = await PromptForSchemaAsync(path);
            }
            
            if (schema == null) 
            {
                IsLoading = false;
                return;
            }

            AddPlot(Path.GetFileName(path), path, schema);
            var targetPlot = SelectedPlot;
            targetPlot!.SchemaPath = schemaPath; // Store the actual file path
            var targetStore = targetPlot!.DataStore;

            await Task.Run(async () =>
            {
                try
                {
                    targetStore.InitializeSchema(schema);
                    var source = new BinaryTelemetrySource(path, schema);
                    var packets = source.ReadPackets().ToList();

                    // Update packet timestamps if a specific field was chosen
                    if (!string.IsNullOrEmpty(timestampField))
                    {
                        var updatedPackets = new List<DecodedPacket>();
                        foreach (var packet in packets)
                        {
                            if (packet.Fields.TryGetValue(timestampField, out var val))
                            {
                                try
                                {
                                    double seconds = Convert.ToDouble(val);
                                    updatedPackets.Add(packet with { Timestamp = DateTime.UnixEpoch.AddSeconds(seconds) });
                                }
                                catch { updatedPackets.Add(packet); }
                            }
                            else updatedPackets.Add(packet);
                        }
                        packets = updatedPackets;
                    }
                    targetStore.InsertPackets(packets);

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        targetPlot.TelemetryPath = path; targetPlot.Schema = schema;
                        targetPlot.SourceType = PlotSourceType.File;
                        targetPlot.AvailableSignals.Clear();
                        targetPlot.RegularSignals.Clear();
                        PopulateSignals(targetPlot, schema.Fields);

                        if (SelectedPlot == targetPlot)
                        {
                            OnPropertyChanged(nameof(HasData));
                            AddToRecentFiles(path); UpdatePlot(targetPlot);
                            OnPropertyChanged(nameof(CurrentPlaybackIndex));
                            OnPropertyChanged(nameof(CurrentPlaybackTime));
                            OnPropertyChanged(nameof(FormattedPlaybackTime));
                            OnPropertyChanged(nameof(PlaybackProgress));
                            OnPropertyChanged(nameof(IsPlaybackBarVisible));
                            SyncSignalCheckboxes();
                        }
                        IsLoading = false;
                        StatusText = $"Loaded {packets.Count} records from {Path.GetFileName(path)}";
                    });
                }
                catch (Exception ex) 
                { 
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => IsLoading = false);
                    await ShowError("Load Error", "Failed to load binary telemetry.", ex); 
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
            var dialog = new SignalBench.Views.BinaryFileImport { DataContext = new BinaryFileImportViewModel(telemetryPath, _loggerFactory.CreateLogger<BinaryFileImportViewModel>()) };
            var result = await dialog.ShowDialog<BinaryFileImportResult?>(topLevel);
            return result?.Schema;
        }
        catch (Exception ex) { await ShowError("Schema Error", "Failed to prompt for schema.", ex); return null; }
    }

    private async Task ExportCsv()
    {
        try
        {
            if (SelectedPlot == null) return;
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export CSV",
                DefaultExtension = "csv",
                FileTypeChoices = [new FilePickerFileType("CSV Files") { Patterns = ["*.csv"] }]
            });
            if (file != null)
            {
                StatusText = "Exporting CSV...";
                await Task.Run(async () =>
                {
                    using var writer = new StreamWriter(file.Path.LocalPath);
                    var selectedSignals = SelectedPlot.AvailableSignals.Where(s => s.IsSelected).ToList();
                    var headers = new List<string> { "Timestamp" };
                    headers.AddRange(selectedSignals.Select(s => s.Name));
                    await writer.WriteLineAsync(string.Join(",", headers));
                    var timestamps = _dataStore.GetTimestamps();
                    var signalData = new Dictionary<string, List<double>>();
                    foreach (var signal in selectedSignals) signalData[signal.Name] = _dataStore.GetSignalData(signal.Name);
                    for (int i = 0; i < timestamps.Count; i++)
                    {
                        var row = new List<string> { timestamps[i].ToString("yyyy-MM-dd HH:mm:ss.fff") };
                        foreach (var signal in selectedSignals) row.Add(signalData[signal.Name][i].ToString());
                        await writer.WriteLineAsync(string.Join(",", row));
                    }
                });
                StatusText = "Export complete.";
            }
        }
        catch (Exception ex) { await ShowError("Export Error", "Failed to export CSV.", ex); }
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
}
