using ReactiveUI;
using SignalBench.Core.Session;
using System.Collections.ObjectModel;
using System.Reactive;

namespace SignalBench.ViewModels;

public class DerivedSignalViewModel : ViewModelBase
{
    private string _name = "";
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private string _formula = "";
    public string Formula
    {
        get => _formula;
        set => this.RaiseAndSetIfChanged(ref _formula, value);
    }

    private string? _validationMessage;
    public string? ValidationMessage
    {
        get => _validationMessage;
        set => this.RaiseAndSetIfChanged(ref _validationMessage, value);
    }

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set => this.RaiseAndSetIfChanged(ref _isEditMode, value);
    }

    public ObservableCollection<string> AvailableFields { get; } = [];

    public ReactiveCommand<Unit, DerivedSignalResult?> DeleteCommand { get; }
    public ReactiveCommand<Unit, DerivedSignalResult?> AddCommand { get; }
    public ReactiveCommand<Unit, DerivedSignalResult?> CancelCommand { get; }

    public DerivedSignalViewModel(IEnumerable<string> availableFields, DerivedSignalDefinition? existing = null)
    {
        foreach (var f in availableFields) AvailableFields.Add(f);

        if (existing != null)
        {
            _name = existing.Name;
            _formula = existing.Formula;
        }

        var canAdd = this.WhenAnyValue(
            x => x.Name,
            x => x.Formula,
            (name, formula) => !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(formula));

        DeleteCommand = ReactiveCommand.Create(() =>
        {
            return new DerivedSignalResult { Name = Name, Formula = Formula, IsDeleted = true };
        });

        AddCommand = ReactiveCommand.Create<DerivedSignalResult?>(() =>
        {
            ValidationMessage = null;
            return new DerivedSignalResult
            {
                Name = Name,
                Formula = Formula
            };
        }, canAdd);

        CancelCommand = ReactiveCommand.Create(() => (DerivedSignalResult?)null);
    }
}

public class DerivedSignalResult
{
    public string Name { get; set; } = "";
    public string Formula { get; set; } = "";
    public bool IsDeleted { get; set; }
}
