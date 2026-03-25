using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalBench.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SignalBench.ViewModels;

public partial class ThresholdRuleViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _formula = "";

    [ObservableProperty]
    private string _color = "#FF0000";

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private bool _isEditMode;

    public event Action<ThresholdRuleResult?>? RequestClose;

    public ObservableCollection<string> AvailableFields { get; } = [];
    private readonly List<string> _existingNames;
    private readonly string? _originalName;

    [RelayCommand]
    private void Delete() => RequestClose?.Invoke(new ThresholdRuleResult { Name = Name, Formula = Formula, IsDeleted = true });

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => RequestClose?.Invoke(new ThresholdRuleResult 
    { 
        Name = Name, 
        Formula = Formula, 
        Color = Color, 
        IsActive = IsActive, 
        Description = Description 
    });

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(null);

    public bool CanSave()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Formula)) return false;

        if (Name != _originalName && _existingNames.Contains(Name, StringComparer.OrdinalIgnoreCase))
        {
            ValidationMessage = "A rule with this name already exists.";
            return false;
        }

        ValidationMessage = null;
        return true;
    }

    public ThresholdRuleViewModel(IEnumerable<string> availableFields, IEnumerable<string> existingNames, ThresholdRule? existing = null)
    {
        foreach (var f in availableFields) AvailableFields.Add(f);
        _existingNames = existingNames.ToList();

        if (existing != null)
        {
            _name = existing.Name;
            _originalName = existing.Name;
            _formula = existing.Formula;
            _color = existing.Color;
            _isActive = existing.IsActive;
            _description = existing.Description;
            _isEditMode = true;
        }
    }
}

public class ThresholdRuleResult
{
    public string Name { get; set; } = "";
    public string Formula { get; set; } = "";
    public string Color { get; set; } = "#FF0000";
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
}
