using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalBench.Core.Data;
using SignalBench.Core.Models;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Session;
using SignalBench.SDK.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SignalBench.ViewModels;

public enum PlotSourceType { None, File, Serial, Network }

public sealed partial class PlotViewModel : TabViewModelBase
{
    public override string TabTypeId => "SignalBench.Plot";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionInfo))]
    [NotifyPropertyChangedFor(nameof(ConnectionIcon))]
    private PlotSourceType _sourceType = PlotSourceType.None;

    [ObservableProperty]
    private string? _telemetryPath;

    [ObservableProperty]
    private PacketSchema? _schema;

    [ObservableProperty]
    private string? _serialSchemaPath;

    [ObservableProperty]
    private string? _networkSchemaPath;

    public string? SchemaPath
    {
        get => SourceType == PlotSourceType.Serial ? SerialSchemaPath : (SourceType == PlotSourceType.Network ? NetworkSchemaPath : null);
        set {
            if (SourceType == PlotSourceType.Serial) SerialSchemaPath = value;
            else if (SourceType == PlotSourceType.Network) NetworkSchemaPath = value;
            OnPropertyChanged(nameof(SchemaPath));
        }
    }

    private bool _isStreaming;
    public bool IsStreaming
    {
        get => _isStreaming;
        set {
            if (SetProperty(ref _isStreaming, value))
            {
                OnPropertyChanged(nameof(ConnectionInfo));
                OnPropertyChanged(nameof(ConnectionIcon));
                SourceStateChanged?.Invoke();
            }
        }
    }

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set {
            if (SetProperty(ref _isPaused, value))
            {
                SourceStateChanged?.Invoke();
            }
        }
    }

    [ObservableProperty]
    private bool _isRecording;

    public IStreamingSource? ActiveSource { get; set; }

    public SerialSettings SerialSettings { get; } = new();
    public NetworkSettings NetworkSettings { get; } = new();
    public CsvSettings CsvSettings { get; set; } = new();

    public bool IsSerialConfigured => !string.IsNullOrEmpty(SerialSettings.Port);
    public bool IsNetworkConfigured => !string.IsNullOrEmpty(NetworkSettings.IpAddress) && NetworkSettings.Port > 0;

    public override string ConnectionInfo => SourceType switch
    {
        PlotSourceType.None => "",
        PlotSourceType.File => TelemetryPath != null ? System.IO.Path.GetFileName(TelemetryPath) : "",
        PlotSourceType.Serial when IsSerialConfigured => GetSerialInfo(),
        PlotSourceType.Network when IsNetworkConfigured => GetNetworkInfo(),
        _ => ""
    };

    public override string ConnectionIcon => SourceType switch
    {
        PlotSourceType.File => "FileChartOutline",
        PlotSourceType.Network => "Lan",
        PlotSourceType.Serial => "SerialPort",
        _ => "InformationOutline"
    };

    private string GetSerialInfo()
    {
        var s = SerialSettings;
        string sb = s.StopBits switch {
            "One" => "1",
            "Two" => "2",
            "OnePointFive" => "1.5",
            _ => "0"
        };
        return $"{s.Port}: {s.BaudRate} {s.DataBits}{s.Parity[0]}{sb}";
    }

    private string GetNetworkInfo()
    {
        var n = NetworkSettings;
        return $"{n.Protocol}: {n.IpAddress}:{n.Port}";
    }

    private int _totalRecords;
    public int TotalRecords
    {
        get => _totalRecords;
        set {
            if (SetProperty(ref _totalRecords, value))
            {
                SourceStateChanged?.Invoke();
            }
        }
    }

    public IDataStore DataStore { get; }
    public ObservableCollection<SignalItemViewModel> AvailableSignals { get; } = [];
    public ObservableCollection<SignalItemViewModel> RegularSignals { get; } = [];
    public ObservableCollection<DerivedSignalDefinition> DerivedSignals { get; } = [];
    public ObservableCollection<ThresholdRule> ThresholdRules { get; } = [];
    public ObservableCollection<string> SelectedSignalNames { get; } = [];

    // Playback state per plot
    public List<DateTime> PlaybackTimestamps { get; set; } = [];
    public Dictionary<string, List<double>> PlaybackSignalData { get; set; } = [];
    public int CurrentPlaybackIndex { get; set; }
    public double SavedElapsedSeconds { get; set; }
    public double FullDuration { get; set; }

    // Events for the View and ViewModel to hook into
    public Action<List<DateTime>, Dictionary<string, List<double>>, DateTime?, double?, int?, List<ThresholdViolation>?>? RequestPlotUpdate;
    public Action<DateTime>? RequestCursorUpdate;
    public Action? RequestPlotClear;
    public event Action? SourceStateChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignalsPaneColumnWidth))]
    private bool _isSignalsPaneOpen = true;

    private Avalonia.Controls.GridLength _signalsPaneColumnWidthValue = new(200);
    public Avalonia.Controls.GridLength SignalsPaneColumnWidth
    {
        get => IsSignalsPaneOpen ? _signalsPaneColumnWidthValue : new Avalonia.Controls.GridLength(24);
        set {
            if (IsSignalsPaneOpen && value.IsAbsolute)
            {
                if (value.Value < 150)
                {
                    IsSignalsPaneOpen = false;
                }
                else
                {
                    _signalsPaneColumnWidthValue = value;
                }
            }
            OnPropertyChanged(nameof(SignalsPaneColumnWidth));
        }
    }

    public SignalStatsViewModel Statistics { get; }

    [RelayCommand]
    private void ToggleSignalsPane() => IsSignalsPaneOpen = !IsSignalsPaneOpen;

    public PlotViewModel(string name, IDataStore dataStore)
    {
        Name = name;
        DataStore = dataStore;
        Statistics = new SignalStatsViewModel(dataStore);

        SelectedSignalNames.CollectionChanged += (s, e) => {
            if (Statistics.SelectedSignal == null && SelectedSignalNames.Count > 0)
            {
                var first = AvailableSignals.FirstOrDefault(sig => sig.Name == SelectedSignalNames[0]);
                if (first != null) Statistics.SelectedSignal = first;
            }
            else if (Statistics.SelectedSignal != null && !SelectedSignalNames.Contains(Statistics.SelectedSignal.Name))
            {
                if (SelectedSignalNames.Count > 0)
                {
                    var first = AvailableSignals.FirstOrDefault(sig => sig.Name == SelectedSignalNames[0]);
                    if (first != null) Statistics.SelectedSignal = first;
                }
                else
                {
                    Statistics.SelectedSignal = null;
                }
            }
        };
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

    public bool IsSignalSelected(string signalName) => SelectedSignalNames.Contains(signalName);

    public override Dictionary<string, object> GetSettings()
    {
        return new Dictionary<string, object>
        {
            { "SourceType", SourceType.ToString() },
            { "TelemetryPath", TelemetryPath ?? "" },
            { "SerialSettings", SerialSettings },
            { "NetworkSettings", NetworkSettings },
            { "CsvSettings", CsvSettings },
            { "SelectedSignalNames", SelectedSignalNames.ToList() },
            { "DerivedSignals", DerivedSignals.ToList() },
            { "ThresholdRules", ThresholdRules.ToList() },
            { "IsSignalsPaneOpen", IsSignalsPaneOpen }
        };
    }

    public override void LoadSettings(Dictionary<string, object> settings)
    {
        if (settings.TryGetValue("SourceType", out var sourceType)) SourceType = Enum.Parse<PlotSourceType>(sourceType.ToString()!);
        if (settings.TryGetValue("TelemetryPath", out var path)) TelemetryPath = path.ToString();
        if (settings.TryGetValue("IsSignalsPaneOpen", out var paneOpen)) IsSignalsPaneOpen = (bool)paneOpen;
    }

    public override void Dispose()
    {
        if (ActiveSource != null)
        {
            var s = ActiveSource;
            ActiveSource = null;
            System.Threading.Tasks.Task.Run(() => s.Stop());
        }
        DataStore.Dispose();
    }
}
