using Microsoft.Extensions.Logging;
using Avalonia.Platform.Storage;
using SignalBench.Core.Data;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;
using SignalBench.Core.Session;

namespace SignalBench.ViewModels;

public partial class MainWindowViewModel
{
    private async Task SaveSessionAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Session",
                DefaultExtension = "sbs",
                FileTypeChoices = [new FilePickerFileType("SignalBench Session") { Patterns = ["*.sbs"] }]
            });
            if (file != null)
            {
                var tabSessions = new List<TabSession>();
                foreach (var t in Tabs)
                {
                    if (t is PlotViewModel p)
                    {
                        var tab = new TabSession
                        {
                            Name = p.Name,
                            SourceType = p.SourceType.ToString(),
                            TelemetryPath = p.TelemetryPath,
                            SelectedSignalNames = [.. p.SelectedSignalNames],
                            DerivedSignals = [.. p.DerivedSignals],
                            ThresholdRules = [.. p.ThresholdRules]
                        };

                        // Embed schema YAML if available
                        if (p.Schema != null)
                        {
                            try { tab.SchemaYaml = new SchemaLoader().Save(p.Schema); } catch { }
                        }

                        // Only save settings relevant to the source type
                        if (p.SourceType == PlotSourceType.Serial) tab.SerialSettings = p.SerialSettings;
                        if (p.SourceType == PlotSourceType.Network) tab.NetworkSettings = p.NetworkSettings;
                        if (p.SourceType == PlotSourceType.File && p.TelemetryPath?.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            tab.CsvSettings = new CsvSettings
                            {
                                Delimiter = p.CsvSettings.Delimiter,
                                TimestampColumn = p.CsvSettings.TimestampColumn,
                                HasHeader = p.CsvSettings.HasHeader
                            };
                        }
                        tabSessions.Add(tab);
                    }
                }
                var session = new ProjectSession
                {
                    SelectedTabIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0,
                    Tabs = tabSessions
                };

                _sessionManager.SaveSession(file.Path.LocalPath, session);
                StatusText = "Session saved.";
            }
        }
        catch (Exception ex) { await ShowError("Save Error", "Failed to save session.", ex); }
    }

    private async Task LoadSessionInternalAsync(string path)
    {
        try
        {
            var session = _sessionManager.LoadSession(path);
            // Clear existing tabs
            if (IsStreaming) await StopStreamingAsync();
            foreach (var t in Tabs) t.Dispose();
            Tabs.Clear();

            foreach (var tab in session.Tabs)
            {
                // Reconstruct PlotViewModel
                var mode = _settingsService.Current.StorageMode == "Sqlite" ? StorageMode.Sqlite : StorageMode.InMemory;
                IDataStore store;
                if (mode == StorageMode.Sqlite)
                {
                    store = new SqliteDataStore(Path.Combine(Path.GetTempPath(), $"signalbench_{Guid.NewGuid():N}.db"));
                }
                else
                {
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
                Tabs.Add(plot);
                SelectedTab = plot;

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

                var targetPlot = Tabs.Last() as PlotViewModel;
                if (targetPlot != null)
                {
                    targetPlot.SelectedSignalNames.Clear();
                    foreach (var sName in tab.SelectedSignalNames) targetPlot.SelectedSignalNames.Add(sName);

                    foreach (var ds in tab.DerivedSignals)
                    {
                        if (!targetPlot.DerivedSignals.Any(d => d.Name == ds.Name))
                            targetPlot.DerivedSignals.Add(ds);
                    }

                    foreach (var tr in tab.ThresholdRules)
                    {
                        if (!targetPlot.ThresholdRules.Any(r => r.Name == tr.Name))
                            targetPlot.ThresholdRules.Add(tr);
                    }
                }
            }
            if (session.SelectedTabIndex >= 0 && session.SelectedTabIndex < Tabs.Count)
                SelectedTab = Tabs[session.SelectedTabIndex];
            StatusText = $"Session loaded: {Path.GetFileName(path)}";
            // Persist as last session
            _settingsService.Current.LastSessionPath = path;
            _settingsService.Save();
        }
        catch (Exception ex) { await ShowError("Load Error", "Failed to load session.", ex); }
    }

    private async Task OpenSessionAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Session",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("SignalBench Session") { Patterns = ["*.sbs"] }]
            });

            if (files.Count > 0)
            {
                var session = _sessionManager.LoadSession(files[0].Path.LocalPath);
                // Clear existing tabs
                if (IsStreaming) await StopStreamingAsync();
                foreach (var t in Tabs) t.Dispose();
                Tabs.Clear();

                foreach (var tab in session.Tabs)
                {
                    // Reconstruct PlotViewModel
                    var mode = _settingsService.Current.StorageMode == "Sqlite" ? StorageMode.Sqlite : StorageMode.InMemory;
                    IDataStore store;
                    if (mode == StorageMode.Sqlite)
                    {
                        store = new SqliteDataStore(Path.Combine(Path.GetTempPath(), $"signalbench_{Guid.NewGuid():N}.db"));
                    }
                    else
                    {
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
                    Tabs.Add(plot);
                    SelectedTab = plot;
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

                    var targetPlot = Tabs.Last() as PlotViewModel;
                    if (targetPlot != null)
                    {
                        targetPlot.SelectedSignalNames.Clear();
                        foreach (var sName in tab.SelectedSignalNames) targetPlot.SelectedSignalNames.Add(sName);

                        foreach (var ds in tab.DerivedSignals)
                        {
                            if (!targetPlot.DerivedSignals.Any(d => d.Name == ds.Name))
                                targetPlot.DerivedSignals.Add(ds);
                        }

                        foreach (var tr in tab.ThresholdRules)
                        {
                            if (!targetPlot.ThresholdRules.Any(r => r.Name == tr.Name))
                                targetPlot.ThresholdRules.Add(tr);
                        }
                    }
                }
                if (session.SelectedTabIndex >= 0 && session.SelectedTabIndex < Tabs.Count)
                    SelectedTab = Tabs[session.SelectedTabIndex];

                StatusText = "Session loaded.";
            }
        }
        catch (Exception ex) { await ShowError("Load Error", "Failed to load session.", ex); }
    }

    public void AutoSaveSession()
    {
        try
        {
            string autoSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SignalBench", "autosave.sbs");
            Directory.CreateDirectory(Path.GetDirectoryName(autoSavePath)!);
            var tabSessions = new List<TabSession>();
            foreach (var t in Tabs)
            {
                if (t is PlotViewModel p)
                {
                    var tab = new TabSession
                    {
                        Name = p.Name,
                        SourceType = p.SourceType.ToString(),
                        TelemetryPath = p.TelemetryPath,
                        SelectedSignalNames = [.. p.SelectedSignalNames],
                        DerivedSignals = [.. p.DerivedSignals],
                        ThresholdRules = [.. p.ThresholdRules],
                        SerialSettings = p.SerialSettings,
                        NetworkSettings = p.NetworkSettings,
                        CsvSettings = p.CsvSettings
                    };
                    if (p.Schema != null) { try { tab.SchemaYaml = new SchemaLoader().Save(p.Schema); } catch { } }
                    tabSessions.Add(tab);
                }
            }
            var session = new ProjectSession { SelectedTabIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0, Tabs = tabSessions };
            _sessionManager.SaveSession(autoSavePath, session);

            _settingsService.Current.LastSessionPath = autoSavePath;
            _settingsService.Save();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Auto-save failed."); }
    }
}
