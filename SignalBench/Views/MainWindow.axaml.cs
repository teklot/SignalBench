using Avalonia.Controls;
using ReactiveUI.Avalonia;
using SignalBench.ViewModels;

namespace SignalBench.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.AutoSaveSession();
        }
        base.OnClosing(e);
    }
}
