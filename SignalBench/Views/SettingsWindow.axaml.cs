using Avalonia.Controls;
using SignalBench.ViewModels;

namespace SignalBench.Views;

public partial class SettingsWindow : Window
{
    public SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    public SettingsWindow()
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
