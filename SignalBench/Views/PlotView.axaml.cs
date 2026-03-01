using Avalonia.Controls;
using Avalonia.Media;
using ScottPlot;
using ScottPlot.Plottables;
using SignalBench.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SignalBench.Views;

public partial class PlotView : UserControl
{
    private VerticalLine? _cursorLine;

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
            vm.RequestCursorUpdate = UpdateCursorOnly;
        }
    }

    public void UpdatePlot(List<DateTime> timestamps, Dictionary<string, List<double>> data, DateTime? cursorPosition = null, double? fixedXMax = null, int? rollingWindowSize = null)
    {
        var mainPlot = this.FindControl<ScottPlot.Avalonia.AvaPlot>("MainPlot");
        if (mainPlot == null) return;

        mainPlot.Plot.Clear();
        _cursorLine = null;

        if (timestamps.Count == 0) 
        {
            mainPlot.Plot.Axes.Bottom.Min = 0;
            mainPlot.Plot.Axes.Bottom.Max = 1;
            mainPlot.Plot.Axes.Left.Min = -10;
            mainPlot.Plot.Axes.Left.Max = 10;
            mainPlot.Plot.Axes.NumericTicksBottom();
            mainPlot.Refresh();
            return;
        }

        double minY = double.MaxValue;
        double maxY = double.MinValue;

        foreach (var kv in data)
        {
            if (kv.Value.Count == 0 || kv.Value.Count != timestamps.Count) continue;
            
            double[] x = timestamps.Select(t => t.ToOADate()).ToArray();
            double[] y = [.. kv.Value];
            
            var yMin = y.Min();
            var yMax = y.Max();
            if (yMin < minY) minY = yMin;
            if (yMax > maxY) maxY = yMax;
            
            var scatter = mainPlot.Plot.Add.Scatter(x, y);
            scatter.LegendText = kv.Key;
            scatter.MarkerSize = 0;
            scatter.LineWidth = 1;
        }

        if (timestamps.Count > 0)
        {
            // Simple filling from left: Axis fits current buffer exactly
            mainPlot.Plot.Axes.Bottom.Min = timestamps[0].ToOADate();
            mainPlot.Plot.Axes.Bottom.Max = timestamps[^1].ToOADate();
            
            // Add a tiny bit of space if only 1 point to prevent zero-width axis
            if (Math.Abs(mainPlot.Plot.Axes.Bottom.Max - mainPlot.Plot.Axes.Bottom.Min) < 0.000001)
            {
                mainPlot.Plot.Axes.Bottom.Max = mainPlot.Plot.Axes.Bottom.Min + 0.00001;
            }

            mainPlot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeAutomatic();
        }

        if (minY != double.MaxValue && maxY != double.MinValue)
        {
            var range = maxY - minY;
            if (range < 0.000001) range = 1.0; 
            
            var yPadding = range * 0.1;
            mainPlot.Plot.Axes.Left.Min = minY - yPadding;
            mainPlot.Plot.Axes.Left.Max = maxY + yPadding;
        }

        if (cursorPosition.HasValue)
        {
            AddOrUpdateCursor(mainPlot, cursorPosition.Value);
        }

        mainPlot.Refresh();
    }

    private void AddOrUpdateCursor(ScottPlot.Avalonia.AvaPlot mainPlot, DateTime cursorPosition)
    {
        var cursorDate = cursorPosition.ToOADate();
        
        // Check if the line is still in the plot's plottables (in case it was cleared externally)
        if (_cursorLine == null || !mainPlot.Plot.GetPlottables().Contains(_cursorLine))
        {
            _cursorLine = mainPlot.Plot.Add.VerticalLine(cursorDate);
            _cursorLine.Color = ScottPlot.Colors.Red;
            _cursorLine.LineWidth = 2; // Made slightly thicker for better visibility
        }
        else
        {
            _cursorLine.X = cursorDate;
        }
    }

    public void UpdateCursorOnly(DateTime? cursorPosition)
    {
        var mainPlot = this.FindControl<ScottPlot.Avalonia.AvaPlot>("MainPlot");
        if (mainPlot == null || !cursorPosition.HasValue) return;

        AddOrUpdateCursor(mainPlot, cursorPosition.Value);
        mainPlot.Refresh();
    }
}
