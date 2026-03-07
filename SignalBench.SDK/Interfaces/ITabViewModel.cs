using System.ComponentModel;

namespace SignalBench.SDK.Interfaces;

public interface ITabViewModel : INotifyPropertyChanged, IDisposable
{
    string Name { get; set; }
    string StatusMessage { get; set; }
    
    // Icon and connection info for the status bar
    string ConnectionInfo { get; }
    string ConnectionIcon { get; }
}
