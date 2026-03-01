using ReactiveUI;
using SignalBench.Core.Services;
using System.Reactive;

namespace SignalBench.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    private string _defaultTelemetryPath;
    public string DefaultTelemetryPath
    {
        get => _defaultTelemetryPath;
        set => this.RaiseAndSetIfChanged(ref _defaultTelemetryPath, value);
    }

    private string _defaultSchemaPath;
    public string DefaultSchemaPath
    {
        get => _defaultSchemaPath;
        set => this.RaiseAndSetIfChanged(ref _defaultSchemaPath, value);
    }

    private string _theme;
    public string Theme
    {
        get => _theme;
        set => this.RaiseAndSetIfChanged(ref _theme, value);
    }

    private string _storageMode;
    public string StorageMode
    {
        get => _storageMode;
        set => this.RaiseAndSetIfChanged(ref _storageMode, value);
    }

    private bool _autoLoadLastSession;
    public bool AutoLoadLastSession
    {
        get => _autoLoadLastSession;
        set => this.RaiseAndSetIfChanged(ref _autoLoadLastSession, value);
    }

    // Serial Settings
    private string _selectedPort;
    public string SelectedPort
    {
        get => _selectedPort;
        set => this.RaiseAndSetIfChanged(ref _selectedPort, value);
    }

    private int _selectedBaudRate;
    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set => this.RaiseAndSetIfChanged(ref _selectedBaudRate, value);
    }

    private string _parity;
    public string Parity
    {
        get => _parity;
        set => this.RaiseAndSetIfChanged(ref _parity, value);
    }

    private int _dataBits;
    public int DataBits
    {
        get => _dataBits;
        set => this.RaiseAndSetIfChanged(ref _dataBits, value);
    }

    private string _stopBits;
    public string StopBits
    {
        get => _stopBits;
        set => this.RaiseAndSetIfChanged(ref _stopBits, value);
    }

    private int _rollingBufferSize;
    public int RollingBufferSize
    {
        get => _rollingBufferSize;
        set => this.RaiseAndSetIfChanged(ref _rollingBufferSize, value);
    }

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
    }

    public string[] Themes { get; } = ["System", "Light", "Dark"];
    public string[] StorageModes { get; } = ["InMemory", "Sqlite"];
    public string[] AvailablePorts { get; set; } = [];
    public int[] AvailableBaudRates { get; } = [9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];
    public string[] ParityOptions { get; } = ["None", "Odd", "Even", "Mark", "Space"];
    public string[] StopBitsOptions { get; } = ["None", "One", "Two", "OnePointFive"];

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<string, Unit> BrowsePathCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshPortsCommand { get; }

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        
        var current = _settingsService.Current;
        _defaultTelemetryPath = current.DefaultTelemetryPath;
        _defaultSchemaPath = current.DefaultSchemaPath;
        _theme = current.Theme;
        _storageMode = current.StorageMode;
        _autoLoadLastSession = current.AutoLoadLastSession;

        // Load Serial Settings
        _selectedPort = current.LastPort;
        _selectedBaudRate = current.LastBaudRate;
        _parity = current.Parity;
        _dataBits = current.DataBits;
        _stopBits = current.StopBits;
        _rollingBufferSize = current.RollingBufferSize;

        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(() => { });
        BrowsePathCommand = ReactiveCommand.CreateFromTask<string>(BrowsePathAsync);
        RefreshPortsCommand = ReactiveCommand.Create(RefreshPorts);

        RefreshPorts();
    }

    private void RefreshPorts()
    {
        AvailablePorts = System.IO.Ports.SerialPort.GetPortNames();
        this.RaisePropertyChanged(nameof(AvailablePorts));
        if (string.IsNullOrEmpty(SelectedPort) && AvailablePorts.Length > 0)
        {
            SelectedPort = AvailablePorts[0];
        }
    }

    private void Save()
    {
        var current = _settingsService.Current;
        current.DefaultTelemetryPath = DefaultTelemetryPath;
        current.DefaultSchemaPath = DefaultSchemaPath;
        current.Theme = Theme;
        current.StorageMode = StorageMode;
        current.AutoLoadLastSession = AutoLoadLastSession;

        // Save Serial Settings
        current.LastPort = SelectedPort;
        current.LastBaudRate = SelectedBaudRate;
        current.Parity = Parity;
        current.DataBits = DataBits;
        current.StopBits = StopBits;
        current.RollingBufferSize = RollingBufferSize;

        _settingsService.Save();
    }

    private async Task BrowsePathAsync(string target)
    {
        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = $"Select Default {target} Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            if (target == "Telemetry") DefaultTelemetryPath = path;
            else if (target == "Schema") DefaultSchemaPath = path;
        }
    }
}
