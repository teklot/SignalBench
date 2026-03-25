namespace SignalBench.Core.Models;

public class ThresholdRule
{
    public string Name { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public string Color { get; set; } = "#FF0000"; // Default Red
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}

public class ThresholdViolation
{
    public DateTime Timestamp { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string Color { get; set; } = "#FF0000";
    public double? Value { get; set; }
}
