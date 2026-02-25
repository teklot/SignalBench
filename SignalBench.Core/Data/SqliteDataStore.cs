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
        
        var packetList = packets.ToList();
        if (packetList.Count == 0) return;

        // Get all unique field names from all packets
        var allFieldNames = packetList
            .SelectMany(p => p.Fields.Keys)
            .Distinct()
            .Where(k => !k.Equals("timestamp", StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k)
            .ToList();

        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;

        // Prepare INSERT statement template
        var fieldNames = string.Join(", ", allFieldNames.Select(k => $"\"{k}\""));
        var fieldParams = string.Join(", ", allFieldNames.Select(k => $"@{k.Replace(" ", "_")}"));
        
        var sql = $"INSERT INTO {_tableName} (timestamp";
        if (!string.IsNullOrEmpty(fieldNames))
            sql += $", {fieldNames}";
        sql += ") VALUES (@ts";
        if (!string.IsNullOrEmpty(fieldParams))
            sql += $", {fieldParams}";
        sql += ")";

        foreach (var packet in packetList)
        {
            command.CommandText = sql;
            command.Parameters.Clear();

            DateTime finalTs = packet.Timestamp;
            if (finalTs == default && packet.Fields.TryGetValue("timestamp", out var val))
            {
                finalTs = ParseTimestamp(val);
            }
            if (finalTs == default) finalTs = DateTime.Now;

            command.Parameters.AddWithValue("@ts", finalTs);

            foreach (var fieldName in allFieldNames)
            {
                if (packet.Fields.TryGetValue(fieldName, out var fieldVal))
                {
                    command.Parameters.AddWithValue($"@{fieldName.Replace(" ", "_")}", fieldVal ?? DBNull.Value);
                }
                else
                {
                    command.Parameters.AddWithValue($"@{fieldName.Replace(" ", "_")}", DBNull.Value);
                }
            }
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static DateTime ParseTimestamp(object? val)
    {
        if (val == null) return default;
        
        if (val is DateTime dt) return dt;
        if (val is double d) return DateTime.UnixEpoch.AddSeconds(d);
        if (val is float f) return DateTime.UnixEpoch.AddSeconds(f);
        if (val is long l) return DateTime.UnixEpoch.AddSeconds(l);
        if (val is ulong ul) return DateTime.UnixEpoch.AddSeconds(ul);
        if (val is int i) return DateTime.UnixEpoch.AddSeconds(i);
        if (val is uint ui) return DateTime.UnixEpoch.AddSeconds(ui);
        if (val is string s && DateTime.TryParse(s, out var dt2)) return dt2;
        
        return default;
    }

    public List<DateTime> GetTimestamps(int? maxPoints = null)
    {
        if (_connection == null) return [];
        var data = new List<DateTime>();
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT timestamp FROM {_tableName} ORDER BY id";
        
        if (maxPoints.HasValue)
        {
            command.CommandText = $"SELECT timestamp FROM {_tableName} WHERE id % @step = 0 ORDER BY id";
            var totalRows = GetTotalRowCount();
            var step = Math.Max(1, totalRows / maxPoints.Value);
            command.Parameters.AddWithValue("@step", step);
        }
        
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            data.Add(reader.GetDateTime(0));
        }
        return data;
    }

    public int GetRowCount()
    {
        if (_connection == null) return 0;
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {_tableName}";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private int GetTotalRowCount()
    {
        if (_connection == null) return 0;
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {_tableName}";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public List<double> GetSignalData(string fieldName, int? maxPoints = null)
    {
        if (_connection == null) return [];
        var data = new List<double>();
        using var command = _connection.CreateCommand();
        
        if (maxPoints.HasValue)
        {
            var totalRows = GetTotalRowCount();
            var step = Math.Max(1, totalRows / maxPoints.Value);
            command.CommandText = $"SELECT \"{fieldName}\" FROM {_tableName} WHERE id % @step = 0 ORDER BY id";
            command.Parameters.AddWithValue("@step", step);
        }
        else
        {
            command.CommandText = $"SELECT \"{fieldName}\" FROM {_tableName} ORDER BY id";
        }
        
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

    public void DeleteSignal(string name)
    {
        if (_connection == null) return;
        
        using var command = _connection.CreateCommand();
        var safeName = $"\"{name.Replace("\"", "\"\"")}\"";
        
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{_tableName}') WHERE name = '{name}'";
        var exists = Convert.ToInt64(command.ExecuteScalar()) > 0;
        
        if (!exists) return;

        var tempTable = $"{_tableName}_temp";
        
        command.CommandText = $@"
            PRAGMA table_info({_tableName});
        ";
        var columns = new List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var colName = reader.GetString(1);
                if (colName != name)
                {
                    columns.Add(colName);
                }
            }
        }

        if (columns.Count == 0) return;

        var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
        
        command.CommandText = $@"
            CREATE TEMP TABLE {tempTable} AS SELECT {columnList} FROM {_tableName};
            DROP TABLE {_tableName};
            ALTER TABLE {tempTable} RENAME TO {_tableName};
        ";
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
