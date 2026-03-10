using SignalBench.Core.Data;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;
using SignalBench.Core.Session;
using SignalBench.SDK.Models;

namespace SignalBench.ViewModels;

public partial class MainWindowViewModel
{
    private async Task OpenSchemaEditorAsync()
    {
        try {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            
            var dialog = new SignalBench.Views.SchemaEditor { 
                DataContext = new SchemaEditorViewModel(SelectedSchema) 
            };
            
            var result = await dialog.ShowDialog<SchemaEditorResult?>(topLevel);
            if (result != null) { 
                SelectedSchema = result.Schema; 
            }
        } catch (Exception ex) { await ShowError("Editor Error", "Failed to open schema editor.", ex); }
    }

    private async Task CreateDerivedSignalAsync()
    {
        try {
            if (SelectedPlot == null) return;
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            var availableFields = SelectedPlot.RegularSignals.Select(s => s.Name).ToList();
            var allSignalNames = SelectedPlot.AvailableSignals.Select(s => s.Name).ToList();
            var dialog = new SignalBench.Views.DerivedSignalDialog { DataContext = new DerivedSignalViewModel(availableFields, allSignalNames) };
            var result = await dialog.ShowDialog<DerivedSignalResult?>(topLevel);
            if (result != null) {
                var ds = new DerivedSignalDefinition { Name = result.Name, Formula = result.Formula };
                SelectedPlot.DerivedSignals.Add(ds);
                _dataStore.InsertDerivedSignal(result.Name, ComputeDerivedSignal(ds, _dataStore, SelectedPlot));
                var item = new SignalItemViewModel { Name = result.Name, IsSelected = true, IsDerived = true };
                
                // Add to plot's collection
                SelectedPlot.AvailableSignals.Add(item);
                SelectedPlot.SelectedSignalNames.Add(result.Name);
                
                SyncSignalCheckboxes();
                UpdatePlot(SelectedPlot);
            }
        } catch (Exception ex) { await ShowError("Derived Signal Error", "Failed to create derived signal.", ex); }
    }

    private async Task EditDerivedSignalAsync(string name)
    {
        try {
            if (SelectedPlot == null) return;
            var ds = SelectedPlot.DerivedSignals.FirstOrDefault(d => d.Name == name);
            if (ds == null) return;

            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var availableFields = SelectedPlot.RegularSignals.Select(s => s.Name).ToList();
            var allSignalNames = SelectedPlot.AvailableSignals.Select(s => s.Name).ToList();
            var dialog = new SignalBench.Views.DerivedSignalDialog { 
                DataContext = new DerivedSignalViewModel(availableFields, allSignalNames, ds) { IsEditMode = true } 
            };
            var result = await dialog.ShowDialog<DerivedSignalResult?>(topLevel);

            if (result != null) {
                if (result.IsDeleted) {
                    await RemoveDerivedSignalAsync(name);
                    return;
                }

                ds.Name = result.Name;
                ds.Formula = result.Formula;
                
                // Recompute and update UI
                _dataStore.InsertDerivedSignal(ds.Name, ComputeDerivedSignal(ds, _dataStore, SelectedPlot));
                var item = SelectedPlot.AvailableSignals.FirstOrDefault(s => s.Name == name);
                if (item != null) item.Name = ds.Name;

                UpdatePlot(SelectedPlot);
            }
        } catch (Exception ex) { await ShowError("Edit Error", "Failed to edit derived signal.", ex); }
    }

    private async Task RemoveDerivedSignalAsync(string name)
    {
        try {
            if (SelectedPlot == null) return;
            var ds = SelectedPlot.DerivedSignals.FirstOrDefault(d => d.Name == name);
            if (ds != null) SelectedPlot.DerivedSignals.Remove(ds);

            var pItem = SelectedPlot.AvailableSignals.FirstOrDefault(s => s.Name == name);
            if (pItem != null) SelectedPlot.AvailableSignals.Remove(pItem);

            SelectedPlot.SelectedSignalNames.Remove(name);
            
            _dataStore.DeleteSignal(name);
            UpdatePlot(SelectedPlot);
        } catch (Exception ex) { await ShowError("Remove Error", "Failed to remove derived signal.", ex); }
    }

    private List<double> ComputeDerivedSignal(DerivedSignalDefinition derived, IDataStore? targetStore = null, PlotViewModel? targetPlot = null)
    {
        var result = new List<double>();
        var store = targetStore ?? _dataStore;
        var plot = targetPlot ?? SelectedPlot;
        if (plot == null) return result;

        var timestamps = store.GetTimestamps();
        var schemaSignals = plot.RegularSignals.ToList();
        var data = new Dictionary<string, List<double>>();
        foreach (var s in schemaSignals) data[s.Name] = store.GetSignalData(s.Name);
        
        for (int i = 0; i < timestamps.Count; i++) {
            var vars = new Dictionary<string, object>();
            foreach (var s in schemaSignals) {
                if (i < data[s.Name].Count)
                    vars[s.Name] = data[s.Name][i];
                else
                    vars[s.Name] = 0.0;
            }
            try { 
                var val = _formulaEngine.Evaluate(derived.Formula, vars);
                result.Add(Convert.ToDouble(val)); 
            }
            catch { result.Add(double.NaN); }
        }
        return result;
    }
}
