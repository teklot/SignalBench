namespace SignalBench.Core.Flux.Transport;

public interface ITransport : IDisposable
{
    void Send(byte[] data);
    bool IsConnected { get; }
    void Connect();
}
