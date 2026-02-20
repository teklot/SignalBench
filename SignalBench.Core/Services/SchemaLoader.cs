using SignalBench.Core.Models.Schema;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SignalBench.Core.Services;

public class SchemaFile
{
    public PacketSchema Packet { get; set; } = new();
}

public class SchemaLoader
{
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public SchemaLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    public PacketSchema Load(string yaml)
    {
        var file = _deserializer.Deserialize<SchemaFile>(yaml);
        if (file == null || file.Packet == null)
            throw new Exception("Invalid schema file format.");

        int currentBitOffset = 0;
        foreach (var field in file.Packet.Fields)
        {
            if (field.BitLength == 0)
            {
                field.BitLength = GetTypeBitCount(field.Type);
            }

            if (field.BitOffset == 0 && field != file.Packet.Fields[0])
            {
                field.BitOffset = currentBitOffset;
            }
            else if (field.BitOffset != 0)
            {
                // If BitOffset is specified, update currentBitOffset for subsequent fields
                currentBitOffset = field.BitOffset;
            }

            currentBitOffset = field.BitOffset + field.BitLength;
        }

        return file.Packet;
    }

    private int GetTypeBitCount(FieldType type) => type switch
    {
        FieldType.Uint8 or FieldType.Int8 => 8,
        FieldType.Uint16 or FieldType.Int16 => 16,
        FieldType.Uint32 or FieldType.Int32 or FieldType.Float32 => 32,
        FieldType.Uint64 or FieldType.Float64 => 64,
        _ => 0
    };

    public string Save(PacketSchema schema)
    {
        return _serializer.Serialize(new SchemaFile { Packet = schema });
    }
}
