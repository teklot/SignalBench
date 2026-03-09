using Avalonia.Controls;
using SignalBench.ViewModels;

namespace SignalBench.Views;

public partial class NetworkDialog : Window
{
    public NetworkDialogViewModel? ViewModel => DataContext as NetworkDialogViewModel;

    public NetworkDialog()
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
