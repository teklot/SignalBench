namespace SignalBench.Core.Models.Schema;

public class FieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public FieldType Type { get; set; }
    public int BitOffset { get; set; }
    public int BitLength { get; set; }
    // For V1, we might just use Type to determine size, but bitfields are mentioned.
}

public enum Endianness
{
    Little,
    Big
}

public class PacketSchema
{
    public string Name { get; set; } = string.Empty;
    public uint? SyncWord { get; set; }
    public Endianness Endianness { get; set; } = Endianness.Little;
    public List<FieldDefinition> Fields { get; set; } = [];
    public int Version { get; set; } = 1;
}
