using SignalBench.Core.Decoding;

namespace SignalBench.Core.Ingestion;

public class CsvTelemetrySource : ITelemetrySource
{
    private readonly string _filePath;
    private readonly string _delimiter;
    private readonly string? _timestampColumn;
    private readonly bool _hasHeader;
    private string[]? _headers;

    public CsvTelemetrySource(string filePath, string delimiter = ",", string? timestampColumn = null, bool hasHeader = true)
    {
        _filePath = filePath;
        _delimiter = delimiter;
        _timestampColumn = timestampColumn;
        _hasHeader = hasHeader;
    }

    public long TotalRecords => 0;

    public IEnumerable<DecodedPacket> ReadPackets()
    {
        using var reader = new StreamReader(_filePath);
        
        string? line;
        if (_hasHeader)
        {
            var headerLine = reader.ReadLine();
            if (string.IsNullOrEmpty(headerLine)) yield break;
            _headers = headerLine.Split(_delimiter);
        }

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            var values = SplitLine(line);

            if (_headers == null)
            {
                _headers = new string[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    _headers[i] = $"field{i + 1}";
                }
            }

            var packet = new DecodedPacket();
            for (int i = 0; i < _headers.Length && i < values.Length; i++)
            {
                var header = _headers[i];
                var rawVal = values[i];
                
                if (double.TryParse(rawVal, out double val))
                {
                    packet.Fields[header] = val;
                }
                else
                {
                    packet.Fields[header] = rawVal;
                }

                if (_timestampColumn != null && header.Equals(_timestampColumn, StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(rawVal, out var dt))
                        packet.Timestamp = dt;
                    else if (double.TryParse(rawVal, out double unix))
                        packet.Timestamp = DateTime.UnixEpoch.AddSeconds(unix);
                }
            }
            
            yield return packet;
        }
    }

    private string[] SplitLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == _delimiter[0] && (_delimiter.Length == 1 || line.Substring(i).StartsWith(_delimiter)))
            {
                result.Add(current.ToString());
                current.Clear();
                if (_delimiter.Length > 1) i += _delimiter.Length - 1;
            }
            else if (c == '"')
            {
                i++;
                while (i < line.Length && line[i] != '"')
                {
                    current.Append(line[i]);
                    i++;
                }
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        
        return result.ToArray();
    }

    public void Seek(long position)
    {
        throw new NotImplementedException();
    }
}
