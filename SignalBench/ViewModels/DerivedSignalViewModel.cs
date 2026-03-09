using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalBench.Core.Session;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
using System.Linq;

namespace SignalBench.ViewModels;

public partial class DerivedSignalViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private string _name = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private string _formula = "";

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private bool _isEditMode;

    public event Action<DerivedSignalResult?>? RequestClose;

    public ObservableCollection<string> AvailableFields { get; } = [];
    private readonly List<string> _existingNames;
    private readonly string? _originalName;

    [RelayCommand]
    private void Delete() => RequestClose?.Invoke(new DerivedSignalResult { Name = Name, Formula = Formula, IsDeleted = true });

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add() => RequestClose?.Invoke(new DerivedSignalResult { Name = Name, Formula = Formula });

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(null);

    public bool CanAdd()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Formula)) return false;

        // If it's a new name or changed name, check for duplicates
        if (Name != _originalName && _existingNames.Contains(Name, StringComparer.OrdinalIgnoreCase))
        {
            ValidationMessage = "A signal with this name already exists.";
            return false;
        }

        ValidationMessage = null;
        return true;
    }

    public DerivedSignalViewModel(IEnumerable<string> availableFields, IEnumerable<string> existingNames, DerivedSignalDefinition? existing = null)
    {
        foreach (var f in availableFields) AvailableFields.Add(f);
        _existingNames = existingNames.ToList();

        if (existing != null)
        {
            _name = existing.Name;
            _originalName = existing.Name;
            _formula = existing.Formula;
            _isEditMode = true;
        }
    }
}

public class DerivedSignalResult
{
    public string Name { get; set; } = "";
    public string Formula { get; set; } = "";
    public bool IsDeleted { get; set; }
}