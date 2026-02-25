using Avalonia.Controls;
using ScottPlot;
using SignalBench.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SignalBench.Views;

public partial class PlotView : UserControl
{
    public PlotView()
    {
        InitializeComponent();
        DataContextChanged += PlotView_DataContextChanged;
    }

    private void PlotView_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RequestPlotUpdate = UpdatePlot;
        }
    }

    public void UpdatePlot(List<DateTime> timestamps, Dictionary<string, List<double>> data)
    {
        var mainPlot = this.FindControl<ScottPlot.Avalonia.AvaPlot>("MainPlot");
        if (mainPlot == null) return;

        mainPlot.Plot.Clear();
        if (timestamps.Count == 0) 
        {
            mainPlot.Refresh();
            return;
        }

        foreach (var kv in data)
        {
            if (kv.Value.Count == 0) continue;
            
            double[] y = [.. kv.Value];
            var signal = mainPlot.Plot.Add.Signal(y);
            signal.LegendText = kv.Key;
        }

        if (timestamps.Count > 0)
        {
            mainPlot.Plot.Axes.Bottom.Min = timestamps[0].ToOADate();
            mainPlot.Plot.Axes.Bottom.Max = timestamps[^1].ToOADate();
            mainPlot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeAutomatic();
        }

        mainPlot.Plot.Axes.AutoScale();
        mainPlot.Refresh();
    }
}
