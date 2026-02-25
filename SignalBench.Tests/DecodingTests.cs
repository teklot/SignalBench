using FluentAssertions;
using SignalBench.Core.Decoding;
using SignalBench.Core.DerivedSignals;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;

namespace SignalBench.Tests;

public class DecodingTests
{
    [Fact]
    public void Should_Decode_Binary_Packet_Using_Schema()
    {
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

        byte[] data = [
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x80, 0x3F,
            0x05, 0x00
        ];

        var packet = decoder.Decode(data, schema);

        packet.Fields["timestamp"].Should().Be((uint)1);
        packet.Fields["battery_voltage"].Should().Be(1.0f);
        packet.Fields["temperature_1"].Should().Be((short)5);
    }

    [Fact]
    public void Should_Decode_Big_Endian_Packet()
    {
        var yaml = @"
            packet:
              name: BigEndianPacket
              endianness: big
              fields:
                - name: value16
                  type: uint16
                - name: value32
                  type: uint32
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06];

        var packet = decoder.Decode(data, schema);

        packet.Fields["value16"].Should().Be((ushort)0x0102);
        packet.Fields["value32"].Should().Be((uint)0x03040506);
    }

    [Fact]
    public void Should_Decode_UInt64_Only()
    {
        var yaml = @"
            packet:
              name: UInt64Only
              endianness: little
              fields:
                - name: value
                  type: uint64
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];

        var packet = decoder.Decode(data, schema);

        ((ulong)packet.Fields["value"]).Should().Be(0x0807060504030201);
    }

    [Fact]
    public void Should_Decode_UInt64_With_Next_Field()
    {
        var yaml = @"
            packet:
              name: UInt64WithNext
              endianness: little
              fields:
                - name: value64
                  type: uint64
                - name: value8
                  type: uint8
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0xFF];

        var packet = decoder.Decode(data, schema);

        ((ulong)packet.Fields["value64"]).Should().Be(0x0807060504030201);
        ((uint)packet.Fields["value8"]).Should().Be(0xFF);
    }

    [Fact]
    public void Should_Decode_Field_Before_UInt64()
    {
        var yaml = @"
            packet:
              name: FieldBeforeUInt64
              endianness: little
              fields:
                - name: value8
                  type: uint8
                - name: value64
                  type: uint64
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        byte[] data = [0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];

        var packet = decoder.Decode(data, schema);

        ((uint)packet.Fields["value8"]).Should().Be(0xFF);
        ((ulong)packet.Fields["value64"]).Should().Be(0x0807060504030201);
    }

    [Fact]
    public void Should_Decode_Three_Fields_Then_UInt64()
    {
        var yaml = @"
            packet:
              name: ThreeThenUInt64
              endianness: little
              fields:
                - name: value8
                  type: uint8
                - name: value16
                  type: uint16
                - name: value32
                  type: uint32
                - name: value64
                  type: uint64
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        byte[] data = [0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10];

        var packet = decoder.Decode(data, schema);

        ((uint)packet.Fields["value8"]).Should().Be(0xFF);
        ((uint)packet.Fields["value16"]).Should().Be(0x0201);
        ((uint)packet.Fields["value32"]).Should().Be(0x06050403);
        ((ulong)packet.Fields["value64"]).Should().Be(0x0E0D0C0B0A090807);
    }

    [Fact]
    public void Should_Decode_Float_Types()
    {
        var yaml = @"
            packet:
              name: Floats
              endianness: little
              fields:
                - name: f32
                  type: float32
                - name: f64
                  type: float64
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        byte[] data = new byte[12];
        BitConverter.GetBytes(3.14f).CopyTo(data, 0);
        BitConverter.GetBytes(2.718281828).CopyTo(data, 4);

        var packet = decoder.Decode(data, schema);

        ((float)packet.Fields["f32"]).Should().BeApproximately(3.14f, 0.001f);
        ((double)packet.Fields["f64"]).Should().BeApproximately(2.718281828, 0.000001);
    }

    [Fact]
    public void Should_Extract_Bitfields()
    {
        var yaml = @"
            packet:
              name: Bitfields
              endianness: little
              fields:
                - name: low_nibble
                  type: uint8
                  bit_offset: 0
                  bit_length: 4
                - name: high_nibble
                  type: uint8
                  bit_offset: 4
                  bit_length: 4
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);
        var decoder = new BinaryDecoder();

        byte[] data = [0xAB];

        var packet = decoder.Decode(data, schema);

        packet.Fields["low_nibble"].Should().Be((uint)0xB);
        packet.Fields["high_nibble"].Should().Be((uint)0xA);
    }

    [Fact]
    public void Should_Handle_Schema_Without_Sync_Word()
    {
        var yaml = @"
            packet:
              name: NoSync
              endianness: little
              fields:
                - name: value
                  type: uint32
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(yaml);

        schema.SyncWord.Should().BeNull();
        schema.Endianness.Should().Be(Endianness.Little);
    }

    [Fact]
    public void Should_Load_And_Save_Schema()
    {
        var originalYaml = @"
            packet:
              name: TestSchema
              endianness: big
              fields:
                - name: field1
                  type: uint16
                - name: field2
                  type: float32
            ";
        var loader = new SchemaLoader();
        var schema = loader.Load(originalYaml);

        schema.Name.Should().Be("TestSchema");
        schema.Endianness.Should().Be(Endianness.Big);
        schema.Fields.Count.Should().Be(2);
        schema.Fields[0].Name.Should().Be("field1");
        schema.Fields[0].Type.Should().Be(FieldType.Uint16);
    }
}

public class FormulaEngineTests
{
    [Fact]
    public void Should_Evaluate_Simple_Formula()
    {
        var engine = new FormulaEngine();
        var parameters = new Dictionary<string, object>
        {
            ["a"] = 10.0,
            ["b"] = 5.0
        };

        var result = engine.Evaluate("a + b", parameters);

        result.Should().Be(15.0);
    }

    [Fact]
    public void Should_Evaluate_Multiplication()
    {
        var engine = new FormulaEngine();
        var parameters = new Dictionary<string, object>
        {
            ["voltage"] = 12.0,
            ["current"] = 2.5
        };

        var result = engine.Evaluate("voltage * current", parameters);

        result.Should().Be(30.0);
    }

    [Fact]
    public void Should_Evaluate_Complex_Formula()
    {
        var engine = new FormulaEngine();
        var parameters = new Dictionary<string, object>
        {
            ["a"] = 10.0,
            ["b"] = 5.0,
            ["c"] = 2.0
        };

        var result = engine.Evaluate("(a + b) * c", parameters);

        result.Should().Be(30.0);
    }

    [Fact]
    public void Should_Evaluate_Division()
    {
        var engine = new FormulaEngine();
        var parameters = new Dictionary<string, object>
        {
            ["x"] = 100.0,
            ["y"] = 4.0
        };

        var result = engine.Evaluate("x / y", parameters);

        result.Should().Be(25.0);
    }

    [Fact]
    public void Should_Evaluate_Sqrt_Function()
    {
        var engine = new FormulaEngine();
        var parameters = new Dictionary<string, object>
        {
            ["battery_voltage"] = 16.0
        };

        var result = engine.Evaluate("sqrt(battery_voltage)", parameters);

        result.Should().Be(4.0);
    }

    [Fact]
    public void Should_Evaluate_Sqrt_Function_Case_Insensitive()
    {
        var engine = new FormulaEngine();
        var parameters = new Dictionary<string, object>
        {
            ["battery_voltage"] = 16.0
        };

        var result1 = engine.Evaluate("sqrt(battery_voltage)", parameters);
        var result2 = engine.Evaluate("Sqrt(battery_voltage)", parameters);
        var result3 = engine.Evaluate("SQRT(battery_voltage)", parameters);

        result1.Should().Be(4.0);
        result2.Should().Be(4.0);
        result3.Should().Be(4.0);
    }

    [Fact]
    public void Should_Evaluate_Other_Math_Functions_Case_Insensitive()
    {
        var engine = new FormulaEngine();
        var parameters = new Dictionary<string, object>
        {
            ["x"] = -5.0,
            ["y"] = 2.0
        };

        var abs1 = engine.Evaluate("abs(x)", parameters);
        var abs2 = engine.Evaluate("Abs(x)", parameters);
        var pow1 = engine.Evaluate("pow(y, 3)", parameters);
        var pow2 = engine.Evaluate("Pow(y, 3)", parameters);
        var ln1 = engine.Evaluate("ln(2.718281828)", parameters);
        var ln2 = engine.Evaluate("Ln(2.718281828)", parameters);

        abs1.Should().Be(5.0);
        abs2.Should().Be(5.0);
        pow1.Should().Be(8.0);
        pow2.Should().Be(8.0);
        ln1.Should().BeApproximately(1.0, 0.001);
        ln2.Should().BeApproximately(1.0, 0.001);
    }
}
