namespace SignalBench.Core.Decoding;

public class DecodedPacket
{
    public string SchemaName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Fields { get; set; } = [];
}
