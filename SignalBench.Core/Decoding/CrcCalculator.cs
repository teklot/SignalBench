using SignalBench.Core.Models.Schema;

namespace SignalBench.Core.Decoding;

public static class CrcCalculator
{
    public static uint CalculateCrc(ReadOnlySpan<byte> data, CrcType type, uint polynomial, uint initialValue, uint finalXor, bool reflectInput, bool reflectOutput)
    {
        return type switch
        {
            CrcType.Crc8 => CalculateCrc8(data, (byte)polynomial, (byte)initialValue, (byte)finalXor, reflectInput, reflectOutput),
            CrcType.Crc16 => CalculateCrc16(data, (ushort)polynomial, (ushort)initialValue, (ushort)finalXor, reflectInput, reflectOutput),
            CrcType.Crc32 => CalculateCrc32(data, polynomial, initialValue, finalXor, reflectInput, reflectOutput),
            _ => 0
        };
    }

    private static byte CalculateCrc8(ReadOnlySpan<byte> data, byte polynomial, byte initialValue, byte finalXor, bool reflectInput, bool reflectOutput)
    {
        byte crc = initialValue;
        
        if (reflectInput)
        {
            byte reflectedPoly = Reflect8(polynomial);
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 1) != 0)
                        crc = (byte)((crc >> 1) ^ reflectedPoly);
                    else
                        crc >>= 1;
                }
            }
            if (!reflectOutput) crc = Reflect8(crc);
        }
        else
        {
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x80) != 0)
                        crc = (byte)((crc << 1) ^ polynomial);
                    else
                        crc <<= 1;
                }
            }
            if (reflectOutput) crc = Reflect8(crc);
        }
        
        return (byte)(crc ^ finalXor);
    }

    private static ushort CalculateCrc16(ReadOnlySpan<byte> data, ushort polynomial, ushort initialValue, ushort finalXor, bool reflectInput, bool reflectOutput)
    {
        ushort crc = initialValue;

        if (reflectInput)
        {
            ushort reflectedPoly = Reflect16(polynomial);
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 1) != 0)
                        crc = (ushort)((crc >> 1) ^ reflectedPoly);
                    else
                        crc >>= 1;
                }
            }
            if (!reflectOutput) crc = Reflect16(crc);
        }
        else
        {
            foreach (byte b in data)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ polynomial);
                    else
                        crc <<= 1;
                }
            }
            if (reflectOutput) crc = Reflect16(crc);
        }

        return (ushort)(crc ^ finalXor);
    }

    private static uint CalculateCrc32(ReadOnlySpan<byte> data, uint polynomial, uint initialValue, uint finalXor, bool reflectInput, bool reflectOutput)
    {
        uint crc = initialValue;

        if (reflectInput)
        {
            uint reflectedPoly = Reflect32(polynomial);
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ reflectedPoly;
                    else
                        crc >>= 1;
                }
            }
            if (!reflectOutput) crc = Reflect32(crc);
        }
        else
        {
            foreach (byte b in data)
            {
                crc ^= (uint)(b << 24);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x80000000) != 0)
                        crc = (crc << 1) ^ polynomial;
                    else
                        crc <<= 1;
                }
            }
            if (reflectOutput) crc = Reflect32(crc);
        }

        return crc ^ finalXor;
    }

    private static byte Reflect8(byte val)
    {
        uint res = 0;
        for (int i = 0; i < 8; i++)
        {
            if ((val & (1 << i)) != 0) res |= (uint)(1 << (7 - i));
        }
        return (byte)res;
    }

    private static ushort Reflect16(ushort val)
    {
        uint res = 0;
        for (int i = 0; i < 16; i++)
        {
            if ((val & (1 << i)) != 0) res |= (uint)(1 << (15 - i));
        }
        return (ushort)res;
    }

    private static uint Reflect32(uint val)
    {
        uint res = 0;
        for (int i = 0; i < 32; i++)
        {
            if ((val & (1u << i)) != 0) res |= (1u << (31 - i));
        }
        return res;
    }
}
