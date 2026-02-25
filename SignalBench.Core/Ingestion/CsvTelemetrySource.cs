using SignalBench.Core.Decoding;

namespace SignalBench.Core.Ingestion;

public class CsvTelemetrySource : ITelemetrySource
{
    private readonly string _filePath;
    private readonly string _delimiter;
    private readonly string? _timestampColumn;
    private string[]? _headers;

    public CsvTelemetrySource(string filePath, string delimiter = ",", string? timestampColumn = null)
    {
        _filePath = filePath;
        _delimiter = delimiter;
        _timestampColumn = timestampColumn;
    }

    public long TotalRecords => 0;

    public IEnumerable<DecodedPacket> ReadPackets()
    {
        using var reader = new StreamReader(_filePath);
        
        // Read header line
        var headerLine = reader.ReadLine();
        if (string.IsNullOrEmpty(headerLine)) yield break;
        
        _headers = headerLine.Split(_delimiter);
        
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            var packet = new DecodedPacket();
            var values = SplitLine(line);
            
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

    private static string[] SplitLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == ',')
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else if (c == '"')
            {
                // Handle quoted strings
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
