using ReactiveUI.Avalonia;
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
                ViewModel.SaveCommand.Subscribe(success => { if (success) Close(true); });
                ViewModel.CancelCommand.Subscribe(_ => Close(false));
            }
        });
    }
}
