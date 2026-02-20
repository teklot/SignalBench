using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Ingestion;

public class BinaryTelemetrySource : ITelemetrySource
{
    private readonly string _filePath;
    private readonly PacketSchema _schema;
    private readonly BinaryDecoder _decoder;
    private readonly int _packetSize;

    public BinaryTelemetrySource(string filePath, PacketSchema schema)
    {
        _filePath = filePath;
        _schema = schema;
        _decoder = new BinaryDecoder();
        _packetSize = CalculatePacketSize(schema);
    }

    private int CalculatePacketSize(PacketSchema schema)
    {
        // Simple calculation for V1 (assuming no gaps/bitfields yet)
        int size = 0;
        foreach (var field in schema.Fields)
        {
            size += GetTypeSize(field.Type);
        }
        return size;
    }

    private int GetTypeSize(FieldType type) => type switch
    {
        FieldType.Uint8 or FieldType.Int8 => 1,
        FieldType.Uint16 or FieldType.Int16 => 2,
        FieldType.Uint32 or FieldType.Int32 or FieldType.Float32 => 4,
        FieldType.Uint64 or FieldType.Float64 => 8,
        _ => 0
    };

    public long TotalRecords => new FileInfo(_filePath).Length / (_packetSize > 0 ? _packetSize : 1);

    public IEnumerable<DecodedPacket> ReadPackets()
    {
        using var stream = File.OpenRead(_filePath);
        byte[] buffer = new byte[_packetSize];

        while (stream.Read(buffer, 0, _packetSize) == _packetSize)
        {
            yield return _decoder.Decode(buffer, _schema);
        }
    }

    public void Seek(long position)
    {
        // position here would be record index
    }
}
