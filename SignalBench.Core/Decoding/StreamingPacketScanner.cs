using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Decoding;

public class StreamingPacketScanner
{
    public class ScanResult
    {
        public List<DecodedPacket> Packets { get; set; } = [];
        public bool MisalignmentDetected { get; set; }
    }

    private readonly PacketSchema _schema;
    private readonly BinaryDecoder _decoder;
    private readonly int _packetSize;
    private readonly List<byte> _internalBuffer = new();
    private readonly uint? _syncWord;
    private readonly int _syncWordSize = 2;
    private int _consecutiveBadSyncs;

    public StreamingPacketScanner(PacketSchema schema)
    {
        _schema = schema;
        _decoder = new BinaryDecoder();
        _packetSize = CalculatePacketSize(schema);
        _syncWord = schema.SyncWord;
    }

    private int CalculatePacketSize(PacketSchema schema)
    {
        if (schema?.Fields == null) return 0;
        int size = 0;
        foreach (var field in schema.Fields)
        {
            size += GetTypeSize(field.Type);
        }
        // If we have a sync word, it's ALREADY included in the schema fields? 
        // Let's check SchemaLoader or DecodingTests.
        // Actually, usually the sync word is NOT a field in the schema fields list.
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

    public ScanResult PushData(byte[] data)
    {
        _internalBuffer.AddRange(data);
        var result = new ScanResult();

        while (true)
        {
            if (_syncWord.HasValue)
            {
                int syncIndex = FindSyncWord();
                if (syncIndex == -1)
                {
                    // No sync word found. 
                    // Keep only enough bytes to match a potential sync word that is split across buffers
                    if (_internalBuffer.Count >= _syncWordSize)
                    {
                        _internalBuffer.RemoveRange(0, _internalBuffer.Count - (_syncWordSize - 1));
                    }
                    break;
                }

                // Discard everything before the sync word
                if (syncIndex > 0)
                {
                    result.MisalignmentDetected = true;
                    _consecutiveBadSyncs++;
                    if (_consecutiveBadSyncs > 10)
                    {
                        ErrorReceived?.Invoke($"Multiple misalignment detected ({_consecutiveBadSyncs} times), forcing resync");
                    }
                    _internalBuffer.RemoveRange(0, syncIndex);
                }
                else
                {
                    _consecutiveBadSyncs = 0;
                }

                // The BinaryDecoder.Decode expects the data stream to START at the first field.
                // We must check if the sync word itself is part of the fields.
                // In this project's architecture, the decoder doesn't know about sync words, it just decodes fields.
                // So if we found a sync word, we should probably SKIP it before passing to decoder, 
                // UNLESS the sync word is also defined as a field in the schema.
                
                bool syncIsField = _schema.Fields.Any(f => f.Name.Equals("sync", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("syncword", StringComparison.OrdinalIgnoreCase));
                int dataStartIndex = syncIsField ? 0 : _syncWordSize;
                int requiredSize = _packetSize + (syncIsField ? 0 : _syncWordSize);

                if (_internalBuffer.Count < requiredSize)
                {
                    break;
                }

                byte[] packetData = _internalBuffer.Skip(dataStartIndex).Take(_packetSize).ToArray();
                result.Packets.Add(_decoder.Decode(packetData, _schema));
                
                // Remove the processed packet (including the sync word)
                _internalBuffer.RemoveRange(0, requiredSize);
            }
            else
            {
                if (_internalBuffer.Count < _packetSize) break;

                byte[] packetData = _internalBuffer.Take(_packetSize).ToArray();
                result.Packets.Add(_decoder.Decode(packetData, _schema));
                _internalBuffer.RemoveRange(0, _packetSize);
            }
        }

        return result;
    }

    public event Action<string>? ErrorReceived;

    private int FindSyncWord()
    {
        if (!_syncWord.HasValue) return 0;

        for (int i = 0; i <= _internalBuffer.Count - _syncWordSize; i++)
        {
            bool match = true;
            for (int j = 0; j < _syncWordSize; j++)
            {
                byte expected = (byte)((_syncWord.Value >> (8 * (_syncWordSize - 1 - j))) & 0xFF);
                if (_internalBuffer[i + j] != expected)
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }
}
