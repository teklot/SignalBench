using CommunityToolkit.Mvvm.ComponentModel;
using SignalBench.SDK.Interfaces;
using System.Collections.Generic;

namespace SignalBench.ViewModels;

public abstract partial class TabViewModelBase : ViewModelBase, ITabViewModel
{
    public abstract string TabTypeId { get; }

    [ObservableProperty]
    private string _name = "Untitled";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public virtual string ConnectionInfo => "";
    public virtual string ConnectionIcon => "InformationOutline";

    public virtual Dictionary<string, object> GetSettings() => [];
    public virtual void LoadSettings(Dictionary<string, object> settings) { }

    public abstract void Dispose();
}