using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.ReactiveUI;
using ReactiveUI;
using SignalBench.ViewModels;
using System.Collections.Specialized;

namespace SignalBench.Views;

public partial class BinaryImport : ReactiveWindow<BinaryImportViewModel>
{
    public BinaryImport()
    {
        InitializeComponent();
        
        this.WhenActivated(d =>
        {
            if (ViewModel != null)
            {
                ViewModel.ImportCommand.Subscribe(result => Close(result));
                ViewModel.CancelCommand.Subscribe(result => Close(result));
                
                // Initial sync
                UpdateColumns(ViewModel.AvailableColumns);

                // Subscribe to future changes
                ViewModel.AvailableColumns.CollectionChanged += (s, args) => UpdateColumns(ViewModel.AvailableColumns);
            }
        });
    }

    private void UpdateColumns(IEnumerable<string> columns)
    {
        var grid = this.FindControl<DataGrid>("PreviewGrid");
        if (grid == null) return;

        grid.Columns.Clear();
        foreach (var col in columns)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = col,
                Binding = new Binding($"[{col}]")
            });
        }
    }
}
