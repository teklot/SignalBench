using Avalonia.ReactiveUI;
using ReactiveUI;
using SignalBench.ViewModels;
using System;

namespace SignalBench.Views;

public partial class NetworkDialog : ReactiveWindow<NetworkDialogViewModel>
{
    public NetworkDialog()
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
