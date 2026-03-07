using SignalBench.SDK.Interfaces;
using SignalBench.SDK.Models;

namespace SignalBench.Core.Ingestion;

public sealed class CsvTelemetrySource(string filePath, string delimiter = ",", string? timestampColumn = null, bool hasHeader = true) : ITelemetrySource
{
    private string[]? _headers;

    public void Dispose() { }

    public IEnumerable<DecodedPacket> ReadPackets()
    {
        using var reader = new StreamReader(filePath);
        
        string? line;
        if (hasHeader)
        {
            var headerLine = reader.ReadLine();
            if (string.IsNullOrEmpty(headerLine)) yield break;
            _headers = headerLine.Split(delimiter);
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

            var fields = new Dictionary<string, object>();
            DateTime timestamp = DateTime.MinValue;

            for (int i = 0; i < _headers.Length && i < values.Length; i++)
            {
                var header = _headers[i];
                var rawVal = values[i];
                
                if (double.TryParse(rawVal, out double val))
                {
                    fields[header] = val;
                }
                else
                {
                    fields[header] = rawVal;
                }

                if (timestampColumn != null && header.Equals(timestampColumn, StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(rawVal, out var dt))
                        timestamp = dt;
                    else if (double.TryParse(rawVal, out double unix))
                        timestamp = DateTime.UnixEpoch.AddSeconds(unix);
                }
            }
            
            yield return new DecodedPacket 
            { 
                SchemaName = "CSV", 
                Timestamp = timestamp, 
                Fields = fields 
            };
        }
    }

    private string[] SplitLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == delimiter[0] && (delimiter.Length == 1 || line[i..].StartsWith(delimiter)))
            {
                result.Add(current.ToString());
                current.Clear();
                if (delimiter.Length > 1) i += delimiter.Length - 1;
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
        
        return [.. result];
    }

    public void Seek(long position) => throw new NotImplementedException();
}
