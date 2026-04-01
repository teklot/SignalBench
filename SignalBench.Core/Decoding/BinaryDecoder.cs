using System.Buffers.Binary;
using SignalBench.Core.Models.Schema;
using SignalBench.SDK.Models;

namespace SignalBench.Core.Decoding;

public sealed class BinaryDecoder
{
    public DecodedPacket Decode(ReadOnlySpan<byte> data, PacketSchema schema)
    {
        var fields = new Dictionary<string, object>();
        DecodeFields(data, schema.Fields, schema.Endianness, fields, "");

        bool isValid = true;
        if (schema.Crc != null)
        {
            isValid = ValidateCrc(data, schema.Crc, schema.Endianness);
        }

        return new DecodedPacket 
        { 
            SchemaName = schema.Name, 
            Timestamp = DateTime.Now, 
            Fields = fields,
            IsValid = isValid
        };
    }

    private bool ValidateCrc(ReadOnlySpan<byte> data, CrcDefinition crcDef, Endianness endian)
    {
        int crcByteOffset = crcDef.BitOffset / 8;
        int crcByteLength = (crcDef.BitLength + 7) / 8;

        if (crcByteOffset + crcByteLength > data.Length) return false;

        // Extract received CRC
        uint receivedCrc = 0;
        var crcData = data.Slice(crcByteOffset, crcByteLength);
        if (crcDef.Type == CrcType.Crc8)
        {
            receivedCrc = crcData[0];
        }
        else if (crcDef.Type == CrcType.Crc16)
        {
            receivedCrc = endian == Endianness.Little
                ? BinaryPrimitives.ReadUInt16LittleEndian(crcData)
                : BinaryPrimitives.ReadUInt16BigEndian(crcData);
        }
        else if (crcDef.Type == CrcType.Crc32)
        {
            receivedCrc = endian == Endianness.Little
                ? BinaryPrimitives.ReadUInt32LittleEndian(crcData)
                : BinaryPrimitives.ReadUInt32BigEndian(crcData);
        }

        // Calculate CRC on data excluding the CRC field itself
        byte[] dataToVerify;
        if (crcByteOffset == 0)
        {
            dataToVerify = data.Slice(crcByteLength).ToArray();
        }
        else if (crcByteOffset + crcByteLength >= data.Length)
        {
            dataToVerify = data.Slice(0, crcByteOffset).ToArray();
        }
        else
        {
            dataToVerify = new byte[data.Length - crcByteLength];
            data.Slice(0, crcByteOffset).CopyTo(dataToVerify);
            data.Slice(crcByteOffset + crcByteLength).CopyTo(dataToVerify.AsSpan(crcByteOffset));
        }
        
        uint calculatedCrc = CrcCalculator.CalculateCrc(
            dataToVerify, 
            crcDef.Type, 
            crcDef.Polynomial, 
            crcDef.InitialValue, 
            crcDef.FinalXor, 
            crcDef.ReflectInput, 
            crcDef.ReflectOutput);

        return receivedCrc == calculatedCrc;
    }

    private void DecodeFields(ReadOnlySpan<byte> data, IEnumerable<FieldDefinition> fieldDefs, Endianness endian, Dictionary<string, object> results, string prefix)
    {
        foreach (var field in fieldDefs)
        {
            string fullName = string.IsNullOrEmpty(prefix) ? field.Name : $"{prefix}/{field.Name}";

            if (field.Fields != null && field.Fields.Count > 0)
            {
                // Recursive call for nested fields
                DecodeFields(data, field.Fields, endian, results, fullName);
                continue;
            }

            int byteOffset = field.BitOffset / 8;
            int bitOffsetInByte = field.BitOffset % 8;

            if (byteOffset >= data.Length) continue;

            int neededBytes = GetTypeSize(field.Type, field.BitLength);
            if (byteOffset + neededBytes > data.Length) continue;

            double rawValue = field.Type switch
            {
                FieldType.Uint8 => ExtractBits(data[byteOffset], bitOffsetInByte, field.BitLength),
                FieldType.Uint16 => ReadUInt16(data, byteOffset, bitOffsetInByte, field.BitLength, endian),
                FieldType.Uint32 => ReadUInt32(data, byteOffset, bitOffsetInByte, field.BitLength, endian),
                FieldType.Uint64 => (double)(endian == Endianness.Little
                    ? BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(byteOffset, 8))
                    : BinaryPrimitives.ReadUInt64BigEndian(data.Slice(byteOffset, 8))),
                FieldType.Int8 => unchecked((sbyte)data[byteOffset]),
                FieldType.Int16 => (endian == Endianness.Little
                    ? BinaryPrimitives.ReadInt16LittleEndian(data.Slice(byteOffset, 2))
                    : BinaryPrimitives.ReadInt16BigEndian(data.Slice(byteOffset, 2))),
                FieldType.Int32 => (endian == Endianness.Little
                    ? BinaryPrimitives.ReadInt32LittleEndian(data.Slice(byteOffset, 4))
                    : BinaryPrimitives.ReadInt32BigEndian(data.Slice(byteOffset, 4))),
                FieldType.Int64 => (double)(endian == Endianness.Little
                    ? BinaryPrimitives.ReadInt64LittleEndian(data.Slice(byteOffset, 8))
                    : BinaryPrimitives.ReadInt64BigEndian(data.Slice(byteOffset, 8))),
                FieldType.Float32 => (endian == Endianness.Little
                    ? BinaryPrimitives.ReadSingleLittleEndian(data.Slice(byteOffset, 4))
                    : BinaryPrimitives.ReadSingleBigEndian(data.Slice(byteOffset, 4))),
                FieldType.Float64 => (endian == Endianness.Little
                    ? BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(byteOffset, 8))
                    : BinaryPrimitives.ReadDoubleBigEndian(data.Slice(byteOffset, 8))),
                FieldType.Bool => (data[byteOffset] != 0 ? 1.0 : 0.0),
                _ => 0.0
            };

            // Apply Scale and Offset
            double finalValue = (rawValue * field.Scale) + field.Offset;
            results[fullName] = finalValue;
        }
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
