using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Session;

public class DerivedSignalDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
}

public class ProjectSession
{
    public string TelemetryFilePath { get; set; } = string.Empty;
    public string SchemaPath { get; set; } = string.Empty;
    public List<string> ActivePlotSignals { get; set; } = [];
    public List<DerivedSignalDefinition> DerivedSignals { get; set; } = [];
    // UI Layout could be added here in V2
}
