using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalBench.Core.Models;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Avalonia;

namespace SignalBench.ViewModels;

public partial class SerialDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _selectedPort = string.Empty;

    [ObservableProperty]
    private int _selectedBaudRate = 115200;

    [ObservableProperty]
    private string _parity = "None";

    [ObservableProperty]
    private int _dataBits = 8;

    [ObservableProperty]
    private string _stopBits = "One";

    [ObservableProperty]
    private int _rollingWindowSeconds = 10;

    [ObservableProperty]
    private string? _loadedSchemaPath;

    [ObservableProperty]
    private string[] _availablePorts = [];

    public int[] AvailableBaudRates { get; } = [9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];
    public string[] ParityOptions { get; } = ["None", "Odd", "Even", "Mark", "Space"];
    public string[] StopBitsOptions { get; } = ["None", "One", "Two", "OnePointFive"];

    public event Action<bool>? RequestClose;

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            await ShowError("Validation Error", "Please select a COM port.");
            return;
        }

        if (string.IsNullOrWhiteSpace(LoadedSchemaPath))
        {
            await ShowError("Validation Error", "Please select a Schema File for Serial streaming.");
            return;
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts = System.IO.Ports.SerialPort.GetPortNames();
        if (string.IsNullOrEmpty(SelectedPort) && AvailablePorts.Length > 0)
        {
            SelectedPort = AvailablePorts[0];
        }
    }

    [RelayCommand]
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

    public SerialDialogViewModel(SerialSettings settings, string? currentSchemaPath)
    {
        _selectedPort = settings.Port;
        _selectedBaudRate = settings.BaudRate;
        _parity = settings.Parity;
        _dataBits = settings.DataBits;
        _stopBits = settings.StopBits;
        _rollingWindowSeconds = settings.RollingWindowSeconds;
        _loadedSchemaPath = currentSchemaPath;

        RefreshPorts();
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

    public void ApplyTo(SerialSettings settings)
    {
        settings.Port = SelectedPort;
        settings.BaudRate = SelectedBaudRate;
        settings.Parity = Parity;
        settings.DataBits = DataBits;
        settings.StopBits = StopBits;
        settings.RollingWindowSeconds = RollingWindowSeconds;
    }
}