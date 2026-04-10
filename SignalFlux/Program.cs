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
        var configOption = new Option<FileInfo?>("--config", "Path to the .conf file");
        configOption.AddAlias("-c");
        
        // Protocol options
        var protocolOption = new Option<string?>("--protocol", "Protocol name (e.g. mavlink, can)");
        protocolOption.AddAlias("-p");
        var messageOption = new Option<string?>("--message", "Message name (e.g. ATTITUDE)");
        messageOption.AddAlias("-m");
        var schemaOption = new Option<FileInfo?>("--schema", "Path to a .yaml schema file");
        schemaOption.AddAlias("-s");

        // Transport options
        var transportTypeOption = new Option<string?>("--transport-type", "Transport type (udp, tcp, serial)");
        transportTypeOption.AddAlias("-t");
        var targetOption = new Option<string?>("--target", "Target endpoint (e.g. 127.0.0.1:14550)");
        targetOption.AddAlias("-g");

        // Signal options
        var signalTypeOption = new Option<string?>("--signal-type", "Signal type (sine, constant)");
        var freqOption = new Option<double>("--freq", "Signal frequency in Hz");
        freqOption.AddAlias("-f");
        var ampOption = new Option<double>("--amp", "Signal amplitude");
        ampOption.AddAlias("-a");

        var schemaDirOption = new Option<DirectoryInfo?>("--schema-dir", "Directory containing .yaml schemas");

        rootCommand.AddOption(configOption);
        rootCommand.AddOption(protocolOption);
        rootCommand.AddOption(messageOption);
        rootCommand.AddOption(schemaOption);
        rootCommand.AddOption(transportTypeOption);
        rootCommand.AddOption(targetOption);
        rootCommand.AddOption(signalTypeOption);
        rootCommand.AddOption(freqOption);
        rootCommand.AddOption(ampOption);
        rootCommand.AddOption(schemaDirOption);

        rootCommand.SetHandler(async (context) =>
        {
            var config = context.ParseResult.GetValueForOption(configOption);
            var protocol = context.ParseResult.GetValueForOption(protocolOption);
            var message = context.ParseResult.GetValueForOption(messageOption);
            var schema = context.ParseResult.GetValueForOption(schemaOption);
            var transportType = context.ParseResult.GetValueForOption(transportTypeOption);
            var target = context.ParseResult.GetValueForOption(targetOption);
            var signalType = context.ParseResult.GetValueForOption(signalTypeOption);
            var freq = context.ParseResult.GetValueForOption(freqOption);
            var amp = context.ParseResult.GetValueForOption(ampOption);
            var schemaDir = context.ParseResult.GetValueForOption(schemaDirOption);

            if (config != null)
            {
                await RunFluxWithConfigAsync(config, schemaDir);
            }
            else if (protocol != null || schema != null)
            {
                await RunFluxInlineAsync(protocol, message, schema, transportType, target, signalType, freq, amp);
            }
            else
            {
                Console.WriteLine("Error: Please specify --config or inline parameters (--protocol/--schema)");
                context.ExitCode = 1;
            }
        });

        return await rootCommand.InvokeAsync(args);
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
