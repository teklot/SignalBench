using ReactiveUI;
using SignalBench.Core.Services;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;

namespace SignalBench.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly SignalBench.SDK.Interfaces.IFeatureService _featureService;

    private string _defaultTelemetryPath;
    public string DefaultTelemetryPath
    {
        get => _defaultTelemetryPath;
        set => this.RaiseAndSetIfChanged(ref _defaultTelemetryPath, value);
    }

    private string _licenseKey;
    public string LicenseKey
    {
        get => _licenseKey;
        set => this.RaiseAndSetIfChanged(ref _licenseKey, value);
    }

    private string _licenseStatus;
    public string LicenseStatus
    {
        get => _licenseStatus;
        set => this.RaiseAndSetIfChanged(ref _licenseStatus, value);
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
        set {
            this.RaiseAndSetIfChanged(ref _theme, value);
            ApplyTheme(value);
        }
    }

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

    public ReactiveCommand<Unit, bool> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<string, Unit> BrowsePathCommand { get; }
    public ReactiveCommand<Unit, Unit> ValidateLicenseCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, SignalBench.SDK.Interfaces.IFeatureService featureService)
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

        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(() => { });
        BrowsePathCommand = ReactiveCommand.CreateFromTask<string>(BrowsePathAsync);
        ValidateLicenseCommand = ReactiveCommand.CreateFromTask(ValidateLicenseAsync);
    }

    private async Task ValidateLicenseAsync()
    {
        LicenseStatus = "Validating...";
        var status = await _featureService.ValidateLicenseAsync(LicenseKey);
        LicenseStatus = status.ToString();
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
        return true;
    }

    private bool Save()
    {
        if (!Validate()) return false;

        var current = _settingsService.Current;
        current.DefaultTelemetryPath = DefaultTelemetryPath;
        current.DefaultSchemaPath = DefaultSchemaPath;
        current.Theme = Theme;
        current.StorageMode = StorageMode;
        current.AutoLoadLastSession = AutoLoadLastSession;
        current.LicenseKey = LicenseKey;

        _settingsService.Save();
        return true;
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
