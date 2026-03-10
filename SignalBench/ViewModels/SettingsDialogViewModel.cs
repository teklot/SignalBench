using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalBench.Core.Services;
using System.Threading.Tasks;
using System;
using Avalonia;

namespace SignalBench.ViewModels;

public partial class SettingsDialogViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly SignalBench.SDK.Interfaces.IFeatureService _featureService;

    [ObservableProperty]
    private string _defaultTelemetryPath = string.Empty;

    [ObservableProperty]
    private string _licenseKey = string.Empty;

    [ObservableProperty]
    private string _licenseStatus = string.Empty;

    [ObservableProperty]
    private string _defaultSchemaPath = string.Empty;

    [ObservableProperty]
    private string _theme = "System";

    partial void OnThemeChanged(string value) => ApplyTheme(value);

    private void ApplyTheme(string themeName)
    {
        if (Application.Current == null) return;
        
        Application.Current.RequestedThemeVariant = themeName switch
        {
            "Light" => Avalonia.Styling.ThemeVariant.Light,
            "Dark" => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default
        };
    }

    [ObservableProperty]
    private string _storageMode = "InMemory";

    [ObservableProperty]
    private bool _autoLoadLastSession;

    [ObservableProperty]
    private int _selectedTabIndex;

    public string[] Themes { get; } = ["System", "Light", "Dark"];
    public string[] StorageModes { get; } = ["InMemory", "Sqlite"];

    public event Action<bool>? RequestClose;

    [RelayCommand]
    private void Save()
    {
        var current = _settingsService.Current;
        current.DefaultTelemetryPath = DefaultTelemetryPath;
        current.DefaultSchemaPath = DefaultSchemaPath;
        current.Theme = Theme;
        current.StorageMode = StorageMode;
        current.AutoLoadLastSession = AutoLoadLastSession;
        current.LicenseKey = LicenseKey;

        _settingsService.Save();
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);

    [RelayCommand]
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

    [RelayCommand]
    private async Task ValidateLicenseAsync()
    {
        LicenseStatus = "Validating...";
        var status = await _featureService.ValidateLicenseAsync(LicenseKey);
        LicenseStatus = status.ToString();
    }

    public SettingsDialogViewModel(ISettingsService settingsService, SignalBench.SDK.Interfaces.IFeatureService featureService)
    {
        _settingsService = settingsService;
        _featureService = featureService;
        
        var current = _settingsService.Current;
        _defaultTelemetryPath = current.DefaultTelemetryPath;
        _defaultSchemaPath = current.DefaultSchemaPath;
        _theme = current.Theme;
        _storageMode = current.StorageMode;
        _autoLoadLastSession = current.AutoLoadLastSession;
        _licenseKey = current.LicenseKey;
        _licenseStatus = _featureService.CurrentStatus.ToString();
    }
}