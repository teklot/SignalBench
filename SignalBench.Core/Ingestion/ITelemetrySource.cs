using SignalBench.Core.Decoding;

namespace SignalBench.Core.Ingestion;

public interface ITelemetrySource
{
    IEnumerable<DecodedPacket> ReadPackets();
    void Seek(long position);
    long TotalRecords { get; }
}
