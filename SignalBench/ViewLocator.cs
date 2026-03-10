using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SignalBench.ViewModels;
using SignalBench.Views;

namespace SignalBench;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        return data switch
        {
            MainWindowViewModel => new MainWindow(),
            PlotViewModel => new PlotView(),
            BinaryFileImportViewModel => new BinaryFileImport(),
            TextFileImportViewModel => new TextFileImport(),
            DerivedSignalViewModel => new DerivedSignalDialog(),

            NetworkDialogViewModel => new NetworkDialog(),
            SchemaEditorViewModel => new SchemaEditor(),
            SerialDialogViewModel => new SerialDialog(),
            SettingsDialogViewModel => new SettingsDialog(),
            SignalStatsViewModel => new SignalStatsView(),
            _ => new TextBlock { Text = "Not Found: " + data.GetType().Name }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}