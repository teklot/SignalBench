using CommunityToolkit.Mvvm.ComponentModel;
using SignalBench.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SignalBench.ViewModels;

public partial class SignalStatsViewModel : ViewModelBase
{
    private readonly IDataStore? _dataStore;
    private double _lastMinX;
    private double _lastMaxX;

    [ObservableProperty]
    private SignalItemViewModel? _selectedSignal;

    [ObservableProperty]
    private double? _min;

    [ObservableProperty]
    private double? _max;

    [ObservableProperty]
    private double? _mean;

    [ObservableProperty]
    private double? _std;

    [ObservableProperty]
    private double? _rms;

    [ObservableProperty]
    private double? _p2p;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _useSelectedWindow;

    public SignalStatsViewModel() { }

    public SignalStatsViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    partial void OnSelectedSignalChanged(SignalItemViewModel? value)
    {
        Calculate();
    }

    partial void OnUseSelectedWindowChanged(bool value)
    {
        Calculate();
    }

    public void SetWindow(double minX, double maxX)
    {
        _lastMinX = minX;
        _lastMaxX = maxX;
        Calculate();
    }

    private void Calculate()
    {
        if (_dataStore == null || SelectedSignal == null)
        {
            Reset();
            return;
        }

        List<double> data;
        if (UseSelectedWindow && _lastMaxX > _lastMinX)
        {
            var start = DateTime.FromOADate(_lastMinX);
            var end = DateTime.FromOADate(_lastMaxX);
            var indices = _dataStore.GetIndices(start, end);
            
            if (indices.start < 0 || indices.end < 0)
            {
                Reset();
                return;
            }
            
            data = _dataStore.GetSignalData(SelectedSignal.Name, indices.start, (indices.end - indices.start) + 1);
        }
        else
        {
            data = _dataStore.GetSignalData(SelectedSignal.Name);
        }

        if (data == null || data.Count == 0)
        {
            Reset();
            return;
        }

        // Filter out NaNs for stats
        var validData = data.Where(d => !double.IsNaN(d)).ToList();
        if (validData.Count == 0)
        {
            Reset();
            return;
        }

        Count = validData.Count;
        double minVal = validData.Min();
        double maxVal = validData.Max();
        Min = minVal;
        Max = maxVal;
        P2p = maxVal - minVal;
        
        double meanVal = validData.Average();
        Mean = meanVal;
        
        double sumOfSquares = validData.Sum(d => d * d);
        Rms = Math.Sqrt(sumOfSquares / validData.Count);

        double sumOfDerivations = validData.Sum(d => Math.Pow(d - meanVal, 2));
        Std = Math.Sqrt(sumOfDerivations / validData.Count);
    }

    private void Reset()
    {
        Min = null;
        Max = null;
        Mean = null;
        Std = null;
        Rms = null;
        P2p = null;
        Count = 0;
    }
}
