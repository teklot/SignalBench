using Avalonia;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace SignalBench;

sealed class Program
{
    public static string AppDirectory { get; } = AppContext.BaseDirectory;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            ShowError("Critical Unhandled Error", ex?.Message ?? "Unknown Error", ex?.ToString());
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            ShowError("Unobserved Task Error", e.Exception.Message, e.Exception.ToString());
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            ShowError("Application Crash", ex.Message, ex.ToString());
        }
    }

    private static void ShowError(string title, string message, string? detail = null)
    {
        // Log the detail for debugging/diagnostics, but do not show to end user
        System.Diagnostics.Debug.WriteLine($"[CRITICAL ERROR] {title}: {message}\n{detail}");

        var box = MessageBoxManager.GetMessageBoxStandard(title, message);
            
        // If we are already on UI thread, we can show it, otherwise we might need to use Post
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            box.ShowAsync();
        }
        else
        {
            // Try to show it on UI thread if possible
            Avalonia.Threading.Dispatcher.UIThread.Post(() => box.ShowAsync());
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
