using FluentAssertions;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SignalBench.Tests;

public class SerialStreamingTests
{
    [Fact]
    public void StreamingPacketScanner_Should_Extract_Packets_With_SyncWord()
    {
        var schema = new PacketSchema
        {
            Name = "Test",
            SyncWord = 0xAA55,
            Endianness = Endianness.Little
        };
        schema.Fields.Add(new FieldDefinition { Name = "val", Type = FieldType.Uint16 });

        var scanner = new StreamingPacketScanner(schema);
        
        // Packet: AA 55 (sync) 01 00 (val=1)
        byte[] data = [0xAA, 0x55, 0x01, 0x00, 0xAA, 0x55, 0x02, 0x00];
        
        var packets = scanner.PushData(data).Packets.ToList();
        
        packets.Count.Should().Be(2);
        packets[0].Fields["val"].Should().Be((uint)1);
        packets[1].Fields["val"].Should().Be((uint)2);
    }

    [Fact]
    public void StreamingPacketScanner_Should_Handle_Split_Packets()
    {
        var schema = new PacketSchema
        {
            Name = "Test",
            SyncWord = 0xAA55,
            Endianness = Endianness.Little
        };
        schema.Fields.Add(new FieldDefinition { Name = "val", Type = FieldType.Uint16 });

        var scanner = new StreamingPacketScanner(schema);
        
        // First chunk: sync word only
        var p1 = scanner.PushData([0xAA, 0x55]).Packets.ToList();
        p1.Should().BeEmpty();

        // Second chunk: data
        var p2 = scanner.PushData([0x05, 0x00]).Packets.ToList();
        p2.Count.Should().Be(1);
        p2[0].Fields["val"].Should().Be((uint)5);
    }

    [Fact]
    public void StreamingPacketScanner_Should_Handle_Noisy_Data()
    {
        var schema = new PacketSchema
        {
            Name = "Test",
            SyncWord = 0xAA55,
            Endianness = Endianness.Little
        };
        schema.Fields.Add(new FieldDefinition { Name = "val", Type = FieldType.Uint16 });

        var scanner = new StreamingPacketScanner(schema);
        
        // Noise + Packet + Noise + Partial Packet
        byte[] data = [0xFF, 0xEE, 0xAA, 0x55, 0x0A, 0x00, 0xCC, 0xAA];
        
        var p1 = scanner.PushData(data).Packets.ToList();
        p1.Count.Should().Be(1);
        p1[0].Fields["val"].Should().Be((uint)10);

        // Complete the partial packet
        var p2 = scanner.PushData([0x55, 0x0B, 0x00]).Packets.ToList();
        p2.Count.Should().Be(1);
        p2[0].Fields["val"].Should().Be((uint)11);
    }
}
