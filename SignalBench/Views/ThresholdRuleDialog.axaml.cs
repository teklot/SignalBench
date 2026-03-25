using Avalonia.Controls;
using SignalBench.ViewModels;
using System;

namespace SignalBench.Views;

public partial class ThresholdRuleDialog : Window
{
    public ThresholdRuleDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ThresholdRuleViewModel vm)
        {
            vm.RequestClose += result => Close(result);
        }
    }
}
