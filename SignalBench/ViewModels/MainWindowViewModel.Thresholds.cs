using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using SignalBench.Core.Models;
using SignalBench.Core.Session;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SignalBench.ViewModels;

public partial class MainWindowViewModel
{
    private async Task ExportViolationsAsync()
    {
        try {
            if (SelectedPlot == null || SelectedPlot.ThresholdRules.Count == 0) return;
            
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
                Title = "Export Violations",
                DefaultExtension = "csv",
                FileTypeChoices = [new FilePickerFileType("CSV File") { Patterns = ["*.csv"] }]
            });
            
            if (file != null) {
                var timestamps = SelectedPlot.DataStore.GetTimestamps();
                var plotData = new Dictionary<string, List<double>>();
                foreach (var signal in SelectedPlot.AvailableSignals)
                    plotData[signal.Name] = SelectedPlot.DataStore.GetSignalData(signal.Name);

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Timestamp,Rule Name,Formula");

                for (int i = 0; i < timestamps.Count; i++)
                {
                    var parameters = new Dictionary<string, object>();
                    foreach (var kv in plotData)
                    {
                        if (kv.Value.Count > i) parameters[kv.Key] = kv.Value[i];
                    }

                    foreach (var rule in SelectedPlot.ThresholdRules)
                    {
                        if (rule.IsActive && _formulaEngine.EvaluateCondition(rule.Formula, parameters))
                        {
                            csv.AppendLine($"{timestamps[i]:yyyy-MM-dd HH:mm:ss.fff},{rule.Name},\"{rule.Formula.Replace("\"", "\"\"")}\"");
                        }
                    }
                }

                await File.WriteAllTextAsync(file.Path.LocalPath, csv.ToString());
                StatusText = "Violations exported.";
            }
        } catch (Exception ex) { await ShowError("Export Error", "Failed to export violations.", ex); }
    }

    private void InitializeThresholdCommands()
    {
        // These are initialized in the constructor but since this is a partial class 
        // we can't easily add to the main constructor without modifying it.
        // Actually, I should have added them to the main constructor.
    }

    private async Task CreateThresholdRuleAsync()
    {
        try {
            if (SelectedPlot == null) return;
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;
            
            var availableFields = SelectedPlot.AvailableSignals.Select(s => s.Name).ToList();
            var existingNames = SelectedPlot.ThresholdRules.Select(r => r.Name).ToList();
            
            var dialog = new SignalBench.Views.ThresholdRuleDialog { 
                DataContext = new ThresholdRuleViewModel(availableFields, existingNames) 
            };
            
            var result = await dialog.ShowDialog<ThresholdRuleResult?>(topLevel);
            if (result != null) {
                var rule = new ThresholdRule { 
                    Name = result.Name, 
                    Formula = result.Formula, 
                    Color = result.Color, 
                    IsActive = result.IsActive, 
                    Description = result.Description 
                };
                SelectedPlot.ThresholdRules.Add(rule);
                UpdatePlot(SelectedPlot);
            }
        } catch (Exception ex) { await ShowError("Threshold Error", "Failed to create threshold rule.", ex); }
    }

    private async Task EditThresholdRuleAsync(string name)
    {
        try {
            if (SelectedPlot == null) return;
            var rule = SelectedPlot.ThresholdRules.FirstOrDefault(r => r.Name == name);
            if (rule == null) return;

            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var availableFields = SelectedPlot.AvailableSignals.Select(s => s.Name).ToList();
            var existingNames = SelectedPlot.ThresholdRules.Select(r => r.Name).ToList();

            var dialog = new SignalBench.Views.ThresholdRuleDialog {
                DataContext = new ThresholdRuleViewModel(availableFields, existingNames, rule)
            };
            var result = await dialog.ShowDialog<ThresholdRuleResult?>(topLevel);

            if (result != null) {
                if (result.IsDeleted) {
                    await RemoveThresholdRuleAsync(name);
                    return;
                }

                rule.Name = result.Name;
                rule.Formula = result.Formula;
                rule.Color = result.Color;
                rule.IsActive = result.IsActive;
                rule.Description = result.Description;
                
                UpdatePlot(SelectedPlot);
            }
        } catch (Exception ex) { await ShowError("Threshold Error", "Failed to edit threshold rule.", ex); }
    }

    private async Task RemoveThresholdRuleAsync(string name)
    {
        if (SelectedPlot == null) return;
        var rule = SelectedPlot.ThresholdRules.FirstOrDefault(r => r.Name == name);
        if (rule != null)
        {
            SelectedPlot.ThresholdRules.Remove(rule);
            UpdatePlot(SelectedPlot);
        }
        await Task.CompletedTask;
    }
}
