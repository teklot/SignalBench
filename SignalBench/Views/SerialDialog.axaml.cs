using Avalonia.Controls;
using SignalBench.ViewModels;

namespace SignalBench.Views;

public partial class SerialDialog : Window
{
    public SerialDialogViewModel? ViewModel => DataContext as SerialDialogViewModel;

    public SerialDialog()
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
