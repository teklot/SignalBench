using SignalBench.Core.Decoding;

namespace SignalBench.Core.Ingestion;

public interface IStreamingSource : IDisposable
{
    void Start();
    void Stop();
    void StartRecording(string filePath);
    void StopRecording();
    
    event Action<DecodedPacket>? PacketReceived;
    event Action<string>? ErrorReceived;
}
