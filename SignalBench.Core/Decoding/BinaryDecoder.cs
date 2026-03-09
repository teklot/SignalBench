using System.Buffers.Binary;
using SignalBench.Core.Models.Schema;
using SignalBench.SDK.Models;

namespace SignalBench.Core.Decoding;

public sealed class BinaryDecoder
{
    public DecodedPacket Decode(ReadOnlySpan<byte> data, PacketSchema schema)
    {
        var fields = new Dictionary<string, object>();

        foreach (var field in schema.Fields)
        {
            int byteOffset = field.BitOffset / 8;
            int bitOffsetInByte = field.BitOffset % 8;

            if (byteOffset >= data.Length) continue;

            int neededBytes = GetTypeSize(field.Type, field.BitLength);
            if (byteOffset + neededBytes > data.Length) continue;

            object value = field.Type switch
            {
                FieldType.Uint8 => (object)ExtractBits(data[byteOffset], bitOffsetInByte, field.BitLength),
                FieldType.Uint16 => (object)ReadUInt16(data, byteOffset, bitOffsetInByte, field.BitLength, schema.Endianness),
                FieldType.Uint32 => (object)ReadUInt32(data, byteOffset, bitOffsetInByte, field.BitLength, schema.Endianness),
                FieldType.Uint64 => (object)(schema.Endianness == Endianness.Little
                    ? BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(byteOffset, 8))
                    : BinaryPrimitives.ReadUInt64BigEndian(data.Slice(byteOffset, 8))),
                FieldType.Int8 => (object)unchecked((sbyte)data[byteOffset]),
                FieldType.Int16 => (object)(schema.Endianness == Endianness.Little
                    ? BinaryPrimitives.ReadInt16LittleEndian(data.Slice(byteOffset, 2))
                    : BinaryPrimitives.ReadInt16BigEndian(data.Slice(byteOffset, 2))),
                FieldType.Int32 => (object)(schema.Endianness == Endianness.Little
                    ? BinaryPrimitives.ReadInt32LittleEndian(data.Slice(byteOffset, 4))
                    : BinaryPrimitives.ReadInt32BigEndian(data.Slice(byteOffset, 4))),
                FieldType.Int64 => (object)(schema.Endianness == Endianness.Little
                    ? BinaryPrimitives.ReadInt64LittleEndian(data.Slice(byteOffset, 8))
                    : BinaryPrimitives.ReadInt64BigEndian(data.Slice(byteOffset, 8))),
                FieldType.Float32 => (object)(schema.Endianness == Endianness.Little
                    ? BinaryPrimitives.ReadSingleLittleEndian(data.Slice(byteOffset, 4))
                    : BinaryPrimitives.ReadSingleBigEndian(data.Slice(byteOffset, 4))),
                FieldType.Float64 => (object)(schema.Endianness == Endianness.Little
                    ? BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(byteOffset, 8))
                    : BinaryPrimitives.ReadDoubleBigEndian(data.Slice(byteOffset, 8))),
                FieldType.Bool => (object)(data[byteOffset] != 0),
                _ => (object)0
            };

            fields[field.Name] = value;
        }

        return new DecodedPacket 
        { 
            SchemaName = schema.Name, 
            Timestamp = DateTime.Now, 
            Fields = fields 
        };
    }

    private uint ExtractBits(byte b, int offset, int length)
    {
        if (length == 0 || length > 8) length = 8;
        return (uint)(b >> offset) & (uint)((1 << length) - 1);
    }

    private uint ReadUInt16(ReadOnlySpan<byte> data, int byteOffset, int bitOffset, int bitLength, Endianness endian)
    {
        uint val = endian == Endianness.Little
            ? BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(byteOffset, 2))
            : BinaryPrimitives.ReadUInt16BigEndian(data.Slice(byteOffset, 2));

        if (bitLength > 0 && bitLength < 16)
            return (val >> bitOffset) & (uint)((1 << bitLength) - 1);
        return val;
    }

    private uint ReadUInt32(ReadOnlySpan<byte> data, int byteOffset, int bitOffset, int bitLength, Endianness endian)
    {
        uint val = endian == Endianness.Little
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(byteOffset, 4))
            : BinaryPrimitives.ReadUInt32BigEndian(data.Slice(byteOffset, 4));

        if (bitLength > 0 && bitLength < 32)
            return (val >> bitOffset) & (uint)((1u << bitLength) - 1);
        return val;
    }

    private int GetTypeSize(FieldType type, int bitLength) => type switch
    {
        FieldType.Uint8 or FieldType.Int8 or FieldType.Bool => 1,
        FieldType.Uint16 or FieldType.Int16 => 2,
        FieldType.Uint32 or FieldType.Int32 or FieldType.Float32 => 4,
        FieldType.Uint64 or FieldType.Int64 or FieldType.Float64 => 8,
        _ => 0
    };
}
