namespace SignalBench.Core.Generation;

public sealed class SineSignal(double amplitude = 1.0, double frequency = 1.0, double phase = 0.0, double offset = 0.0) : SignalBase
{
    public double Amplitude { get; } = amplitude;
    public double Frequency { get; } = frequency;
    public double Phase { get; } = phase;
    public double Offset { get; } = offset;

    public override double Evaluate(double time)
    {
        return Amplitude * Math.Sin(2.0 * Math.PI * Frequency * time + Phase) + Offset;
    }
}
