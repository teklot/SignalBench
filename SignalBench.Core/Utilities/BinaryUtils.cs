namespace SignalBench.Core.Utilities;

internal static class BinaryUtils
{
    public static void WriteUInt16(Span<byte> span, ushort value, bool bigEndian)
    {
        if (bigEndian)
        {
            span[0] = (byte)(value >> 8);
            span[1] = (byte)value;
        }
        else
        {
            span[0] = (byte)value;
            span[1] = (byte)(value >> 8);
        }
    }

    public static void WriteInt16(Span<byte> span, short value, bool bigEndian) => WriteUInt16(span, (ushort)value, bigEndian);

    public static void WriteUInt32(Span<byte> span, uint value, bool bigEndian)
    {
        if (bigEndian)
        {
            span[0] = (byte)(value >> 24);
            span[1] = (byte)(value >> 16);
            span[2] = (byte)(value >> 8);
            span[3] = (byte)value;
        }
        else
        {
            span[0] = (byte)value;
            span[1] = (byte)(value >> 8);
            span[2] = (byte)(value >> 16);
            span[3] = (byte)(value >> 24);
        }
    }

    public static void WriteInt32(Span<byte> span, int value, bool bigEndian) => WriteUInt32(span, (uint)value, bigEndian);

    public static void WriteUInt64(Span<byte> span, ulong value, bool bigEndian)
    {
        if (bigEndian)
        {
            for (int i = 0; i < 8; i++) span[i] = (byte)(value >> (8 * (7 - i)));
        }
        else
        {
            for (int i = 0; i < 8; i++) span[i] = (byte)(value >> (8 * i));
        }
    }

    public static void WriteInt64(Span<byte> span, long value, bool bigEndian) => WriteUInt64(span, (ulong)value, bigEndian);

    public static void WriteFloat32(Span<byte> span, float value, bool bigEndian)
    {
        uint val = BitConverter.SingleToUInt32Bits(value);
        WriteUInt32(span, val, bigEndian);
    }

    public static void WriteFloat64(Span<byte> span, double value, bool bigEndian)
    {
        ulong val = BitConverter.DoubleToUInt64Bits(value);
        WriteUInt64(span, val, bigEndian);
    }
}
