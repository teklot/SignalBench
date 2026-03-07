using SignalBench.SDK.Models;

namespace SignalBench.SDK.Interfaces;

/// <summary>
/// Defines a source of telemetry data.
/// </summary>
public interface ITelemetrySource : IDisposable
{
    /// <summary>
    /// Reads all available packets from the source.
    /// </summary>
    IEnumerable<DecodedPacket> ReadPackets();

    /// <summary>
    /// Seeks to a specific position in the data source if supported.
    /// </summary>
    void Seek(long position);
}
