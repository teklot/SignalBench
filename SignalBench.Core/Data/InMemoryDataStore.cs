using SignalBench.SDK.Models;
using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Data;

public sealed class InMemoryDataStore : IDataStore
{
    private readonly Dictionary<string, List<double>> _signals = [];
    private readonly List<DateTime> _timestamps = [];
    private List<string> _signalNames = [];
    private readonly object _lock = new();

    public void InitializeSchema(PacketSchema schema)
    {
        lock (_lock)
        {
            _signals.Clear();
            _timestamps.Clear();
            _signalNames = schema.Fields.Select(f => f.Name).Where(n => !n.Equals("timestamp", StringComparison.OrdinalIgnoreCase)).ToList();
            
            foreach (var name in _signalNames)
            {
                _signals[name] = [];
            }
        }
    }

    public void InsertPackets(IEnumerable<DecodedPacket> packets)
    {
        lock (_lock)
        {
            int index = 0;
            foreach (var packet in packets)
            {
                DateTime ts;
                if (packet.Timestamp == default)
                {
                    ts = DateTime.Now.AddSeconds(index * 0.001);
                }
                else
                {
                    ts = packet.Timestamp;
                }
                _timestamps.Add(ts);
                index++;
                
                foreach (var name in _signalNames)
                {
                    if (packet.Fields.TryGetValue(name, out var val))
                    {
                        if (val is double d) _signals[name].Add(d);
                        else if (val is float f) _signals[name].Add(f);
                        else if (val is int i) _signals[name].Add(i);
                        else if (val is long l) _signals[name].Add(l);
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
    }

    public void InsertDerivedSignal(string name, List<double> data)
    {
        _signals[name] = data;
        if (!_signalNames.Contains(name))
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
        return [.. _timestamps.Where((t, i) => i % step == 0)];
    }

    public List<DateTime> GetTimestamps(int startIndex, int count)
    {
        int actualCount = Math.Min(count, _timestamps.Count - startIndex);
        if (actualCount <= 0) return [];
        return _timestamps.GetRange(startIndex, actualCount);
    }

    public List<DateTime> GetTimestamps(DateTime startTime)
    {
        lock (_lock)
        {
            return [.. _timestamps.Where(t => t >= startTime)];
        }
    }

    public List<double> GetSignalData(string fieldName, DateTime startTime)
    {
        lock (_lock)
        {
            if (!_signals.TryGetValue(fieldName, out var data)) return [];
            
            // Find the index of the first timestamp >= startTime
            int index = _timestamps.FindIndex(t => t >= startTime);
            if (index < 0) return [];
            
            return data.GetRange(index, data.Count - index);
        }
    }

    public DateTime GetTimestamp(int index)
    {
        if (index < 0 || index >= _timestamps.Count) return default;
        return _timestamps[index];
    }

    public List<double> GetSignalData(string fieldName, int? maxPoints = null)
    {
        if (!_signals.TryGetValue(fieldName, out var data))
            return [];
            
        if (!maxPoints.HasValue || maxPoints.Value >= data.Count)
            return data;
        
        var step = Math.Max(1, data.Count / maxPoints.Value);
        return [.. data.Where((d, i) => i % step == 0)];
    }

    public int GetRowCount() => _timestamps.Count;

    public void Reset(string dbPath)
    {
        _signals.Clear();
        _timestamps.Clear();
        _signalNames.Clear();
    }

    public void Clear()
    {
        _timestamps.Clear();
        foreach (var key in _signals.Keys)
        {
            _signals[key].Clear();
        }
    }

    public List<double> GetSignalData(string fieldName, int startIndex, int count)
    {
        if (!_signals.TryGetValue(fieldName, out var data))
            return [];

        int actualCount = Math.Min(count, data.Count - startIndex);
        if (actualCount <= 0) return [];

        return data.GetRange(startIndex, actualCount);
    }

    public void Dispose()
    {
        _signals.Clear();
        _timestamps.Clear();
    }
}
