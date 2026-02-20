using FluentAssertions;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;

namespace SignalBench.Tests;

public class DecodingTests
{
    [Fact]
    public void Should_Decode_Binary_Packet_Using_Schema()
    {
        // Arrange
        var yaml = @"
packet:
  name: EPS_Telemetry
  sync_word: 0xAA55
  endianness: little
  fields:
    - name: timestamp
      type: uint32
    - name: battery_voltage
      type: float32
    - name: temperature_1
      type: int16
";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        // 10 bytes: 4 (uint32) + 4 (float32) + 2 (int16)
        byte[] data = [
            0x01, 0x00, 0x00, 0x00, // 1
            0x00, 0x00, 0x80, 0x3F, // 1.0f
            0x05, 0x00              // 5
        ];

        // Act
        var packet = decoder.Decode(data, schema);

        // Assert
        packet.Fields["timestamp"].Should().Be((uint)1);
        packet.Fields["battery_voltage"].Should().Be(1.0f);
        packet.Fields["temperature_1"].Should().Be((short)5);
    }
}
