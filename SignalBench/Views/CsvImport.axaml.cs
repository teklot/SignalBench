using Avalonia.Controls;
using Avalonia.Data;
using SignalBench.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace SignalBench.Views;

public partial class CsvImport : Window
{
    public CsvImportViewModel? ViewModel => DataContext as CsvImportViewModel;

    public CsvImport()
    {
        InitializeComponent();
        
        DataContextChanged += (s, e) =>
        {
            if (ViewModel != null)
            {
                ViewModel.RequestClose += result => Close(result);
                
                // Initial sync
                UpdateColumns(ViewModel.AvailableColumns);

                // Subscribe to future changes
                ViewModel.AvailableColumns.CollectionChanged += (s, args) => UpdateColumns(ViewModel.AvailableColumns);
            }
        };
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
