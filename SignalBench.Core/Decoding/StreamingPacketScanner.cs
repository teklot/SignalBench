using SignalBench.Core.Models.Schema;
using SignalBench.SDK.Models;

namespace SignalBench.Core.Decoding;

public sealed class StreamingPacketScanner(PacketSchema schema)
{
    public sealed class ScanResult
    {
        public List<DecodedPacket> Packets { get; init; } = [];
        public bool MisalignmentDetected { get; set; }
    }

    private readonly BinaryDecoder _decoder = new();
    private readonly int _packetSize = CalculatePacketSize(schema);
    private readonly List<byte> _internalBuffer = [];
    private readonly uint? _syncWord = schema.SyncWord;
    private readonly int _syncWordSize = 2;
    private int _consecutiveBadSyncs;

    private static int CalculatePacketSize(PacketSchema schema)
    {
        if (schema?.Fields == null) return 0;
        int size = 0;
        foreach (var field in schema.Fields)
        {
            size += GetTypeSize(field.Type);
        }
        return size;
    }

    private static int GetTypeSize(FieldType type) => type switch
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

                bool syncIsField = schema.Fields.Any(f => f.Name.Equals("sync", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("syncword", StringComparison.OrdinalIgnoreCase));
                int dataStartIndex = syncIsField ? 0 : _syncWordSize;
                int requiredSize = _packetSize + (syncIsField ? 0 : _syncWordSize);

                if (_internalBuffer.Count < requiredSize)
                {
                    break;
                }

                byte[] packetData = _internalBuffer.Skip(dataStartIndex).Take(_packetSize).ToArray();
                result.Packets.Add(_decoder.Decode(packetData, schema));
                
                // Remove the processed packet (including the sync word)
                _internalBuffer.RemoveRange(0, requiredSize);
            }
            else
            {
                if (_internalBuffer.Count < _packetSize) break;

                byte[] packetData = _internalBuffer.Take(_packetSize).ToArray();
                result.Packets.Add(_decoder.Decode(packetData, schema));
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
