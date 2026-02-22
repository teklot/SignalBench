using Avalonia.ReactiveUI;
using SignalBench.ViewModels;

namespace SignalBench.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
