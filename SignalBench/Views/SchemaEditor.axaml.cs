using Avalonia.Controls;
using SignalBench.ViewModels;

namespace SignalBench.Views;

public partial class SchemaEditor : Window
{
    public SchemaEditorViewModel? ViewModel => DataContext as SchemaEditorViewModel;

    public SchemaEditor()
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
