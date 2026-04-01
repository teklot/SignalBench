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

public class CrcDefinition
{
    public CrcType Type { get; set; } = CrcType.Crc16;
    public uint Polynomial { get; set; } = 0x1021;
    public uint InitialValue { get; set; } = 0xFFFF;
    public uint FinalXor { get; set; } = 0x0000;
    public bool ReflectInput { get; set; } = false;
    public bool ReflectOutput { get; set; } = false;
    public int BitOffset { get; set; }
    public int BitLength { get; set; } = 16;
}

public class PacketSchema
{
    public string Name { get; set; } = string.Empty;
    public uint? SyncWord { get; set; }
    public Endianness Endianness { get; set; } = Endianness.Little;
    public List<FieldDefinition> Fields { get; set; } = [];
    public CrcDefinition? Crc { get; set; }
    public int Version { get; set; } = 1;
}
