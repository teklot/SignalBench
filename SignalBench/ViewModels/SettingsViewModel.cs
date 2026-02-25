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

    public string[] Themes { get; } = ["System", "Light", "Dark"];
    public string[] StorageModes { get; } = ["InMemory", "Sqlite"];

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<string, Unit> BrowsePathCommand { get; }

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        
        var current = _settingsService.Current;
        _defaultTelemetryPath = current.DefaultTelemetryPath;
        _defaultSchemaPath = current.DefaultSchemaPath;
        _theme = current.Theme;
        _storageMode = current.StorageMode;
        _autoLoadLastSession = current.AutoLoadLastSession;

        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(() => { });
        BrowsePathCommand = ReactiveCommand.CreateFromTask<string>(BrowsePathAsync);
    }

    private void Save()
    {
        var current = _settingsService.Current;
        current.DefaultTelemetryPath = DefaultTelemetryPath;
        current.DefaultSchemaPath = DefaultSchemaPath;
        current.Theme = Theme;
        current.StorageMode = StorageMode;
        current.AutoLoadLastSession = AutoLoadLastSession;
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
