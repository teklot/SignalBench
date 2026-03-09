using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using SignalBench.Core.Data;

namespace SignalBench.ViewModels;

public class SignalStatsViewModel : ViewModelBase
{
    private readonly IDataStore _dataStore;
    private SignalItemViewModel? _selectedSignal;
    private bool _useSelectedWindow;
    
    private double _min;
    private double _max;
    private double _mean;
    private double _stdDev;
    private double _rms;
    private double _p2p;

    public SignalItemViewModel? SelectedSignal
    {
        get => _selectedSignal;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSignal, value);
            UpdateStatistics();
        }
    }

    public string? SelectedSignalName => SelectedSignal?.Name;

    public bool UseSelectedWindow
    {
        get => _useSelectedWindow;
        set
        {
            this.RaiseAndSetIfChanged(ref _useSelectedWindow, value);
            UpdateStatistics();
        }
    }

    public double Min { get => _min; private set => this.RaiseAndSetIfChanged(ref _min, value); }
    public double Max { get => _max; private set => this.RaiseAndSetIfChanged(ref _max, value); }
    public double Mean { get => _mean; private set => this.RaiseAndSetIfChanged(ref _mean, value); }
    public double StdDev { get => _stdDev; private set => this.RaiseAndSetIfChanged(ref _stdDev, value); }
    public double RMS { get => _rms; private set => this.RaiseAndSetIfChanged(ref _rms, value); }
    public double P2P { get => _p2p; private set => this.RaiseAndSetIfChanged(ref _p2p, value); }

    // Store window range
    private double? _windowMin;
    private double? _windowMax;

    public SignalStatsViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public void SetWindow(double min, double max)
    {
        _windowMin = min;
        _windowMax = max;
        if (UseSelectedWindow)
        {
            UpdateStatistics();
        }
    }

    public void UpdateStatistics()
    {
        if (string.IsNullOrEmpty(SelectedSignalName))
        {
            ResetStats();
            return;
        }

        List<double> data;
        if (UseSelectedWindow && _windowMin.HasValue && _windowMax.HasValue)
        {
            // We need timestamps to filter by window if window is in time
            // However, ScottPlot 5 window is usually in axis units (double)
            // For SignalBench, X axis is usually DateTime.ToOADate()
            
            var allData = _dataStore.GetSignalData(SelectedSignalName);
            var timestamps = _dataStore.GetTimestamps();
            
            if (allData.Count == 0 || allData.Count != timestamps.Count)
            {
                ResetStats();
                return;
            }

            data = new List<double>();
            for (int i = 0; i < timestamps.Count; i++)
            {
                double x = timestamps[i].ToOADate();
                if (x >= _windowMin.Value && x <= _windowMax.Value)
                {
                    data.Add(allData[i]);
                }
            }
        }
        else
        {
            data = _dataStore.GetSignalData(SelectedSignalName);
        }

        if (data == null || data.Count == 0)
        {
            ResetStats();
            return;
        }

        Min = data.Min();
        Max = data.Max();
        Mean = data.Average();
        P2P = Max - Min;

        double sumOfSquares = data.Sum(x => x * x);
        RMS = Math.Sqrt(sumOfSquares / data.Count);

        double sumOfDerivations = data.Sum(x => Math.Pow(x - Mean, 2));
        StdDev = Math.Sqrt(sumOfDerivations / data.Count);
    }

    private void ResetStats()
    {
        Min = 0;
        Max = 0;
        Mean = 0;
        StdDev = 0;
        RMS = 0;
        P2P = 0;
    }
}
