using ReactiveUI;
using SignalBench.SDK.Interfaces;

namespace SignalBench.ViewModels;

public abstract class TabViewModelBase : ViewModelBase, ITabViewModel
{
    public abstract string TabTypeId { get; }

    private string _name = "Untitled";
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public virtual string ConnectionInfo => "";
    public virtual string ConnectionIcon => "InformationOutline";

    public virtual Dictionary<string, object> GetSettings() => [];
    public virtual void LoadSettings(Dictionary<string, object> settings) { }

    public abstract void Dispose();
}
