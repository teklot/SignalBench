namespace SignalBench.SDK.Interfaces;

public interface ITabFactory
{
    /// <summary>
    /// Unique ID for the tab type (e.g. "SignalBench.Plot")
    /// </summary>
    string TabTypeId { get; }
    
    /// <summary>
    /// Display name for the menu (e.g. "New Plot")
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Material Icon name for the menu
    /// </summary>
    string Icon { get; }

    /// <summary>
    /// Creates a new instance of the tab view model.
    /// </summary>
    ITabViewModel CreateTab();
}
