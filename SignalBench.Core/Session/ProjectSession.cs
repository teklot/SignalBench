using SignalBench.Core.Models;

namespace SignalBench.Core.Session;

public class DerivedSignalDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
}

public class CsvSettings
{
    public string Delimiter { get; set; } = ",";
    public string? TimestampColumn { get; set; }
    public bool HasHeader { get; set; } = true;
}

public class TabSession
{
    public string Name { get; set; } = "New Plot";
    public string TabTypeId { get; set; } = "SignalBench.Plot";
    
    // Generic settings for plugins
    public Dictionary<string, object> Settings { get; set; } = [];

    // Legacy/Core Plot settings - Kept for compatibility with existing logic
    public string SourceType { get; set; } = "None"; // None, File, Serial, Network
    public string? TelemetryPath { get; set; }
    public string? SchemaYaml { get; set; } // Embedded schema content
    public List<string> SelectedSignalNames { get; set; } = [];
    public List<DerivedSignalDefinition> DerivedSignals { get; set; } = [];
    
    // Core settings objects
    public SerialSettings? SerialSettings { get; set; }
    public NetworkSettings? NetworkSettings { get; set; }
    public CsvSettings? CsvSettings { get; set; }
}

public class ProjectSession
{
    public List<TabSession> Tabs { get; set; } = [];
    public int SelectedTabIndex { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
