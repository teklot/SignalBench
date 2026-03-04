using ReactiveUI;
using SignalBench.Core.Models;
using System.Reactive;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Avalonia;

namespace SignalBench.ViewModels;

public class NetworkDialogViewModel : ViewModelBase
{
    private string _networkProtocol;
    public string NetworkProtocol
    {
        get => _networkProtocol;
        set => this.RaiseAndSetIfChanged(ref _networkProtocol, value);
    }

    private string _networkIp;
    public string NetworkIp
    {
        get => _networkIp;
        set => this.RaiseAndSetIfChanged(ref _networkIp, value);
    }

    private int _networkPort;
    public int NetworkPort
    {
        get => _networkPort;
        set => this.RaiseAndSetIfChanged(ref _networkPort, value);
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

    public string[] NetworkProtocols { get; } = ["UDP", "TCP"];

    public ReactiveCommand<Unit, bool> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSchemaCommand { get; }

    public NetworkDialogViewModel(NetworkSettings settings, string? currentSchemaPath)
    {
        _networkProtocol = settings.Protocol;
        _networkIp = settings.IpAddress;
        _networkPort = settings.Port;
        _rollingWindowSeconds = settings.RollingWindowSeconds;
        _loadedSchemaPath = currentSchemaPath;

        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(() => { });
        OpenSchemaCommand = ReactiveCommand.CreateFromTask(OpenSchemaAsync);
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
        if (string.IsNullOrWhiteSpace(NetworkIp))
        {
            _ = ShowError("Validation Error", "Network IP Address is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(LoadedSchemaPath))
        {
            _ = ShowError("Validation Error", "Please select a Schema File for Network streaming.");
            return false;
        }

        return true;
    }

    private bool Save()
    {
        if (!Validate()) return false;
        return true;
    }

    public void ApplyTo(NetworkSettings settings)
    {
        settings.Protocol = NetworkProtocol;
        settings.IpAddress = NetworkIp;
        settings.Port = NetworkPort;
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
