using Avalonia.ReactiveUI;
using ReactiveUI;
using SignalBench.ViewModels;

namespace SignalBench.Views;

public partial class SettingsWindow : ReactiveWindow<SettingsViewModel>
{
    public SettingsWindow()
    {
        InitializeComponent();
        
        this.WhenActivated(d =>
        {
            if (ViewModel != null)
            {
                ViewModel.SaveCommand.Subscribe(_ => Close());
                ViewModel.CancelCommand.Subscribe(_ => Close());
            }
        });
    }
}
