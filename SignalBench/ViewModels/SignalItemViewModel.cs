using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

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
    [NotifyPropertyChangedFor(nameof(FormattedValue))]
    private double _currentValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedValue))]
    private string? _unit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedValue))]
    private Dictionary<double, string>? _lookup;

    public string FormattedValue
    {
        get
        {
            if (Lookup != null && Lookup.TryGetValue(CurrentValue, out var mappedValue))
            {
                return mappedValue;
            }

            string val = CurrentValue.ToString("G5");
            return string.IsNullOrEmpty(Unit) ? val : $"{val} {Unit}";
        }
    }
}
