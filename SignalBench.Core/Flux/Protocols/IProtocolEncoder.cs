namespace SignalBench.Core.Flux.Protocols;

public interface IProtocolEncoder
{
    string ProtocolName { get; }
    byte[] Encode(Dictionary<string, double> fieldValues);
}
