using SignalBench.Core.Models.Schema;
using SignalBench.Core.Utilities;

namespace SignalBench.Core.Flux.Protocols;

public sealed class SchemaEncoder(PacketSchema schema) : IProtocolEncoder
{
    public string ProtocolName => schema.Name;

    public byte[] Encode(Dictionary<string, double> fieldValues)
    {
        // Calculate required length in bytes
        int maxBit = 0;
        foreach (var field in schema.Fields)
        {
            maxBit = Math.Max(maxBit, field.BitOffset + field.BitLength);
        }
        
        if (schema.Crc != null)
        {
            maxBit = Math.Max(maxBit, schema.Crc.BitOffset + schema.Crc.BitLength);
        }

        int byteLength = (maxBit + 7) / 8;
        var buffer = new byte[byteLength];
        bool bigEndian = schema.Endianness == Endianness.Big;

        foreach (var field in schema.Fields)
        {
            if (!fieldValues.TryGetValue(field.Name, out double rawValue))
                continue;

            // Apply scale and offset: raw = (physical - offset) / scale
            double valueToEncode = (rawValue - field.Offset) / field.Scale;

            // Handle byte-aligned fields for now (simpler)
            if (field.BitOffset % 8 == 0 && field.BitLength % 8 == 0)
            {
                int byteOffset = field.BitOffset / 8;
                var span = buffer.AsSpan(byteOffset);

                switch (field.Type)
                {
                    case FieldType.Uint8: buffer[byteOffset] = (byte)valueToEncode; break;
                    case FieldType.Int8: buffer[byteOffset] = (byte)(sbyte)valueToEncode; break;
                    case FieldType.Uint16: BinaryUtils.WriteUInt16(span, (ushort)valueToEncode, bigEndian); break;
                    case FieldType.Int16: BinaryUtils.WriteInt16(span, (short)valueToEncode, bigEndian); break;
                    case FieldType.Uint32: BinaryUtils.WriteUInt32(span, (uint)valueToEncode, bigEndian); break;
                    case FieldType.Int32: BinaryUtils.WriteInt32(span, (int)valueToEncode, bigEndian); break;
                    case FieldType.Uint64: BinaryUtils.WriteUInt64(span, (ulong)valueToEncode, bigEndian); break;
                    case FieldType.Int64: BinaryUtils.WriteInt64(span, (long)valueToEncode, bigEndian); break;
                    case FieldType.Float32: BinaryUtils.WriteFloat32(span, (float)valueToEncode, bigEndian); break;
                    case FieldType.Float64: BinaryUtils.WriteFloat64(span, valueToEncode, bigEndian); break;
                    case FieldType.Bool: buffer[byteOffset] = valueToEncode != 0 ? (byte)1 : (byte)0; break;
                }
            }
            // TODO: Add bit-level non-aligned encoding if needed later
        }

        // TODO: Add CRC calculation logic based on schema.Crc

        return buffer;
    }
}
