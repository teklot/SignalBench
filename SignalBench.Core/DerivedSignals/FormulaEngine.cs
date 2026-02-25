using NCalc;

namespace SignalBench.Core.DerivedSignals;

public class FormulaEngine
{
    public double Evaluate(string formula, Dictionary<string, object> parameters)
    {
        var expression = new Expression(formula, ExpressionOptions.AllowNullParameter | ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
        foreach (var kv in parameters)
        {
            expression.Parameters[kv.Key] = kv.Value;
        }

        var result = expression.Evaluate();
        return Convert.ToDouble(result);
    }
}
