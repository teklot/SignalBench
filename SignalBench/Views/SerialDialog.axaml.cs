using ReactiveUI.Avalonia;
using ReactiveUI;
using SignalBench.ViewModels;
using System;

namespace SignalBench.Views;

public partial class SerialDialog : ReactiveWindow<SerialDialogViewModel>
{
    public SerialDialog()
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
