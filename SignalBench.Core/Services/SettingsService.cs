using System.Text.Json;
using SignalBench.Core.Models;

namespace SignalBench.Core.Services;

public interface ISettingsService
{
    UserSettings Current { get; }
    void Save();
    void Load();
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    public UserSettings Current { get; private set; } = new();

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var signalBenchDir = Path.Combine(appData, "SignalBench");
        Directory.CreateDirectory(signalBenchDir);
        _settingsPath = Path.Combine(signalBenchDir, "settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                Current = JsonSerializer.Deserialize(json, SignalBenchJsonContext.Default.UserSettings) ?? new UserSettings();
            }
            else
            {
                Current = new UserSettings();
            }
        }
        catch
        {
            Current = new UserSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, SignalBenchJsonContext.Default.UserSettings);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
