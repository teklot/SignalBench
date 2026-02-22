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

        double[] x = [.. timestamps.Select(t => t.ToOADate())];

        foreach (var kv in data)
        {
            if (kv.Value.Count != x.Length) continue;
            double[] y = [.. kv.Value];
            var scatter = mainPlot.Plot.Add.Scatter(x, y);
            scatter.LegendText = kv.Key;
        }

        mainPlot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeAutomatic();
        mainPlot.Plot.Axes.AutoScale();
        mainPlot.Refresh();
    }
}
