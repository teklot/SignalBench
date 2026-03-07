using SignalBench.SDK.Models;

namespace SignalBench.SDK.Interfaces;

/// <summary>
/// Defines a source that produces live telemetry streams.
/// </summary>
public interface IStreamingSource : ITelemetrySource
{
    /// <summary>
    /// Event raised when a new packet is decoded.
    /// </summary>
    event Action<DecodedPacket> PacketReceived;

    /// <summary>
    /// Event raised when an ingestion or decoding error occurs.
    /// </summary>
    event Action<string> ErrorReceived;

    /// <summary>
    /// Starts the streaming process.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the streaming process.
    /// </summary>
    void Stop();

    /// <summary>
    /// Starts recording the raw stream to a file.
    /// </summary>
    void StartRecording(string path);

    /// <summary>
    /// Stops recording.
    /// </summary>
    void StopRecording();
}
