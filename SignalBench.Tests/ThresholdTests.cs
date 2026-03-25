using Xunit;
using SignalBench.Core.DerivedSignals;
using System.Collections.Generic;

namespace SignalBench.Tests;

public class ThresholdTests
{
    [Fact]
    public void FormulaEngine_EvaluateCondition_Works()
    {
        var engine = new FormulaEngine();
        var parameters = new Dictionary<string, object>
        {
            { "battery_voltage", 6.0 },
            { "current", 1.5 }
        };

        Assert.True(engine.EvaluateCondition("battery_voltage < 6.5", parameters));
        Assert.False(engine.EvaluateCondition("battery_voltage > 6.5", parameters));
        Assert.True(engine.EvaluateCondition("battery_voltage < 6.5 AND current > 1.0", parameters));
    }
}
