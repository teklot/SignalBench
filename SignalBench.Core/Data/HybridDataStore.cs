using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Data;

public enum StorageMode
{
    InMemory,
    Sqlite
}

public class HybridDataStore : IDataStore
{
    private readonly StorageMode _storageMode;
    private readonly InMemoryDataStore _inMemory;
    private SqliteDataStore? _sqlite;

    public HybridDataStore(StorageMode mode = StorageMode.InMemory)
    {
        _storageMode = mode;
        _inMemory = new InMemoryDataStore();
    }

    public void InitializeSchema(Models.Schema.PacketSchema schema)
    {
        if (_storageMode == StorageMode.Sqlite)
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
        if (_storageMode == StorageMode.Sqlite)
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
        if (_storageMode == StorageMode.Sqlite)
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
        if (_storageMode == StorageMode.Sqlite)
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
        return _storageMode == StorageMode.Sqlite 
            ? _sqlite?.GetTimestamps(maxPoints) ?? []
            : _inMemory.GetTimestamps(maxPoints);
    }

    public List<DateTime> GetTimestamps(int startIndex, int count)
    {
        return _storageMode == StorageMode.Sqlite
            ? _sqlite?.GetTimestamps(startIndex, count) ?? []
            : _inMemory.GetTimestamps(startIndex, count);
    }

    public DateTime GetTimestamp(int index)
    {
        return _storageMode == StorageMode.Sqlite
            ? _sqlite?.GetTimestamp(index) ?? default
            : _inMemory.GetTimestamp(index);
    }

    public List<double> GetSignalData(string fieldName, int? maxPoints = null)
    {
        return _storageMode == StorageMode.Sqlite 
            ? _sqlite?.GetSignalData(fieldName, maxPoints) ?? []
            : _inMemory.GetSignalData(fieldName, maxPoints);
    }

    public int GetRowCount()
    {
        return _storageMode == StorageMode.Sqlite 
            ? _sqlite?.GetRowCount() ?? 0
            : _inMemory.GetRowCount();
    }

    public void Reset(string dbPath)
    {
        if (_storageMode == StorageMode.Sqlite)
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
        if (_storageMode == StorageMode.Sqlite)
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
        return _storageMode == StorageMode.Sqlite
            ? _sqlite?.GetSignalData(fieldName, startIndex, count) ?? []
            : _inMemory.GetSignalData(fieldName, startIndex, count);
    }

    public void Dispose()
    {
        _inMemory.Dispose();
        _sqlite?.Dispose();
    }
}
