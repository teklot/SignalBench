using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalBench.Core.Models;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Avalonia;

namespace SignalBench.ViewModels;

public partial class NetworkDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _networkProtocol = "UDP";

    [ObservableProperty]
    private string _networkIp = "127.0.0.1";

    [ObservableProperty]
    private int _networkPort = 5000;

    [ObservableProperty]
    private int _rollingWindowSeconds = 10;

    [ObservableProperty]
    private string? _loadedSchemaPath;

    public string[] NetworkProtocols { get; } = ["UDP", "TCP"];

    public event Action<bool>? RequestClose;

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(NetworkIp))
        {
            await ShowError("Validation Error", "Network IP Address is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(LoadedSchemaPath))
        {
            await ShowError("Validation Error", "Please select a Schema File for Network streaming.");
            return;
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);

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

    public NetworkDialogViewModel(NetworkSettings settings, string? currentSchemaPath)
    {
        _networkProtocol = settings.Protocol;
        _networkIp = settings.IpAddress;
        _networkPort = settings.Port;
        _rollingWindowSeconds = settings.RollingWindowSeconds;
        _loadedSchemaPath = currentSchemaPath;
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

    public void ApplyTo(NetworkSettings settings)
    {
        settings.Protocol = NetworkProtocol;
        settings.IpAddress = NetworkIp;
        settings.Port = NetworkPort;
        settings.RollingWindowSeconds = RollingWindowSeconds;
    }
}