using System.CommandLine;
using SignalBench.Core.Flux;
using SignalBench.Core.Flux.Transport;
using SignalBench.Core.Flux.Protocols;
using SignalBench.Core.Generation;
using SignalBench.Core.Services;

namespace SignalFlux;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("SignalFlux - Binary Protocol Signal Generation Engine");

        // Config file option
        var configOption = new Option<FileInfo?>("--config", "-c") { Description = "Path to the .conf file" };
        
        // Protocol options
        var protocolOption = new Option<string?>("--protocol", "-p") { Description = "Protocol name (e.g. mavlink, can)" };
        var messageOption = new Option<string?>("--message", "-m") { Description = "Message name (e.g. ATTITUDE)" };
        var schemaOption = new Option<FileInfo?>("--schema", "-s") { Description = "Path to a .yaml schema file" };

        // Transport options
        var transportTypeOption = new Option<string?>("--transport-type", "-t") { Description = "Transport type (udp, tcp, serial)" };
        var targetOption = new Option<string?>("--target", "-g") { Description = "Target endpoint (e.g. 127.0.0.1:14550)" };

        // Signal options
        var signalTypeOption = new Option<string?>("--signal-type") { Description = "Signal type (sine, constant)" };
        var freqOption = new Option<double>("--freq", "-f") { Description = "Signal frequency in Hz" };
        var ampOption = new Option<double>("--amp", "-a") { Description = "Signal amplitude" };

        var schemaDirOption = new Option<DirectoryInfo?>("--schema-dir") { Description = "Directory containing .yaml schemas" };

        rootCommand.Options.Add(configOption);
        rootCommand.Options.Add(protocolOption);
        rootCommand.Options.Add(messageOption);
        rootCommand.Options.Add(schemaOption);
        rootCommand.Options.Add(transportTypeOption);
        rootCommand.Options.Add(targetOption);
        rootCommand.Options.Add(signalTypeOption);
        rootCommand.Options.Add(freqOption);
        rootCommand.Options.Add(ampOption);
        rootCommand.Options.Add(schemaDirOption);

        rootCommand.SetAction(async (parseResult, ct) =>
        {
            var config = parseResult.GetValue(configOption);
            var protocol = parseResult.GetValue(protocolOption);
            var message = parseResult.GetValue(messageOption);
            var schema = parseResult.GetValue(schemaOption);
            var transportType = parseResult.GetValue(transportTypeOption);
            var target = parseResult.GetValue(targetOption);
            var signalType = parseResult.GetValue(signalTypeOption);
            var freq = parseResult.GetValue(freqOption);
            var amp = parseResult.GetValue(ampOption);
            var schemaDir = parseResult.GetValue(schemaDirOption);

            if (config != null)
            {
                await RunFluxWithConfigAsync(config, schemaDir);
                return 0;
            }
            else if (protocol != null || schema != null)
            {
                await RunFluxInlineAsync(protocol, message, schema, transportType, target, signalType, freq, amp);
                return 0;
            }
            else
            {
                Console.WriteLine("Error: Please specify --config or inline parameters (--protocol/--schema)");
                return 1;
            }
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }

    static async Task RunFluxWithConfigAsync(FileInfo configFile, DirectoryInfo? schemaDir)
    {
        Console.WriteLine($"[SignalFlux] Loading configuration from {configFile.FullName}");
        try 
        {
            var loader = new FluxConfigLoader(schemaDir?.FullName ?? configFile.DirectoryName ?? ".");
            using var engine = loader.LoadFromFile(configFile.FullName);
            await RunEngineAsync(engine);
        }
        catch (Exception ex) { Console.WriteLine($"[Error] {ex.Message}"); }
    }

    static async Task RunFluxInlineAsync(string? protocol, string? message, FileInfo? schemaFile, string? transportType, string? target, string? signalType, double freq, double amp)
    {
        protocol ??= "generic";
        message ??= "DATA";
        transportType ??= "udp";
        target ??= "127.0.0.1:14550";
        signalType ??= "sine";
        if (amp == 0) amp = 1.0;
        if (freq == 0) freq = 1.0;

        Console.WriteLine($"[SignalFlux] Starting inline simulation: {protocol}/{message} via {transportType} to {target}");

        try 
        {
            using var engine = new SignalEngine();

            ITransport transport = transportType.ToLower() switch {
                "udp" => new UdpTransport(target),
                _ => throw new NotSupportedException($"Transport {transportType} not supported")
            };
            transport.Connect();

            if (schemaFile != null && schemaFile.Exists)
            {
                var schema = new SchemaLoader().Load(File.ReadAllText(schemaFile.FullName));
                engine.RegisterTransport(protocol, transport, new SchemaEncoder(schema), 20);
                
                // Map all fields in the schema to the same signal for simple inline testing
                foreach (var field in schema.Fields)
                {
                    var signal = signalType.ToLower() == "sine" ? (SignalBase)new SineSignal(amp, freq) : new ConstantSignal(amp);
                    engine.AddChannel(new FluxChannel(field.Name, protocol, schema.Name, field.Name, signal));
                }
            }
            else
            {
                Console.WriteLine("[Warning] No schema provided. In-line simulation requires a schema.");
            }

            await RunEngineAsync(engine);
        }
        catch (Exception ex) { Console.WriteLine($"[Error] {ex.Message}"); }
    }

    static async Task RunEngineAsync(SignalEngine engine)
    {
        Console.WriteLine("[SignalFlux] Engine started. Press Ctrl+C to stop.");
        engine.Start();

        var tcs = new TaskCompletionSource();
        Console.CancelKeyPress += (s, e) => {
            e.Cancel = true;
            tcs.SetResult();
        };

        await tcs.Task;
        Console.WriteLine("[SignalFlux] Stopping engine...");
    }
}
