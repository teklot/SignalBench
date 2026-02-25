using Microsoft.Extensions.Logging;
using ReactiveUI;
using SignalBench.Core.Data;
using SignalBench.Core.Decoding;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;
using System.Collections.ObjectModel;
using System.Reactive;

namespace SignalBench.ViewModels;

public class BinaryImportViewModel : ViewModelBase
{
    private PacketSchema? _selectedSchema;
    public PacketSchema? SelectedSchema
    {
        get => _selectedSchema;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSchema, value);
            LoadPreview();
            this.RaisePropertyChanged(nameof(IsImportEnabled));
        }
    }

    private string _schemaFilePath = string.Empty;
    public string SchemaFilePath
    {
        get => _schemaFilePath;
        set => this.RaiseAndSetIfChanged(ref _schemaFilePath, value);
    }

    public ObservableCollection<IDictionary<string, object>> PreviewRecords { get; } = [];
    public ObservableCollection<string> AvailableColumns { get; } = [];

    private readonly string _telemetryPath;
    private readonly ILogger<BinaryImportViewModel> _logger;
    public bool IsImportEnabled => SelectedSchema != null;

    public ReactiveCommand<Unit, PacketSchema?> ImportCommand { get; }
    public ReactiveCommand<Unit, PacketSchema?> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseSchemaCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateSchemaCommand { get; }
    public ReactiveCommand<Unit, Unit> EditSchemaCommand { get; }

    public BinaryImportViewModel(string telemetryPath, ILogger<BinaryImportViewModel> logger)
    {
        _telemetryPath = telemetryPath;
        _logger = logger;

        ImportCommand = ReactiveCommand.Create(() => SelectedSchema);
        CancelCommand = ReactiveCommand.Create(() => (PacketSchema?)null);
        BrowseSchemaCommand = ReactiveCommand.CreateFromTask(BrowseSchemaAsync);
        CreateSchemaCommand = ReactiveCommand.CreateFromTask(CreateSchemaAsync);
        var canEditSchema = this.WhenAnyValue(x => x.SelectedSchema, (PacketSchema? s) => s != null);
        EditSchemaCommand = ReactiveCommand.CreateFromTask(EditSchemaAsync, canEditSchema);
    }

    private async Task ShowError(string title, string message, Exception? ex = null)
    {
        if (ex != null)
        {
            _logger.LogError(ex, "{Title}: {Message}", title, message);
        }
        else
        {
            _logger.LogError("{Title}: {Message}", title, message);
        }

        var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(title, message);

        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel != null)
        {
            await box.ShowWindowDialogAsync(topLevel);
        }
    }

    private async Task EditSchemaAsync()
    {
        try
        {
            if (SelectedSchema == null) return;

            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var dialog = new SignalBench.Views.SchemaEditor
            {
                DataContext = new SchemaEditorViewModel(SelectedSchema)
            };

            var result = await dialog.ShowDialog<SchemaEditorResult?>(topLevel);
            if (result != null)
            {
                SelectedSchema = result.Schema;
                if (!string.IsNullOrEmpty(result.FilePath))
                {
                    SchemaFilePath = result.FilePath;
                }
            }
        }
        catch (Exception ex)
        {
            await ShowError("Editor Error", "Failed to open schema editor.", ex);
        }
    }

    private async Task CreateSchemaAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var dialog = new SignalBench.Views.SchemaEditor
            {
                DataContext = new SchemaEditorViewModel()
            };

            var result = await dialog.ShowDialog<SchemaEditorResult?>(topLevel);
            if (result != null)
            {
                SelectedSchema = result.Schema;
                SchemaFilePath = string.IsNullOrEmpty(result.FilePath)
                    ? "New Schema (unsaved)"
                    : result.FilePath;
            }
        }
        catch (Exception ex)
        {
            await ShowError("Editor Error", "Failed to open schema editor.", ex);
        }
    }

    private async Task BrowseSchemaAsync()
    {
        try
        {
            var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Schema File",
                AllowMultiple = false,
                FileTypeFilter = [
                    new Avalonia.Platform.Storage.FilePickerFileType("YAML Schema") { Patterns = ["*.yaml", "*.yml"] }
                ]
            });

            if (files.Count > 0)
            {
                SchemaFilePath = files[0].Path.LocalPath;
                try
                {
                    var yaml = await File.ReadAllTextAsync(SchemaFilePath);
                    var loader = new SchemaLoader();
                    SelectedSchema = loader.Load(yaml);
                }
                catch (Exception ex)
                {
                    await ShowError("Schema Error", "Failed to load the selected schema file.", ex);
                }
            }
        }
        catch (Exception ex)
        {
            await ShowError("File Error", "Could not select schema file.", ex);
        }
    }

    private void LoadPreview()
    {
        PreviewRecords.Clear();
        AvailableColumns.Clear();

        if (SelectedSchema == null) return;

        try
        {
            var source = new SignalBench.Core.Ingestion.BinaryTelemetrySource(_telemetryPath, SelectedSchema);
            var packets = source.ReadPackets().Take(50).ToList();

            if (packets.Count > 0)
            {
                foreach (var field in SelectedSchema.Fields)
                {
                    AvailableColumns.Add(field.Name);
                }

                foreach (var p in packets)
                {
                    PreviewRecords.Add(p.Fields);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Binary Preview Error");
        }
    }
}
