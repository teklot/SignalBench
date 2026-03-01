using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Data;

public interface IDataStore : IDisposable
{
    void InitializeSchema(PacketSchema schema);
    void InsertPackets(IEnumerable<DecodedPacket> packets);
    void InsertDerivedSignal(string name, List<double> data);
    void DeleteSignal(string name);
    List<DateTime> GetTimestamps(int? maxPoints = null);
    List<DateTime> GetTimestamps(int startIndex, int count);
    DateTime GetTimestamp(int index);
    List<double> GetSignalData(string fieldName, int? maxPoints = null);
    int GetRowCount();
    void Reset(string dbPath);
    void Clear();
    List<double> GetSignalData(string fieldName, int startIndex, int count);
}
