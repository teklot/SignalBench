using SignalBench.Core.Flux;
using SignalBench.Core.Flux.Protocols;
using SignalBench.Core.Generation;
using SignalBench.Core.Models.Schema;
using Xunit;

namespace SignalBench.Tests;

public class FluxTests
{
    [Fact]
    public void SchemaEncoder_EncodesByteAlignedFields()
    {
        // Arrange
        var schema = new PacketSchema
        {
            Name = "TestPacket",
            Endianness = Endianness.Little,
            Fields = new List<FieldDefinition>
            {
                new FieldDefinition { Name = "u8", Type = FieldType.Uint8, BitOffset = 0, BitLength = 8 },
                new FieldDefinition { Name = "u16", Type = FieldType.Uint16, BitOffset = 8, BitLength = 16 },
                new FieldDefinition { Name = "f32", Type = FieldType.Float32, BitOffset = 24, BitLength = 32 }
            }
        };

        var encoder = new SchemaEncoder(schema);
        var values = new Dictionary<string, double>
        {
            ["u8"] = 0xAA,
            ["u16"] = 0x1234,
            ["f32"] = 1.23f
        };

        // Act
        var result = encoder.Encode(values);

        // Assert
        Assert.Equal(7, result.Length);
        Assert.Equal(0xAA, result[0]);
        
        // u16 (0x1234) Little Endian: [0x34, 0x12]
        Assert.Equal(0x34, result[1]);
        Assert.Equal(0x12, result[2]);

        // f32 (1.23)
        var f32Bytes = BitConverter.GetBytes(1.23f);
        Assert.Equal(f32Bytes[0], result[3]);
        Assert.Equal(f32Bytes[1], result[4]);
        Assert.Equal(f32Bytes[2], result[5]);
        Assert.Equal(f32Bytes[3], result[6]);
    }

    [Fact]
    public void FluxChannel_EvaluatesSignals()
    {
        // Arrange
        var signal = new SineSignal(amplitude: 10, frequency: 1, offset: 5);
        var channel = new FluxChannel("test", "proto", "msg", "field", signal);

        // Act & Assert
        // Sine(0) = 0 -> 10 * 0 + 5 = 5
        Assert.Equal(5, channel.Evaluate(0), 5);
        
        // Sine(pi/2) = 1 -> 10 * 1 + 5 = 15
        // frequency is 1Hz, so pi/2 is at t = 0.25s
        Assert.Equal(15, channel.Evaluate(0.25), 5);
    }
}
