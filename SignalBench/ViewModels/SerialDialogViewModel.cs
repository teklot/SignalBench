using ReactiveUI;
using SignalBench.Core.Models;
using System.Reactive;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Avalonia;

namespace SignalBench.ViewModels;

public class SerialDialogViewModel : ViewModelBase
{
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

    private int _rollingWindowSeconds;
    public int RollingWindowSeconds
    {
        get => _rollingWindowSeconds;
        set => this.RaiseAndSetIfChanged(ref _rollingWindowSeconds, value);
    }

    private string? _loadedSchemaPath;
    public string? LoadedSchemaPath
    {
        get => _loadedSchemaPath;
        set => this.RaiseAndSetIfChanged(ref _loadedSchemaPath, value);
    }

    public string[] AvailablePorts { get; set; } = [];
    public int[] AvailableBaudRates { get; } = [9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];
    public string[] ParityOptions { get; } = ["None", "Odd", "Even", "Mark", "Space"];
    public string[] StopBitsOptions { get; } = ["None", "One", "Two", "OnePointFive"];

    public ReactiveCommand<Unit, bool> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshPortsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSchemaCommand { get; }

    public SerialDialogViewModel(SerialSettings settings, string? currentSchemaPath)
    {
        _selectedPort = settings.Port;
        _selectedBaudRate = settings.BaudRate;
        _parity = settings.Parity;
        _dataBits = settings.DataBits;
        _stopBits = settings.StopBits;
        _rollingWindowSeconds = settings.RollingWindowSeconds;
        _loadedSchemaPath = currentSchemaPath;

        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(() => { });
        RefreshPortsCommand = ReactiveCommand.Create(RefreshPorts);
        OpenSchemaCommand = ReactiveCommand.CreateFromTask(OpenSchemaAsync);

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

    private async Task ShowError(string title, string message)
    {
        var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(title, message);
        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel != null)
        {
            await box.ShowWindowDialogAsync(topLevel);
        }
    }

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            _ = ShowError("Validation Error", "Please select a COM port.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(LoadedSchemaPath))
        {
            _ = ShowError("Validation Error", "Please select a Schema File for Serial streaming.");
            return false;
        }

        return true;
    }

    private bool Save()
    {
        if (!Validate()) return false;
        return true;
    }

    public void ApplyTo(SerialSettings settings)
    {
        settings.Port = SelectedPort;
        settings.BaudRate = SelectedBaudRate;
        settings.Parity = Parity;
        settings.DataBits = DataBits;
        settings.StopBits = StopBits;
        settings.RollingWindowSeconds = RollingWindowSeconds;
    }

    private async Task OpenSchemaAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open Packet Schema",
                AllowMultiple = false,
                FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("YAML Files") { Patterns = ["*.yaml", "*.yml"] }]
            });

            if (files.Count > 0)
            {
                LoadedSchemaPath = files[0].Path.LocalPath;
            }
        }
        catch { }
    }
}
