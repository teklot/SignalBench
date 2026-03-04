using Avalonia.Controls;
using Avalonia.ReactiveUI;
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
