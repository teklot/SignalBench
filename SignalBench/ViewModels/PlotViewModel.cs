using ReactiveUI;
using SignalBench.Core.Data;
using SignalBench.Core.Models;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Session;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SignalBench.ViewModels;

public class PlotViewModel : ViewModelBase
{
    private string _name = "New Plot";
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private string? _telemetryPath;
    public string? TelemetryPath
    {
        get => _telemetryPath;
        set => this.RaiseAndSetIfChanged(ref _telemetryPath, value);
    }

    private PacketSchema? _schema;
    public PacketSchema? Schema
    {
        get => _schema;
        set => this.RaiseAndSetIfChanged(ref _schema, value);
    }

    private bool _isStreaming;
    public bool IsStreaming
    {
        get => _isStreaming;
        set => this.RaiseAndSetIfChanged(ref _isStreaming, value);
    }

    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        set => this.RaiseAndSetIfChanged(ref _isRecording, value);
    }

    public SerialSettings SerialSettings { get; } = new();
    public NetworkSettings NetworkSettings { get; } = new();

    private int _totalRecords;
    public int TotalRecords
    {
        get => _totalRecords;
        set => this.RaiseAndSetIfChanged(ref _totalRecords, value);
    }

    public IDataStore DataStore { get; }
    public ObservableCollection<SignalItemViewModel> AvailableSignals { get; } = [];
    public ObservableCollection<SignalItemViewModel> RegularSignals { get; } = [];
    public ObservableCollection<DerivedSignalDefinition> DerivedSignals { get; } = [];
    public ObservableCollection<string> SelectedSignalNames { get; } = [];

    // Playback state per plot
    public List<DateTime> PlaybackTimestamps { get; set; } = [];
    public Dictionary<string, List<double>> PlaybackSignalData { get; set; } = [];
    public int CurrentPlaybackIndex { get; set; }
    public double SavedElapsedSeconds { get; set; }
    public double FullDuration { get; set; }

    // Events for the View to hook into
    public Action<List<DateTime>, Dictionary<string, List<double>>, DateTime?, double?, int?>? RequestPlotUpdate;
    public Action<DateTime>? RequestCursorUpdate;
    public Action? RequestPlotClear;

    public PlotViewModel(string name, IDataStore dataStore)
    {
        Name = name;
        DataStore = dataStore;
    }

    public void ToggleSignal(string signalName)
    {
        if (SelectedSignalNames.Contains(signalName))
        {
            SelectedSignalNames.Remove(signalName);
        }
        else
        {
            SelectedSignalNames.Add(signalName);
        }
    }

    public bool IsSignalSelected(string signalName)
    {
        return SelectedSignalNames.Contains(signalName);
    }

    public void Dispose()
    {
        DataStore.Dispose();
    }
}
