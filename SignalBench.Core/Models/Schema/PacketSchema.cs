namespace SignalBench.Core.Models.Schema;

public class FieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public FieldType Type { get; set; }
    public int BitOffset { get; set; }
    public int BitLength { get; set; }
    
    // Metadata & Transformation
    public double Scale { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public string? Unit { get; set; }
    public string? Description { get; set; }
    
    // Categorical Mapping
    public Dictionary<double, string>? Lookup { get; set; }
    
    // Nested Fields support
    public List<FieldDefinition>? Fields { get; set; }
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
