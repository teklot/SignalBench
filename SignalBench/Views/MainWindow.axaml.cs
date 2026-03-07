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

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (ViewModel != null)
        {
            var settings = ViewModel.SettingsService.Current;
            
            // Restore dimensions
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;

            // Restore position if available
            if (settings.WindowX.HasValue && settings.WindowY.HasValue)
            {
                Position = new Avalonia.PixelPoint(settings.WindowX.Value, settings.WindowY.Value);
            }

            // Restore state (Maximized/Normal)
            if (Enum.TryParse<WindowState>(settings.WindowState, out var state))
            {
                WindowState = state;
            }
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (ViewModel != null)
        {
            var settings = ViewModel.SettingsService.Current;
            
            // Save state
            settings.WindowState = WindowState.ToString();
            
            // Only save dimensions if not minimized/maximized to avoid saving corrupted sizes
            if (WindowState == WindowState.Normal)
            {
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
                settings.WindowX = Position.X;
                settings.WindowY = Position.Y;
            }
            
            ViewModel.SettingsService.Save();
            ViewModel.AutoSaveSession();
        }
        base.OnClosing(e);
    }
}
