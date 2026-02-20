using System.Buffers.Binary;
using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Decoding;

public class BinaryDecoder
{
    public DecodedPacket Decode(ReadOnlySpan<byte> data, PacketSchema schema)
    {
        var packet = new DecodedPacket { SchemaName = schema.Name, Timestamp = default };

        foreach (var field in schema.Fields)
        {
            // Calculate byte offset from bit offset
            int byteOffset = field.BitOffset / 8;
            int bitOffsetInByte = field.BitOffset % 8;

            if (byteOffset >= data.Length) continue;

            object value = field.Type switch
            {
                FieldType.Uint8 => ExtractBits(data[byteOffset], bitOffsetInByte, field.BitLength),
                FieldType.Uint16 => ReadUInt16(data, byteOffset, bitOffsetInByte, field.BitLength, schema.Endianness),
                FieldType.Uint32 => ReadUInt32(data, byteOffset, bitOffsetInByte, field.BitLength, schema.Endianness),
                FieldType.Uint64 => schema.Endianness == Endianness.Little
                    ? BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(byteOffset, 8))
                    : BinaryPrimitives.ReadUInt64BigEndian(data.Slice(byteOffset, 8)),
                FieldType.Int8 => (sbyte)data[byteOffset],
                FieldType.Int16 => schema.Endianness == Endianness.Little
                    ? BinaryPrimitives.ReadInt16LittleEndian(data.Slice(byteOffset, 2))
                    : BinaryPrimitives.ReadInt16BigEndian(data.Slice(byteOffset, 2)),
                FieldType.Int32 => schema.Endianness == Endianness.Little
                    ? BinaryPrimitives.ReadInt32LittleEndian(data.Slice(byteOffset, 4))
                    : BinaryPrimitives.ReadInt32BigEndian(data.Slice(byteOffset, 4)),
                FieldType.Float32 => schema.Endianness == Endianness.Little
                    ? BinaryPrimitives.ReadSingleLittleEndian(data.Slice(byteOffset, 4))
                    : BinaryPrimitives.ReadSingleBigEndian(data.Slice(byteOffset, 4)),
                FieldType.Float64 => schema.Endianness == Endianness.Little
                    ? BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(byteOffset, 8))
                    : BinaryPrimitives.ReadDoubleBigEndian(data.Slice(byteOffset, 8)),
                _ => 0
            };

            packet.Fields[field.Name] = value;
        }

        return packet;
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

    private int GetTypeSize(FieldType type) => type switch
    {
        FieldType.Uint8 or FieldType.Int8 => 1,
        FieldType.Uint16 or FieldType.Int16 => 2,
        FieldType.Uint32 or FieldType.Int32 or FieldType.Float32 => 4,
        FieldType.Uint64 or FieldType.Float64 => 8,
        _ => 0
    };
}
