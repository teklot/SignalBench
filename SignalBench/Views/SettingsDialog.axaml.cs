using Avalonia.Controls;
using SignalBench.ViewModels;

namespace SignalBench.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialogViewModel? ViewModel => DataContext as SettingsDialogViewModel;

    public SettingsDialog()
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
