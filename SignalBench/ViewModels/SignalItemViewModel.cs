using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace SignalBench.ViewModels;

public partial class SignalItemViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private int _systemId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private int _componentId;

    private string? _messageType;
    private string? _displayName;

    public string DisplayName
    {
        get
        {
            _displayName ??= ComputeDisplayName();
            return _displayName;
        }
    }

    public string? MessageType
    {
        get
        {
            _messageType ??= ComputeMessageType();
            return _messageType;
        }
    }

    partial void OnNameChanged(string value)
    {
        _displayName = null;
        _messageType = null;
    }

    private string ComputeDisplayName()
    {
        int dot = Name.IndexOf('.');
        return dot >= 0 ? Name[(dot + 1)..] : Name;
    }

    private string? ComputeMessageType()
    {
        int dot = Name.IndexOf('.');
        return dot >= 0 ? Name[..dot] : null;
    }

    public string? Subtitle => MessageType != null
        ? $"{MessageType} · sys:{SystemId} comp:{ComponentId}"
        : null;

    public string FormattedValue
    {
        get
        {
            if (Lookup != null && Lookup.TryGetValue(CurrentValue, out var mappedValue))
            {
                return mappedValue;
            }

            string val = CurrentValue.ToString("0.#####");
            return string.IsNullOrEmpty(Unit) ? val : $"{val} {Unit}";
        }
    }
}
