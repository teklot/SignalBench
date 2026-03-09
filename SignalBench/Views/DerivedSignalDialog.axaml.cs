using Avalonia.Controls;
using SignalBench.ViewModels;

namespace SignalBench.Views;

public partial class DerivedSignalDialog : Window
{
    public DerivedSignalViewModel? ViewModel => DataContext as DerivedSignalViewModel;

    public DerivedSignalDialog()
    {
        InitializeComponent();
        
        DataContextChanged += (s, e) =>
        {
            if (ViewModel != null)
            {
                ViewModel.RequestClose += result => Close(result);
            }
        };
    }
}
