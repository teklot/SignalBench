using FluentAssertions;
using SignalBench.Core.Data;
using SignalBench.Core.Models.Schema;
using SignalBench.SDK.Models;
using Xunit;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SignalBench.Tests;

public class DataStoreTests
{
    [Fact]
    public void SqliteDataStore_Should_Return_Correct_Timestamp_At_Index()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var store = new SqliteDataStore(dbPath);
        
        var schema = new PacketSchema { Name = "Test" };
        schema.Fields.Add(new FieldDefinition { Name = "val", Type = FieldType.Float32 });
        store.InitializeSchema(schema);

        var startTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var packets = new List<DecodedPacket>();
        for (int i = 0; i < 100; i++)
        {
            packets.Add(new DecodedPacket
            {
                SchemaName = "Test",
                Timestamp = startTime.AddSeconds(i),
                Fields = new Dictionary<string, object> { ["val"] = (float)i }
            });
        }
        store.InsertPackets(packets);

        store.GetRowCount().Should().Be(100);
        
        for (int i = 0; i < 100; i++)
        {
            var ts = store.GetTimestamp(i);
            ts.Should().Be(startTime.AddSeconds(i));
        }

        store.Dispose();
        if (File.Exists(dbPath)) File.Delete(dbPath);
    }

    [Fact]
    public void InMemoryDataStore_Should_Return_Correct_Timestamp_At_Index()
    {
        var store = new InMemoryDataStore();
        
        var schema = new PacketSchema { Name = "Test" };
        schema.Fields.Add(new FieldDefinition { Name = "val", Type = FieldType.Float32 });
        store.InitializeSchema(schema);

        var startTime = new DateTime(2025, 1, 1, 12, 0, 0);
        var packets = new List<DecodedPacket>();
        for (int i = 0; i < 100; i++)
        {
            packets.Add(new DecodedPacket
            {
                SchemaName = "Test",
                Timestamp = startTime.AddSeconds(i),
                Fields = new Dictionary<string, object> { ["val"] = (float)i }
            });
        }
        store.InsertPackets(packets);

        store.GetRowCount().Should().Be(100);
        
        for (int i = 0; i < 100; i++)
        {
            var ts = store.GetTimestamp(i);
            ts.Should().Be(startTime.AddSeconds(i));
        }
    }
}
