using Avalonia.ReactiveUI;
using ReactiveUI;
using SignalBench.ViewModels;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Data;
using ScottPlot;

namespace SignalBench.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            if (ViewModel != null)
            {
                ViewModel.RequestPlotUpdate = (ts, data) => UpdatePlot(ts, data);
            }
        });
    }

    public void UpdatePlot(List<DateTime> timestamps, Dictionary<string, List<double>> data)
    {
        MainPlot.Plot.Clear();
        if (timestamps.Count == 0) return;

        double[] x = [.. timestamps.Select(t => t.ToOADate())];

        foreach (var kv in data)
        {
            if (kv.Value.Count != x.Length) continue;
            double[] y = [.. kv.Value];
            var scatter = MainPlot.Plot.Add.Scatter(x, y);
            scatter.LegendText = kv.Key;
        }

        MainPlot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeAutomatic();
        MainPlot.Plot.Axes.AutoScale();
        MainPlot.Refresh();
    }
}
