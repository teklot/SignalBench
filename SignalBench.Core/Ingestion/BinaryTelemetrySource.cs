using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;
using System.Collections.Concurrent;

namespace SignalBench.Core.Ingestion;

public class BinaryTelemetrySource : ITelemetrySource
{
    private readonly string _filePath;
    private readonly PacketSchema _schema;
    private readonly BinaryDecoder _decoder;
    private readonly BinaryPacketScanner _scanner;
    private readonly int _packetSize;
    private readonly bool _useSyncWord;
    private const int ChunkSize = 1024 * 1024;

    public BinaryTelemetrySource(string filePath, PacketSchema schema)
    {
        _filePath = filePath;
        _schema = schema;
        _decoder = new BinaryDecoder();
        _scanner = new BinaryPacketScanner();
        _packetSize = CalculatePacketSize(schema);
        _useSyncWord = schema.SyncWord.HasValue;
    }

    private int CalculatePacketSize(PacketSchema schema)
    {
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

    public long TotalRecords
    {
        get
        {
            if (_useSyncWord)
            {
                using var stream = File.OpenRead(_filePath);
                return _scanner.ScanForSyncWord(stream, _schema.SyncWord!.Value).Count();
            }
            return new FileInfo(_filePath).Length / (_packetSize > 0 ? _packetSize : 1);
        }
    }

    public IEnumerable<DecodedPacket> ReadPackets()
    {
        if (_useSyncWord && _schema.SyncWord.HasValue)
        {
            return ReadPacketsWithSyncWord();
        }
        return ReadPacketsParallel();
    }

    private IEnumerable<DecodedPacket> ReadPacketsWithSyncWord()
    {
        using var stream = File.OpenRead(_filePath);
        var syncPositions = _scanner.ScanForSyncWord(stream, _schema.SyncWord!.Value).ToList();

        foreach (var position in syncPositions)
        {
            stream.Position = position;
            byte[] buffer = new byte[_packetSize];
            int bytesRead = stream.Read(buffer, 0, _packetSize);
            if (bytesRead == _packetSize)
            {
                yield return _decoder.Decode(buffer, _schema);
            }
        }
    }

    private IEnumerable<DecodedPacket> ReadPacketsParallel()
    {
        var fileInfo = new FileInfo(_filePath);
        var fileLength = fileInfo.Length;

        if (fileLength < ChunkSize * 2)
        {
            return ReadPacketsSequential();
        }

        var packets = new ConcurrentBag<DecodedPacket>();
        var totalChunks = (int)((fileLength + ChunkSize - 1) / ChunkSize);
        var threads = Math.Max(1, Environment.ProcessorCount - 1);

        var options = new ParallelOptions { MaxDegreeOfParallelism = threads };

        Parallel.For(0, totalChunks, options, chunkIndex =>
        {
            var startByte = chunkIndex * ChunkSize;
            var localPackets = new List<DecodedPacket>();

            lock (_scanner)
            {
                using var stream = File.OpenRead(_filePath);
                stream.Position = startByte;

                var alignOffset = startByte % _packetSize;
                if (alignOffset != 0)
                {
                    stream.Position += (_packetSize - alignOffset);
                    startByte = (int)stream.Position;
                }

                var buffer = new byte[_packetSize];
                while (stream.Position + _packetSize <= startByte + ChunkSize && stream.Position < fileLength)
                {
                    var bytesRead = stream.Read(buffer, 0, _packetSize);
                    if (bytesRead == _packetSize)
                    {
                        localPackets.Add(_decoder.Decode(buffer, _schema));
                    }
                    else
                    {
                        break;
                    }
                }
            }

            foreach (var p in localPackets)
            {
                packets.Add(p);
            }
        });

        return packets.OrderBy(p => p.Timestamp).ToList();
    }

    private IEnumerable<DecodedPacket> ReadPacketsSequential()
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
    }
}
