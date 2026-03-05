using Avalonia.Controls;
using Avalonia.Data;
using ReactiveUI.Avalonia;
using ReactiveUI;
using SignalBench.ViewModels;
using System.Collections.Specialized;

namespace SignalBench.Views;

public partial class CsvImport : ReactiveWindow<CsvImportViewModel>
{
    public CsvImport()
    {
        InitializeComponent();
        
        this.WhenActivated(d =>
        {
            if (ViewModel != null)
            {
                ViewModel.ImportCommand.Subscribe(result => Close(result));
                ViewModel.CancelCommand.Subscribe(result => Close(result));
                ViewModel.PreviewRecords.CollectionChanged += OnPreviewRecordsChanged;
                
                // Initial update if already loaded
                if (ViewModel.PreviewRecords.Count > 0)
                    UpdateColumns();
            }
        });
    }

    private void OnPreviewRecordsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset || (e.Action == NotifyCollectionChangedAction.Add && ViewModel?.PreviewRecords.Count == 1))
        {
            UpdateColumns();
        }
    }

    private void UpdateColumns()
    {
        var grid = this.FindControl<DataGrid>("PreviewGrid");
        if (grid == null || ViewModel == null) return;

        grid.Columns.Clear();
        if (ViewModel.PreviewRecords.Count > 0)
        {
            var firstRecord = ViewModel.PreviewRecords[0];
            foreach (var key in firstRecord.Keys)
            {
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = key,
                    Binding = new Binding($"[{key}]")
                });
            }
        }
    }
}
