using Avalonia.ReactiveUI;
using ReactiveUI;
using SignalBench.ViewModels;

namespace SignalBench.Views;

public partial class DerivedSignalDialog : ReactiveWindow<DerivedSignalViewModel>
{
    public DerivedSignalDialog()
    {
        InitializeComponent();
        
        this.WhenActivated(d =>
        {
            if (ViewModel != null)
            {
                ViewModel.AddCommand.Subscribe(result => Close(result));
                ViewModel.CancelCommand.Subscribe(result => Close(result));
            }
        });
    }
}
