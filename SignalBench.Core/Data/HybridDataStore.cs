using SignalBench.SDK.Models;
using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Data;

public enum StorageMode
{
    InMemory,
    Sqlite
}

public sealed class HybridDataStore(StorageMode mode = StorageMode.InMemory) : IDataStore
{
    private readonly InMemoryDataStore _inMemory = new();
    private SqliteDataStore? _sqlite;

    public void InitializeSchema(PacketSchema schema)
    {
        if (mode == StorageMode.Sqlite)
        {
            _sqlite ??= new SqliteDataStore();
            _sqlite.InitializeSchema(schema);
        }
        else
        {
            _inMemory.InitializeSchema(schema);
        }
    }

    public void InsertPackets(IEnumerable<DecodedPacket> packets)
    {
        if (mode == StorageMode.Sqlite)
        {
            _sqlite?.InsertPackets(packets);
        }
        else
        {
            _inMemory.InsertPackets(packets);
        }
    }

    public void InsertDerivedSignal(string name, List<double> data)
    {
        if (mode == StorageMode.Sqlite)
        {
            _sqlite?.InsertDerivedSignal(name, data);
        }
        else
        {
            _inMemory.InsertDerivedSignal(name, data);
        }
    }

    public void DeleteSignal(string name)
    {
        if (mode == StorageMode.Sqlite)
        {
            _sqlite?.DeleteSignal(name);
        }
        else
        {
            _inMemory.DeleteSignal(name);
        }
    }

    public List<DateTime> GetTimestamps(int? maxPoints = null)
    {
        return mode == StorageMode.Sqlite 
            ? _sqlite?.GetTimestamps(maxPoints) ?? []
            : _inMemory.GetTimestamps(maxPoints);
    }

    public List<DateTime> GetTimestamps(int startIndex, int count)
    {
        return mode == StorageMode.Sqlite
            ? _sqlite?.GetTimestamps(startIndex, count) ?? []
            : _inMemory.GetTimestamps(startIndex, count);
    }

    public List<DateTime> GetTimestamps(DateTime startTime)
    {
        return mode == StorageMode.Sqlite
            ? _sqlite?.GetTimestamps(startTime) ?? []
            : _inMemory.GetTimestamps(startTime);
    }

    public List<double> GetSignalData(string fieldName, DateTime startTime)
    {
        return mode == StorageMode.Sqlite
            ? _sqlite?.GetSignalData(fieldName, startTime) ?? []
            : _inMemory.GetSignalData(fieldName, startTime);
    }

    public DateTime GetTimestamp(int index)
    {
        return mode == StorageMode.Sqlite
            ? _sqlite?.GetTimestamp(index) ?? default
            : _inMemory.GetTimestamp(index);
    }

    public List<double> GetSignalData(string fieldName, int? maxPoints = null)
    {
        return mode == StorageMode.Sqlite 
            ? _sqlite?.GetSignalData(fieldName, maxPoints) ?? []
            : _inMemory.GetSignalData(fieldName, maxPoints);
    }

    public (int start, int end) GetIndices(DateTime startTime, DateTime endTime)
    {
        return mode == StorageMode.Sqlite
            ? _sqlite?.GetIndices(startTime, endTime) ?? (-1, -1)
            : _inMemory.GetIndices(startTime, endTime);
    }

    public int GetRowCount()
    {
        return mode == StorageMode.Sqlite 
            ? _sqlite?.GetRowCount() ?? 0
            : _inMemory.GetRowCount();
    }

    public void Reset(string dbPath)
    {
        if (mode == StorageMode.Sqlite)
        {
            _sqlite?.Reset(dbPath);
        }
        else
        {
            _inMemory.Reset(dbPath);
        }
    }

    public void Clear()
    {
        if (mode == StorageMode.Sqlite)
        {
            _sqlite?.Clear();
        }
        else
        {
            _inMemory.Clear();
        }
    }

    public List<double> GetSignalData(string fieldName, int startIndex, int count)
    {
        return mode == StorageMode.Sqlite
            ? _sqlite?.GetSignalData(fieldName, startIndex, count) ?? []
            : _inMemory.GetSignalData(fieldName, startIndex, count);
    }

    public void Dispose()
    {
        _inMemory.Dispose();
        _sqlite?.Dispose();
    }
}
