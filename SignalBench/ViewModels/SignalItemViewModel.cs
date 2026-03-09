using CommunityToolkit.Mvvm.ComponentModel;

namespace SignalBench.ViewModels;

public partial class SignalItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isDerived;

    [ObservableProperty]
    private int _colorIndex;

    [ObservableProperty]
    private double _currentValue;
}