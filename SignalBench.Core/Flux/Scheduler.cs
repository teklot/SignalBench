namespace SignalBench.Core.Flux;

public enum TimeMode
{
    RealTime,
    FixedStep
}

public sealed class Scheduler
{
    public int TickMs { get; set; } = 10;
    public TimeMode Mode { get; set; } = TimeMode.RealTime;
    private double _tickCount;
    private readonly object _lock = new();

    public double CurrentTime
    {
        get
        {
            lock (_lock)
            {
                return Mode == TimeMode.FixedStep
                    ? _tickCount * (TickMs / 1000.0)
                    : _stopwatch.Elapsed.TotalSeconds;
            }
        }
    }

    public event Action<double>? OnTick;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private bool _running;

    public void Start()
    {
        lock (_lock)
        {
            _running = true;
            _tickCount = 0;
            _stopwatch.Restart();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _running = false;
            _stopwatch.Stop();
        }
    }

    public void RunLoop(CancellationToken ct = default)
    {
        Start();

        while (!ct.IsCancellationRequested)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var time = CurrentTime;
            OnTick?.Invoke(time);

            lock (_lock)
            {
                if (_running)
                    _tickCount++;
            }

            sw.Stop();
            var elapsed = sw.Elapsed.TotalMilliseconds;
            var sleep = TickMs - elapsed;
            if (sleep > 0)
                Thread.Sleep((int)sleep);
        }
        Stop();
    }
}
