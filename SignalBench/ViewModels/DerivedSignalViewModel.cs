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
    private readonly List<string> _existingNames;
    private readonly string? _originalName;

    public ReactiveCommand<Unit, DerivedSignalResult?> DeleteCommand { get; }
    public ReactiveCommand<Unit, DerivedSignalResult?> AddCommand { get; }
    public ReactiveCommand<Unit, DerivedSignalResult?> CancelCommand { get; }

    public DerivedSignalViewModel(IEnumerable<string> availableFields, IEnumerable<string> existingNames, DerivedSignalDefinition? existing = null)
    {
        foreach (var f in availableFields) AvailableFields.Add(f);
        _existingNames = existingNames.ToList();

        if (existing != null)
        {
            _name = existing.Name;
            _originalName = existing.Name;
            _formula = existing.Formula;
        }

        var canAdd = this.WhenAnyValue(
            x => x.Name,
            x => x.Formula,
            (name, formula) => {
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(formula)) return false;
                
                // If it's a new name or changed name, check for duplicates
                if (name != _originalName && _existingNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    ValidationMessage = "A signal with this name already exists.";
                    return false;
                }
                
                ValidationMessage = null;
                return true;
            });

        DeleteCommand = ReactiveCommand.Create<DerivedSignalResult?>(() =>
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
