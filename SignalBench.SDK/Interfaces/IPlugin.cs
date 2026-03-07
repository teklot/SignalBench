namespace SignalBench.SDK.Interfaces;

/// <summary>
/// Defines the contract for a SignalBench plugin.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// The unique identifier for the plugin.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The display name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// A brief description of what the plugin does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The version of the plugin.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    void Initialize();
}
