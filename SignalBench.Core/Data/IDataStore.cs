using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Data;

public interface IDataStore : IDisposable
{
    void InitializeSchema(PacketSchema schema);
    void InsertPackets(IEnumerable<DecodedPacket> packets);
    void InsertDerivedSignal(string name, List<double> data);
    void DeleteSignal(string name);
    List<DateTime> GetTimestamps();
    List<double> GetSignalData(string fieldName);
    void Reset(string dbPath);
}
