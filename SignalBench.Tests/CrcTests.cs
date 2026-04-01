using FluentAssertions;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;

namespace SignalBench.Tests;

public class CrcTests
{
    [Fact]
    public void Should_Validate_Correct_Crc8()
    {
        var yaml = @"
            packet:
              name: Crc8Packet
              fields:
                - name: data
                  type: uint8
              crc:
                type: Crc8
                polynomial: 0x07
                initial_value: 0x00
                final_xor: 0x00
                bit_offset: 8
                bit_length: 8
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        // Data: 0x01
        // CRC8 (poly=0x07, init=0x00) for 0x01 is 0x07
        byte[] data = [0x01, 0x07];

        var packet = decoder.Decode(data, schema);

        packet.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Invalidate_Incorrect_Crc8()
    {
        var yaml = @"
            packet:
              name: Crc8Packet
              fields:
                - name: data
                  type: uint8
              crc:
                type: Crc8
                polynomial: 0x07
                initial_value: 0x00
                final_xor: 0x00
                bit_offset: 8
                bit_length: 8
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        byte[] data = [0x01, 0x08]; // 0x08 is wrong

        var packet = decoder.Decode(data, schema);

        packet.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Validate_Reflected_Crc16_ARC()
    {
        // CRC-16/ARC: poly=0x8005, init=0x0000, refin=true, refout=true, xorout=0x0000
        var yaml = @"
            packet:
              name: Crc16Arc
              fields:
                - name: data
                  type: uint64 # To fit 9 bytes we need more than uint64, but let's use a byte array approach
                - name: data2
                  type: uint8
              crc:
                type: Crc16
                polynomial: 0x8005
                initial_value: 0x0000
                final_xor: 0x0000
                reflect_input: true
                reflect_output: true
                bit_offset: 72
                bit_length: 16
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        // Data: ""123456789""
        byte[] data = [0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3D, 0xBB]; // Little endian CRC 0xBB3D

        var packet = decoder.Decode(data, schema);

        packet.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Validate_Crc_In_Middle()
    {
        var yaml = @"
            packet:
              name: MiddleCrc
              fields:
                - name: before
                  type: uint8
                - name: after
                  type: uint8
                  bit_offset: 24 # After 1 byte before + 2 bytes CRC
              crc:
                type: Crc16
                polynomial: 0x1021
                initial_value: 0x0000
                bit_offset: 8
                bit_length: 16
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        // Data: [Before=0x01, CRC=0x????, After=0x02]
        // CRC-16/XMODEM for [0x01, 0x02] is 0x1373
        byte[] data = [0x01, 0x73, 0x13, 0x02]; // Little endian CRC 0x1373

        var packet = decoder.Decode(data, schema);

        packet.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Calculate_XMODEM()
    {
        byte[] data = [0x01, 0x02];
        var crc = CrcCalculator.CalculateCrc(data, CrcType.Crc16, 0x1021, 0x0000, 0x0000, false, false);
        crc.Should().Be(0x1373);
    }

    [Fact]
    public void Should_Mark_Invalid_Indices_In_DataStore()
    {
        var store = new SignalBench.Core.Data.InMemoryDataStore();
        var schema = new PacketSchema { Name = "Test" };
        schema.Fields.Add(new FieldDefinition { Name = "Value", Type = FieldType.Uint8 });
        store.InitializeSchema(schema);

        var packets = new List<SignalBench.SDK.Models.DecodedPacket>
        {
            new() { SchemaName = "Test", Timestamp = DateTime.Now, IsValid = true, Fields = new() { ["Value"] = 1.0 } },
            new() { SchemaName = "Test", Timestamp = DateTime.Now, IsValid = false, Fields = new() { ["Value"] = 2.0 } },
            new() { SchemaName = "Test", Timestamp = DateTime.Now, IsValid = true, Fields = new() { ["Value"] = 3.0 } }
        };

        store.InsertPackets(packets);

        var invalidIndices = store.GetInvalidIndices();
        invalidIndices.Should().ContainSingle().Which.Should().Be(1);
    }
}
