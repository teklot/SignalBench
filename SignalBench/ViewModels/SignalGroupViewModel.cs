using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace SignalBench.ViewModels;

public partial class SignalGroupViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool? _isChecked = false;

    public ObservableCollection<SignalItemViewModel> Children { get; } = new();

    private bool _suppressRecalc;

    public void AttachChildren()
    {
        foreach (var child in Children)
        {
            child.PropertyChanged += OnChildPropertyChanged;
        }
        RecalculateCheckedState();
    }

    public void DetachChildren()
    {
        foreach (var child in Children)
        {
            child.PropertyChanged -= OnChildPropertyChanged;
        }
    }

    private void OnChildPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SignalItemViewModel.IsSelected))
        {
            RecalculateCheckedState();
        }
    }

    partial void OnIsCheckedChanged(bool? value)
    {
        if (_suppressRecalc || value == null) return;
        foreach (var child in Children)
        {
            child.IsSelected = value.Value;
        }
    }

    public void RecalculateCheckedState()
    {
        _suppressRecalc = true;

        int selected = Children.Count(c => c.IsSelected);
        if (selected == 0) IsChecked = false;
        else if (selected == Children.Count) IsChecked = true;
        else IsChecked = null;

        _suppressRecalc = false;
    }
}
