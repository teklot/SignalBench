using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Data;

public class InMemoryDataStore : IDataStore
{
    private Dictionary<string, List<double>> _signals = new();
    private List<DateTime> _timestamps = [];
    private List<string> _signalNames = [];

    public void InitializeSchema(PacketSchema schema)
    {
        _signals.Clear();
        _timestamps.Clear();
        _signalNames = schema.Fields.Select(f => f.Name).Where(n => !n.Equals("timestamp", StringComparison.OrdinalIgnoreCase)).ToList();
        
        foreach (var name in _signalNames)
        {
            _signals[name] = [];
        }
    }

    public void InsertPackets(IEnumerable<DecodedPacket> packets)
    {
        foreach (var packet in packets)
        {
            _timestamps.Add(packet.Timestamp == default ? DateTime.Now : packet.Timestamp);
            
            foreach (var name in _signalNames)
            {
                if (packet.Fields.TryGetValue(name, out var val))
                {
                    if (val is double d) _signals[name].Add(d);
                    else if (val is float f) _signals[name].Add(f);
                    else if (val is int i) _signals[name].Add(i);
                    else if (val is long l) _signals[name].Add(l);
                    else if (val is double vd) _signals[name].Add(vd);
                    else if (val != null && double.TryParse(val.ToString(), out var parsed))
                        _signals[name].Add(parsed);
                    else
                        _signals[name].Add(double.NaN);
                }
                else
                {
                    _signals[name].Add(double.NaN);
                }
            }
        }
    }

    public void InsertDerivedSignal(string name, List<double> data)
    {
        _signals[name] = data;
        _signalNames.Add(name);
    }

    public void DeleteSignal(string name)
    {
        if (_signals.ContainsKey(name))
        {
            _signals.Remove(name);
            _signalNames.Remove(name);
        }
    }

    public List<DateTime> GetTimestamps(int? maxPoints = null)
    {
        if (!maxPoints.HasValue || maxPoints.Value >= _timestamps.Count)
            return _timestamps;
        
        var step = Math.Max(1, _timestamps.Count / maxPoints.Value);
        return _timestamps.Where((t, i) => i % step == 0).ToList();
    }

    public List<double> GetSignalData(string fieldName, int? maxPoints = null)
    {
        if (!_signals.TryGetValue(fieldName, out var data))
            return [];
            
        if (!maxPoints.HasValue || maxPoints.Value >= data.Count)
            return data;
        
        var step = Math.Max(1, data.Count / maxPoints.Value);
        return data.Where((d, i) => i % step == 0).ToList();
    }

    public int GetRowCount() => _timestamps.Count;

    public void Reset(string dbPath)
    {
        _signals.Clear();
        _timestamps.Clear();
        _signalNames.Clear();
    }

    public void Dispose()
    {
        _signals.Clear();
        _timestamps.Clear();
    }
}
