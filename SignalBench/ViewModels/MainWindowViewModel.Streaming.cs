using Avalonia.Threading;
using SignalBench.Core.Ingestion;
using SignalBench.SDK.Models;

namespace SignalBench.ViewModels;

public partial class MainWindowViewModel
{
    private DateTime _lastLivePlotUpdate = DateTime.MinValue;
    private readonly object _liveDataLock = new();
    private Dictionary<PlotViewModel, List<DecodedPacket>> _livePacketBuffers = new();

    private void RefreshPorts() { }

    private async Task ToggleStreamingAsync()
    {
        if (SelectedPlot?.SourceType == PlotSourceType.Serial)
        {
            if (IsSerialPaused)
            {
                await ResumeSerialAsync();
            }
            else if (IsSerialStreaming)
            {
                await PauseSerialAsync();
            }
            else
            {
                await ConfigureAndStartSerialAsync();
            }
        }
        else
        {
            await ConfigureAndStartSerialAsync();
        }
    }

    private async Task ToggleUdpStreamingAsync()
    {
        if (SelectedPlot?.SourceType == PlotSourceType.Network)
        {
            if (IsNetworkPaused)
            {
                await ResumeNetworkAsync();
            }
            else if (IsNetworkStreaming)
            {
                await PauseNetworkAsync();
            }
            else
            {
                await ConfigureAndStartNetworkAsync();
            }
        }
        else
        {
            await ConfigureAndStartNetworkAsync();
        }
    }

    private async Task ConfigureAndStartSerialAsync()
    {
        if (!await OpenSerialSettingsAsync()) return;
        await StartStreamingAsync();
    }

    private async Task ConfigureAndStartNetworkAsync()
    {
        if (!await OpenNetworkSettingsAsync()) return;
        await StartNetworkStreamingAsync();
    }

    private async Task PauseSerialAsync()
    {
        if (SelectedPlot?.ActiveSource != null)
        {
            await Task.Run(() => SelectedPlot.ActiveSource.Stop());
            SelectedPlot.IsStreaming = false;
            SelectedPlot.IsPaused = true;
            StatusText = "Serial stream paused.";
            NotifySourceStateChanged();
        }
    }

    private async Task ResumeSerialAsync()
    {
        if (SelectedPlot?.ActiveSource != null && IsSerialPaused)
        {
            await Task.Run(() => SelectedPlot.ActiveSource.Start());
            SelectedPlot.IsStreaming = true;
            SelectedPlot.IsPaused = false;
            StatusText = "Serial stream resumed.";
            NotifySourceStateChanged();
        }
    }

    private async Task PauseNetworkAsync()
    {
        if (SelectedPlot?.ActiveSource != null)
        {
            await Task.Run(() => SelectedPlot.ActiveSource.Stop());
            SelectedPlot.IsStreaming = false;
            SelectedPlot.IsPaused = true;
            StatusText = "Network stream paused.";
            NotifySourceStateChanged();
        }
    }

    private async Task ResumeNetworkAsync()
    {
        if (SelectedPlot?.ActiveSource != null && IsNetworkPaused)
        {
            await Task.Run(() => SelectedPlot.ActiveSource.Start());
            SelectedPlot.IsStreaming = true;
            SelectedPlot.IsPaused = false;
            StatusText = "Network stream resumed.";
            NotifySourceStateChanged();
        }
    }

    private async Task StartNetworkStreamingAsync()
    {
        if (SelectedPlot == null) return;
        var settings = SelectedPlot.NetworkSettings;
        var schema = SelectedPlot.Schema;
        
        if (string.IsNullOrEmpty(settings.IpAddress) || settings.Port <= 0 || schema == null)
        {
            await ShowError("Configuration Error", "Please configure Network settings."); return;
        }

        try
        {
            // Stop any existing stream for THIS plot
            if (SelectedPlot.ActiveSource != null)
            {
                var s = SelectedPlot.ActiveSource; SelectedPlot.ActiveSource = null;
                await Task.Run(() => s.Stop());
            }

            var protocol = settings.Protocol == "UDP" ? NetworkProtocol.Udp : NetworkProtocol.Tcp;
            var plotName = !string.IsNullOrEmpty(schema.Name) ? schema.Name : (protocol == NetworkProtocol.Udp 
                ? $"UDP:{settings.Port}" 
                : $"TCP:{settings.IpAddress}:{settings.Port}");
            
            SelectedPlot.Name = plotName;
            var targetPlot = SelectedPlot;
            var targetStore = targetPlot.DataStore;
            
            // Clear old state
            targetStore.Clear();
            targetPlot.TotalRecords = 0;
            targetPlot.RequestPlotClear?.Invoke();
            _totalRecords = 0;
            targetPlot.TelemetryPath = null;
            _playbackTimestamps = [];
            _playbackSignalData = [];
            OnPropertyChanged(nameof(TotalRecords));

            // CRITICAL: Clear old signals so new ones from schema can be added
            targetPlot.AvailableSignals.Clear();
            targetPlot.RegularSignals.Clear();
            targetPlot.SelectedSignalNames.Clear();
            targetPlot.DerivedSignals.Clear();

            targetPlot.IsStreaming = true;
            targetPlot.IsPaused = false;
            targetPlot.SourceType = PlotSourceType.Network;
            targetStore.InitializeSchema(schema);

            // Populate signals IMMEDIATELY before starting the source
            PopulateSignals(targetPlot, schema.Fields);

            if (targetPlot == SelectedPlot)
            {
                SyncSignalCheckboxes();
                OnPropertyChanged(nameof(HasData));
                OnPropertyChanged(nameof(IsPlaybackBarVisible));
            }

            var source = new NetworkTelemetrySource(settings.IpAddress, settings.Port, schema, protocol);
            SelectedPlot.ActiveSource = source;
            source.PacketReceived += (p) => HandleLivePacket(targetPlot, p);
            source.ErrorReceived += msg =>
            {
                Dispatcher.UIThread.Post(() => { StatusText = $"Network Error: {msg}"; });
            };

            await Task.Run(() => source.Start());
            NotifySourceStateChanged();
            
            // Force status bar info refresh
            if (SelectedPlot != null)
            {
                SelectedPlot.RaisePropertyChanged(nameof(PlotViewModel.ConnectionInfo));
                SelectedPlot.RaisePropertyChanged(nameof(PlotViewModel.ConnectionIcon));
            }

            StatusText = protocol == NetworkProtocol.Udp
                ? $"Listening on UDP port {settings.Port}..." 
                : $"Connected to TCP {settings.IpAddress}:{settings.Port}...";
        }
        catch (Exception ex)
        {
            await ShowError("Connection Error", $"Failed to start network stream: {ex.Message}");
        }
    }

    private async Task StartStreamingAsync()
    {
        if (SelectedPlot == null) return;
        var settings = SelectedPlot.SerialSettings;
        var schema = SelectedPlot.Schema;

        if (string.IsNullOrEmpty(settings.Port) || schema == null) {
            await ShowError("Configuration Error", "Please configure Serial settings."); return;
        }

        try {
            // Stop any existing stream for THIS plot
            if (SelectedPlot.ActiveSource != null)
            {
                var s = SelectedPlot.ActiveSource; SelectedPlot.ActiveSource = null;
                await Task.Run(() => s.Stop());
            }

            var plotName = !string.IsNullOrEmpty(schema.Name) ? schema.Name : $"{settings.Port}";
            SelectedPlot.Name = plotName;
            var targetPlot = SelectedPlot;
            var targetStore = targetPlot.DataStore;

            // Clear old state
            targetStore.Clear();
            targetPlot.TotalRecords = 0;
            targetPlot.RequestPlotClear?.Invoke();
            _totalRecords = 0;
            targetPlot.TelemetryPath = null;
            _playbackTimestamps = [];
            _playbackSignalData = [];
            OnPropertyChanged(nameof(TotalRecords));

            // CRITICAL: Clear old signals so new ones from schema can be added
            targetPlot.AvailableSignals.Clear();
            targetPlot.RegularSignals.Clear();
            targetPlot.SelectedSignalNames.Clear();
            targetPlot.DerivedSignals.Clear();

            SelectedPlot.IsStreaming = true;
            SelectedPlot.IsPaused = false;
            SelectedPlot.SourceType = PlotSourceType.Serial;
            targetStore.InitializeSchema(schema);

            // Populate signals IMMEDIATELY before starting the source
            PopulateSignals(targetPlot, schema.Fields);

            if (targetPlot == SelectedPlot)
            {
                SyncSignalCheckboxes();
                OnPropertyChanged(nameof(HasData));
                OnPropertyChanged(nameof(IsPlaybackBarVisible));
            }

            var parity = Enum.Parse<System.IO.Ports.Parity>(settings.Parity);
            var stopBits = Enum.Parse<System.IO.Ports.StopBits>(settings.StopBits);

            var source = new SerialTelemetrySource(settings.Port, settings.BaudRate, schema, parity, settings.DataBits, stopBits);
            SelectedPlot.ActiveSource = source;
            source.PacketReceived += (p) => HandleLivePacket(targetPlot, p);
            source.ErrorReceived += msg => {
                Dispatcher.UIThread.Post(() => { StatusText = $"Serial Error: {msg}"; targetPlot.IsStreaming = false; NotifySourceStateChanged(); });
            };
            
            await Task.Run(() => source.Start());
            NotifySourceStateChanged();

            // Force status bar info refresh
            if (SelectedPlot != null)
            {
                SelectedPlot.RaisePropertyChanged(nameof(PlotViewModel.ConnectionInfo));
                SelectedPlot.RaisePropertyChanged(nameof(PlotViewModel.ConnectionIcon));
            }

            StatusText = $"Streaming from {settings.Port}...";
        } catch (Exception ex) { await ShowError("Connection Error", $"Failed to start streaming: {ex.Message}"); }
    }

    private void HandleLivePacket(PlotViewModel plot, DecodedPacket packet)
    {
        lock (_liveDataLock) { 
            if (!_livePacketBuffers.ContainsKey(plot)) _livePacketBuffers[plot] = new();
            _livePacketBuffers[plot].Add(packet); 
        }
        if ((DateTime.Now - _lastLivePlotUpdate).TotalMilliseconds > 100) {
            _lastLivePlotUpdate = DateTime.Now;
            Dispatcher.UIThread.Post(UpdateLivePlot);
        }
    }

    private void UpdateLivePlot()
    {
        Dictionary<PlotViewModel, List<DecodedPacket>> batches;
        lock (_liveDataLock) { 
            batches = new Dictionary<PlotViewModel, List<DecodedPacket>>(_livePacketBuffers);
            _livePacketBuffers.Clear();
        }

        foreach (var kvp in batches)
        {
            var targetPlot = kvp.Key;
            var batch = kvp.Value;
            if (batch.Count == 0) continue;

            var targetStore = targetPlot.DataStore;
            bool wasEmpty = targetPlot.TotalRecords == 0;
            targetStore.InsertPackets(batch);

            // Re-compute derived signals if any exist for this plot
            if (targetPlot.DerivedSignals.Count > 0)
            {
                foreach (var ds in targetPlot.DerivedSignals)
                {
                    // Pass specific store to ComputeDerivedSignal
                    targetStore.InsertDerivedSignal(ds.Name, ComputeDerivedSignal(ds, targetStore, targetPlot));
                }
            }

            int rowCount = targetStore.GetRowCount();
            targetPlot.TotalRecords = rowCount;
            if (targetPlot == SelectedPlot)
            {
                _totalRecords = rowCount;
                OnPropertyChanged(nameof(TotalRecords));
                OnPropertyChanged(nameof(CurrentPlaybackTime));
                OnPropertyChanged(nameof(FormattedPlaybackTime));
                if (wasEmpty)
                {
                    OnPropertyChanged(nameof(HasData));
                    OnPropertyChanged(nameof(IsPlaybackBarVisible));
                }
                
                // Update CurrentPlaybackIndex to latest during streaming
                CurrentPlaybackIndex = rowCount - 1;
                RefreshCurrentValues();
            }

            // Determine rolling window in seconds
            int windowSeconds = targetPlot.SourceType == PlotSourceType.Serial 
                ? targetPlot.SerialSettings.RollingWindowSeconds 
                : targetPlot.NetworkSettings.RollingWindowSeconds;

            DateTime latestTs = targetStore.GetTimestamp(targetPlot.TotalRecords - 1);
            DateTime startTs = latestTs.AddSeconds(-windowSeconds);

            var timestamps = targetStore.GetTimestamps(startTs);
            var plotData = new Dictionary<string, List<double>>();
            foreach (var signalName in targetPlot.SelectedSignalNames)
                plotData[signalName] = targetStore.GetSignalData(signalName, startTs);

            if (timestamps.Count > 0)
            {
                double? forceXMax = timestamps[^1].ToOADate();
                targetPlot.RequestPlotUpdate?.Invoke(timestamps, plotData, null, forceXMax, windowSeconds);
            }
        }
    }

    private async Task StopStreamingAsync()
    {
        foreach (var t in Tabs) {
            if (t is PlotViewModel p)
            {
                p.IsStreaming = false;
                p.IsPaused = false;
                if (p.ActiveSource != null)
                {
                    var s = p.ActiveSource; p.ActiveSource = null;
                    await Task.Run(() => s.Stop());
                }
            }
        }
        
        NotifySourceStateChanged();
        StatusText = "Streaming stopped.";
    }

    private async Task ToggleRecording()
    {
        if (SelectedPlot?.ActiveSource == null) return;
        var source = SelectedPlot.ActiveSource;

        if (SelectedPlot.IsRecording)
        {
            source.StopRecording(); 
            SelectedPlot.IsRecording = false;
            if (SelectedPlot == SelectedPlot) IsRecording = false;
            StatusText = "Recording stopped.";
        }
        else
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save Raw Stream",
                DefaultExtension = "bin",
                FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("Binary Files") { Patterns = ["*.bin", "*.dat"] }]
            });
            if (file != null)
            {
                source.StartRecording(file.Path.LocalPath); 
                SelectedPlot.IsRecording = true;
                if (SelectedPlot == SelectedPlot) IsRecording = true;
                StatusText = $"Recording to {file.Name}...";
            }
        }
    }
}
