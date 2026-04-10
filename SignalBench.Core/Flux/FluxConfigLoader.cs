using SignalBench.Core.Flux.Protocols;
using SignalBench.Core.Flux.Transport;
using SignalBench.Core.Generation;
using SignalBench.Core.Services;

namespace SignalBench.Core.Flux;

public sealed class FluxConfigLoader(string schemaBasePath)
{
    private readonly string _schemaBasePath = schemaBasePath;
    private readonly SchemaLoader _schemaLoader = new();

    public SignalEngine LoadFromFile(string path)
    {
        var lines = File.ReadAllLines(path);
        var sections = ParseIni(lines);
        var engine = new SignalEngine();

        foreach (var section in sections)
        {
            if (section.Key.StartsWith("transport."))
            {
                var protocol = section.Key["transport.".Length..];
                var type = section.Value.GetValueOrDefault("type", "udp");
                var target = section.Value.GetValueOrDefault("target", "");
                var schemaFile = section.Value.GetValueOrDefault("schema", "");
                
                // Moved from global [timing] to per-transport
                var tickMsStr = section.Value.GetValueOrDefault("tick_ms", "10");
                int.TryParse(tickMsStr, out var tickMs);

                ITransport transport = type.ToLower() switch
                {
                    "udp" => new UdpTransport(target),
                    _ => throw new NotSupportedException($"Transport {type} not supported")
                };
                transport.Connect();

                if (!string.IsNullOrEmpty(schemaFile))
                {
                    var fullSchemaPath = Path.Combine(_schemaBasePath, schemaFile);
                    var yaml = File.ReadAllText(fullSchemaPath);
                    var schema = _schemaLoader.Load(yaml);
                    
                    engine.RegisterTransport(protocol, transport, new SchemaEncoder(schema), tickMs);

                    // Auto-load channels for this schema if a section exists
                    if (sections.TryGetValue(schema.Name, out var channelSection))
                    {
                        foreach (var kv in channelSection)
                        {
                            var signal = ParseSignal(kv.Value);
                            engine.AddChannel(new FluxChannel(kv.Key, protocol, schema.Name, kv.Key, signal));
                        }
                    }
                }
            }
        }

        return engine;
    }

    private Dictionary<string, Dictionary<string, string>> ParseIni(string[] lines)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith(';')) continue;

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1];
                if (!result.ContainsKey(currentSection))
                    result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else if (currentSection != null && trimmed.Contains('='))
            {
                var parts = trimmed.Split('=', 2);
                result[currentSection][parts[0].Trim()] = parts[1].Split('#')[0].Trim();
            }
        }
        return result;
    }

    private SignalBase ParseSignal(string value)
    {
        if (value.Contains("sine"))
        {
            // simplified parser for example
            return new SineSignal(1.0, 1.0);
        }
        if (double.TryParse(value, out var constant))
            return new ConstantSignal(constant);
        
        return new ConstantSignal(0);
    }
}
