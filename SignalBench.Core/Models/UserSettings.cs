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
    public string StorageMode { get; set; } = "InMemory"; // InMemory, Sqlite

    // Serial Settings
    public string LastPort { get; set; } = string.Empty;
    public int LastBaudRate { get; set; } = 115200;
    public string Parity { get; set; } = "None";
    public int DataBits { get; set; } = 8;
    public string StopBits { get; set; } = "One";
    public int RollingBufferSize { get; set; } = 500;
}
