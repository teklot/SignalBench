using ReactiveUI;

namespace SignalBench.ViewModels;

public class SignalItemViewModel : ViewModelBase
{
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    private bool _isDerived;
    public bool IsDerived
    {
        get => _isDerived;
        set => this.RaiseAndSetIfChanged(ref _isDerived, value);
    }

    private int _colorIndex;
    public int ColorIndex
    {
        get => _colorIndex;
        set => this.RaiseAndSetIfChanged(ref _colorIndex, value);
    }
}
