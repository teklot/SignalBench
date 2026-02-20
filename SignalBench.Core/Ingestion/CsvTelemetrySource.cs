using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using SignalBench.Core.Decoding;

namespace SignalBench.Core.Ingestion;

public class CsvTelemetrySource : ITelemetrySource
{
    private readonly string _filePath;
    private readonly CsvConfiguration _config;
    private readonly string? _timestampColumn;

    public CsvTelemetrySource(string filePath, string delimiter = ",", string? timestampColumn = null)
    {
        _filePath = filePath;
        _timestampColumn = timestampColumn;
        _config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
        };
    }

    public long TotalRecords => 0; // Would require counting rows, maybe lazy load

    public IEnumerable<DecodedPacket> ReadPackets()
    {
        using var reader = new StreamReader(_filePath);
        using var csv = new CsvReader(reader, _config);

        csv.Read();
        csv.ReadHeader();
        string[] headers = csv.HeaderRecord!;

        while (csv.Read())
        {
            var packet = new DecodedPacket();
            foreach (var header in headers)
            {
                string rawVal = csv.GetField(header) ?? "";
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

    public void Seek(long position)
    {
        // Seeking in CSV is hard without an index
        throw new NotImplementedException();
    }
}
