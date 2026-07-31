using System.Runtime.InteropServices;
using MavLinkSharp;
using SignalBench.Core.Models.Schema;
using SignalBench.SDK.Models;

namespace SignalBench.Core.Decoding;

public sealed class MavlinkDecoder
{
    private readonly Frame _frame = new();
    private readonly List<byte> _buffer = [];

    private static readonly byte[] StartMarkers = [0xFD, 0xFE];

    public void PushData(byte[] data)
    {
        lock (_buffer)
        {
            _buffer.AddRange(data);
        }
    }

    public bool TryReadPacket(out DecodedPacket? packet)
    {
        packet = null;
        lock (_buffer)
        {
            while (_buffer.Count >= 12)
            {
                int startIdx = -1;
                for (int i = 0; i < _buffer.Count; i++)
                {
                    if (_buffer[i] == 0xFD || _buffer[i] == 0xFE)
                    {
                        startIdx = i;
                        break;
                    }
                }

                if (startIdx < 0)
                {
                    _buffer.Clear();
                    return false;
                }

                if (startIdx > 0)
                    _buffer.RemoveRange(0, startIdx);

                var span = CollectionsMarshal.AsSpan(_buffer);
                if (!_frame.TryParse(span))
                {
                    _buffer.RemoveAt(0);
                    continue;
                }

                int consumed = _frame.PacketLength;
                if (consumed <= 0 || consumed > _buffer.Count)
                {
                    _buffer.Clear();
                    return false;
                }

                var messageName = Metadata.Messages[_frame.MessageId].Name;
                var fields = new Dictionary<string, object>();
                foreach (var kvp in _frame.Fields)
                {
                    fields[$"{messageName}.{kvp.Key}"] = ConvertToDouble(kvp.Value);
                }

                packet = new DecodedPacket
                {
                    SchemaName = messageName,
                    Timestamp = DateTime.Now,
                    Fields = fields,
                    IsValid = true,
                    SystemId = _frame.SystemId,
                    ComponentId = _frame.ComponentId
                };

                _buffer.RemoveRange(0, consumed);
                return true;
            }
        }
        return false;
    }

    public static PacketSchema CreateSchemaFromDialect()
    {
        var fields = new List<FieldDefinition>();
        foreach (var msgKvp in Metadata.Messages)
        {
            var msg = msgKvp.Value;
            if (msg.Fields == null || msg.Fields.Count == 0) continue;

            foreach (var fieldDef in msg.Fields)
            {
                fields.Add(new FieldDefinition
                {
                    Name = $"{msg.Name}.{fieldDef.Name}",
                    Type = FieldType.Float64,
                    Unit = fieldDef.Units
                });
            }
        }
        return new PacketSchema { Name = "MAVLink", Fields = fields };
    }

    private static double ConvertToDouble(object val)
    {
        if (val is double d) return d;
        if (val is float f) return f;
        if (val is int i) return i;
        if (val is uint ui) return ui;
        if (val is byte b) return b;
        if (val is short s) return s;
        if (val is ushort us) return us;
        if (val is long l) return l;
        if (val is ulong ul) return ul;
        if (val is sbyte sb) return sb;
        if (val is bool bv) return bv ? 1.0 : 0.0;
        if (val is string str && double.TryParse(str, out var parsed)) return parsed;
        return double.NaN;
    }
}
