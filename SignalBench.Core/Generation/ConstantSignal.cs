namespace SignalBench.Core.Generation;

public sealed class ConstantSignal(double value) : SignalBase
{
    public double Value { get; } = value;

    public override double Evaluate(double time) => Value;
}
