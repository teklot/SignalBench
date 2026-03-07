using ReactiveUI;
using SignalBench.Core.Data;
using SignalBench.Core.Models;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Session;
using SignalBench.SDK.Interfaces;
using System.Collections.ObjectModel;

namespace SignalBench.ViewModels;

public enum PlotSourceType { None, File, Serial, Network }

public sealed class PlotViewModel : TabViewModelBase
{
    public override string TabTypeId => "SignalBench.Plot";

    private PlotSourceType _sourceType = PlotSourceType.None;
    public PlotSourceType SourceType
    {
        get => _sourceType;
        set {
            this.RaiseAndSetIfChanged(ref _sourceType, value);
            this.RaisePropertyChanged(nameof(ConnectionInfo));
            this.RaisePropertyChanged(nameof(ConnectionIcon));
        }
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

    private string? _serialSchemaPath;
    public string? SerialSchemaPath
    {
        get => _serialSchemaPath;
        set => this.RaiseAndSetIfChanged(ref _serialSchemaPath, value);
    }

    private string? _networkSchemaPath;
    public string? NetworkSchemaPath
    {
        get => _networkSchemaPath;
        set => this.RaiseAndSetIfChanged(ref _networkSchemaPath, value);
    }

    public string? SchemaPath
    {
        get => SourceType == PlotSourceType.Serial ? SerialSchemaPath : (SourceType == PlotSourceType.Network ? NetworkSchemaPath : null);
        set {
            if (SourceType == PlotSourceType.Serial) SerialSchemaPath = value;
            else if (SourceType == PlotSourceType.Network) NetworkSchemaPath = value;
            this.RaisePropertyChanged(nameof(SchemaPath));
        }
    }

    private bool _isStreaming;
    public bool IsStreaming
    {
        get => _isStreaming;
        set {
            this.RaiseAndSetIfChanged(ref _isStreaming, value);
            this.RaisePropertyChanged(nameof(ConnectionInfo));
            this.RaisePropertyChanged(nameof(ConnectionIcon));
        }
    }

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set => this.RaiseAndSetIfChanged(ref _isPaused, value);
    }

    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        set => this.RaiseAndSetIfChanged(ref _isRecording, value);
    }

    public IStreamingSource? ActiveSource { get; set; }

    public SerialSettings SerialSettings { get; } = new();
    public NetworkSettings NetworkSettings { get; } = new();
    public CsvSettings CsvSettings { get; set; } = new();

    public bool IsSerialConfigured => !string.IsNullOrEmpty(SerialSettings.Port);
    public bool IsNetworkConfigured => !string.IsNullOrEmpty(NetworkSettings.IpAddress) && NetworkSettings.Port > 0;

    public override string ConnectionInfo
    {
        get
        {
            if (SourceType == PlotSourceType.None) return "";
            if (SourceType == PlotSourceType.File) return TelemetryPath != null ? System.IO.Path.GetFileName(TelemetryPath) : "";
            
            if (SourceType == PlotSourceType.Serial && IsSerialConfigured) return GetSerialInfo();
            if (SourceType == PlotSourceType.Network && IsNetworkConfigured) return GetNetworkInfo();
            
            return "";
        }
    }

    public override string ConnectionIcon
    {
        get
        {
            return SourceType switch
            {
                PlotSourceType.File => "FileChartOutline",
                PlotSourceType.Network => "Lan",
                PlotSourceType.Serial => "SerialPort",
                _ => "InformationOutline"
            };
        }
    }

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

    private bool _isSignalsPaneOpen = true;
    public bool IsSignalsPaneOpen
    {
        get => _isSignalsPaneOpen;
        set {
            this.RaiseAndSetIfChanged(ref _isSignalsPaneOpen, value);
            this.RaisePropertyChanged(nameof(SignalsPaneColumnWidth));
        }
    }

    private Avalonia.Controls.GridLength _signalsPaneColumnWidth = new(200);
    public Avalonia.Controls.GridLength SignalsPaneColumnWidth
    {
        get => IsSignalsPaneOpen ? _signalsPaneColumnWidth : new Avalonia.Controls.GridLength(0);
        set {
            if (IsSignalsPaneOpen && value.IsAbsolute)
            {
                if (value.Value < 150)
                {
                    IsSignalsPaneOpen = false;
                }
                else
                {
                    _signalsPaneColumnWidth = value;
                }
            }
            this.RaisePropertyChanged(nameof(SignalsPaneColumnWidth));
        }
    }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ToggleSignalsPaneCommand { get; }

    public PlotViewModel(string name, IDataStore dataStore)
    {
        Name = name;
        DataStore = dataStore;
        ToggleSignalsPaneCommand = ReactiveCommand.Create(() => { IsSignalsPaneOpen = !IsSignalsPaneOpen; });
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
            { "IsSignalsPaneOpen", IsSignalsPaneOpen }
        };
    }

    public override void LoadSettings(Dictionary<string, object> settings)
    {
        if (settings.TryGetValue("SourceType", out var sourceType)) SourceType = Enum.Parse<PlotSourceType>(sourceType.ToString()!);
        if (settings.TryGetValue("TelemetryPath", out var path)) TelemetryPath = path.ToString();
        if (settings.TryGetValue("IsSignalsPaneOpen", out var paneOpen)) IsSignalsPaneOpen = (bool)paneOpen;
        
        // Complex objects might need better handling depending on how YAML deserializes to Dictionary<string, object>
        // For now, we assume the core logic will handle the specific TabSession properties which overlap with these for the Plot type.
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
