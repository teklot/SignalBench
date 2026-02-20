namespace SignalBench.Core.Models;

public class UserSettings
{
    public string DefaultTelemetryPath { get; set; } = string.Empty;
    public string DefaultSchemaPath { get; set; } = string.Empty;
    public string Theme { get; set; } = "System"; // System, Light, Dark
    public bool AutoLoadLastSession { get; set; } = false;
    public string LastSessionPath { get; set; } = string.Empty;
    public List<string> RecentFiles { get; set; } = [];
    public int MaxRecentFiles { get; set; } = 10;
}
