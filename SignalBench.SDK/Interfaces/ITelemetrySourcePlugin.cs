using SignalBench.SDK.Models;

namespace SignalBench.SDK.Interfaces;

/// <summary>
/// A plugin that provides a telemetry source.
/// </summary>
public interface ITelemetrySourcePlugin : IPlugin
{
    /// <summary>
    /// Creates a new instance of the telemetry source.
    /// </summary>
    /// <param name="connectionString">A configuration string or JSON specific to the source.</param>
    /// <param name="schema">The schema to use for decoding.</param>
    /// <returns>An initialized telemetry source.</returns>
    ITelemetrySource CreateSource(string connectionString, object? schema = null);
}
