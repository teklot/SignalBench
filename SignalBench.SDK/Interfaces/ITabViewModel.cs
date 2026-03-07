using System.ComponentModel;

namespace SignalBench.SDK.Interfaces;

public interface ITabViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Unique ID for the tab type (e.g. "SignalBench.Plot")
    /// </summary>
    string TabTypeId { get; }

    string Name { get; set; }
    string StatusMessage { get; set; }
    
    // Icon and connection info for the status bar
    string ConnectionInfo { get; }
    string ConnectionIcon { get; }

    /// <summary>
    /// Serializes tab-specific settings to a generic dictionary for session persistence.
    /// </summary>
    Dictionary<string, object> GetSettings();

    /// <summary>
    /// Deserializes tab-specific settings from a generic dictionary.
    /// </summary>
    void LoadSettings(Dictionary<string, object> settings);
}
