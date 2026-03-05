using ReactiveUI.Avalonia;
using ReactiveUI;
using SignalBench.ViewModels;

namespace SignalBench.Views;

public partial class SchemaEditor : ReactiveWindow<SchemaEditorViewModel>
{
    public SchemaEditor()
    {
        InitializeComponent();
        
        this.WhenActivated(d =>
        {
            if (ViewModel != null)
            {
                ViewModel.SaveCommand.Subscribe(result => Close(result));
                ViewModel.CancelCommand.Subscribe(result => Close(result));
            }
        });
    }
}
