using Microsoft.Data.Sqlite;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Data;

public class SqliteDataStore : IDataStore
{
    private SqliteConnection? _connection;
    private readonly string _tableName = "telemetry";

    public SqliteDataStore()
    {
    }

    public SqliteDataStore(string dbPath)
    {
        Reset(dbPath);
    }

    public void Reset(string dbPath)
    {
        Dispose();

        // Retry deletion a few times in case the file is briefly locked by the OS
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
                break;
            }
            catch (IOException)
            {
                if (i == 4) throw;
                Thread.Sleep(100);
            }
        }

        _connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        _connection.Open();
    }

    public void InitializeSchema(PacketSchema schema)
    {
        if (_connection == null) throw new InvalidOperationException("DataStore not initialized.");
        using var command = _connection.CreateCommand();
        // Filter out 'timestamp' from dynamic columns if it's already explicitly defined
        var fieldCols = schema.Fields
            .Where(f => !f.Name.Equals("timestamp", StringComparison.OrdinalIgnoreCase))
            .Select(f => $"\"{f.Name}\" REAL");

        var columns = string.Join(", ", fieldCols);
        var sql = $"CREATE TABLE IF NOT EXISTS {_tableName} (id INTEGER PRIMARY KEY, timestamp DATETIME";
        if (!string.IsNullOrEmpty(columns))
            sql += ", " + columns;
        sql += ")";

        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void InsertPackets(IEnumerable<DecodedPacket> packets)
    {
        if (_connection == null) throw new InvalidOperationException("DataStore not initialized.");
        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;

        foreach (var packet in packets)
        {
            // Filter out 'timestamp' from the fields dictionary for the dynamic part of the query
            var otherFields = packet.Fields
                .Where(kv => !kv.Key.Equals("timestamp", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var fieldNames = string.Join(", ", otherFields.Keys.Select(k => $"\"{k}\""));
            var fieldParameters = string.Join(", ", otherFields.Keys.Select(k => $"@{k.Replace(" ", "_")}"));

            var sql = $"INSERT INTO {_tableName} (timestamp";
            if (!string.IsNullOrEmpty(fieldNames))
                sql += $", {fieldNames}";
            sql += $") VALUES (@ts";
            if (!string.IsNullOrEmpty(fieldParameters))
                sql += $", {fieldParameters}";
            sql += ")";

            command.CommandText = sql;
            command.Parameters.Clear();

            DateTime finalTs = packet.Timestamp;
            // If timestamp is default (0001-01-01) and there's a 'timestamp' field, try to parse it
            if (finalTs == default && packet.Fields.TryGetValue("timestamp", out var val))
            {
                if (val is DateTime dt)
                    finalTs = dt;
                else if (val is double d)
                    finalTs = DateTime.UnixEpoch.AddSeconds(d);
                else if (val is float f)
                    finalTs = DateTime.UnixEpoch.AddSeconds(f);
                else if (val is long l)
                    finalTs = DateTime.UnixEpoch.AddSeconds(l);
                else if (val is ulong ul)
                    finalTs = DateTime.UnixEpoch.AddSeconds(ul);
                else if (val is int i)
                    finalTs = DateTime.UnixEpoch.AddSeconds(i);
                else if (val is uint ui)
                    finalTs = DateTime.UnixEpoch.AddSeconds(ui);
                else if (val is string s && DateTime.TryParse(s, out var dt2))
                    finalTs = dt2;
            }

            if (finalTs == default) finalTs = DateTime.Now;

            command.Parameters.AddWithValue("@ts", finalTs);

            foreach (var kv in otherFields)
            {
                command.Parameters.AddWithValue($"@{kv.Key.Replace(" ", "_")}", kv.Value ?? DBNull.Value);
            }
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public List<DateTime> GetTimestamps()
    {
        if (_connection == null) return [];
        var data = new List<DateTime>();
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT timestamp FROM {_tableName} ORDER BY id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            data.Add(reader.GetDateTime(0));
        }
        return data;
    }

    public List<double> GetSignalData(string fieldName)
    {
        if (_connection == null) return [];
        var data = new List<double>();
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT \"{fieldName}\" FROM {_tableName} ORDER BY id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            data.Add(reader.IsDBNull(0) ? 0 : reader.GetDouble(0));
        }
        return data;
    }

    public void InsertDerivedSignal(string name, List<double> data)
    {
        if (_connection == null) throw new InvalidOperationException("DataStore not initialized.");
        
        using var command = _connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {_tableName} ADD COLUMN \"{name}\" REAL";
        try { command.ExecuteNonQuery(); } catch { }

        using var transaction = _connection.BeginTransaction();
        using var updateCmd = _connection.CreateCommand();
        updateCmd.Transaction = transaction;
        updateCmd.CommandText = $"SELECT id FROM {_tableName} ORDER BY id";
        
        var ids = new List<long>();
        using (var reader = updateCmd.ExecuteReader())
        {
            while (reader.Read()) ids.Add(reader.GetInt64(0));
        }

        updateCmd.Parameters.Clear();
        updateCmd.CommandText = $"UPDATE {_tableName} SET \"{name}\" = @val WHERE id = @id";
        var valParam = updateCmd.Parameters.Add("@val", Microsoft.Data.Sqlite.SqliteType.Real);
        var idParam = updateCmd.Parameters.Add("@id", Microsoft.Data.Sqlite.SqliteType.Integer);

        for (int i = 0; i < data.Count && i < ids.Count; i++)
        {
            valParam.Value = double.IsNaN(data[i]) ? DBNull.Value : data[i];
            idParam.Value = ids[i];
            updateCmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
