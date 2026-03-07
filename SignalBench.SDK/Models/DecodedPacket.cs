namespace SignalBench.SDK.Models;

/// <summary>
/// Represents a single decoded unit of telemetry data.
/// </summary>
public sealed record DecodedPacket
{
    public required string SchemaName { get; init; }
    public required DateTime Timestamp { get; init; }
    public Dictionary<string, object> Fields { get; init; } = [];
}
