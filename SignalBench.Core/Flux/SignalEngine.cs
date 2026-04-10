using SignalBench.Core.Flux.Protocols;
using SignalBench.Core.Flux.Transport;
using SignalBench.Core.Generation;

namespace SignalBench.Core.Flux;

public sealed class SignalEngine : IDisposable
{
    private readonly Dictionary<string, TransportContext> _transports = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FluxChannel> _channels = [];
    private CancellationTokenSource? _cts;

    public void RegisterTransport(string protocol, ITransport transport, IProtocolEncoder encoder, int tickMs)
    {
        _transports[protocol.ToLower()] = new TransportContext(transport, encoder, tickMs);
    }

    public void AddChannel(FluxChannel channel)
    {
        _channels.Add(channel);
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        foreach (var (protocol, context) in _transports)
        {
            var protoChannels = _channels.Where(c => c.Protocol.Equals(protocol, StringComparison.OrdinalIgnoreCase)).ToList();
            Task.Run(() => RunTransportLoop(protocol, context, protoChannels, _cts.Token));
        }
    }

    private async Task RunTransportLoop(string protocol, TransportContext context, List<FluxChannel> channels, CancellationToken ct)
    {
        var scheduler = new Scheduler { TickMs = context.TickMs };
        scheduler.Start();

        while (!ct.IsCancellationRequested)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var time = scheduler.CurrentTime;

            // Group by message/topic within this protocol
            var groupedByMessage = channels.GroupBy(c => c.Message);
            foreach (var msgGroup in groupedByMessage)
            {
                var fieldValues = new Dictionary<string, double>();
                foreach (var channel in msgGroup)
                {
                    fieldValues[channel.Field] = channel.Evaluate(time);
                }

                try
                {
                    var frame = context.Encoder.Encode(fieldValues);
                    context.Transport.Send(frame);
                }
                catch
                {
                    // Log error in production
                }
            }

            sw.Stop();
            var sleep = context.TickMs - sw.Elapsed.TotalMilliseconds;
            if (sleep > 0) await Task.Delay((int)sleep, ct);
        }
        scheduler.Stop();
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        Stop();
        foreach (var context in _transports.Values)
        {
            context.Transport.Dispose();
        }
    }

    private record TransportContext(ITransport Transport, IProtocolEncoder Encoder, int TickMs);
}

public sealed class FluxChannel(string name, string protocol, string message, string field, SignalBase signal)
{
    public string Name { get; } = name;
    public string Protocol { get; } = protocol;
    public string Message { get; } = message;
    public string Field { get; } = field;
    public SignalBase Signal { get; } = signal;

    public double Evaluate(double time) => Signal.Evaluate(time);
}
