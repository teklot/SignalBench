using CommunityToolkit.Mvvm.ComponentModel;

namespace SignalBench.ViewModels;

public class ViewModelBase : ObservableObject
{
    public void RaisePropertyChanged(string? propertyName)
    {
        OnPropertyChanged(propertyName);
    }
}
