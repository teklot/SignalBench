using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace SignalBench.ViewModels;

public partial class FieldEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = "NewField";

    [ObservableProperty]
    private FieldType _type = FieldType.Uint16;

    [ObservableProperty]
    private int _bitOffset;

    [ObservableProperty]
    private int _bitLength;

    [ObservableProperty]
    private double _scale = 1.0;

    [ObservableProperty]
    private double _offset = 0.0;

    [ObservableProperty]
    private string? _unit;

    [ObservableProperty]
    private string? _description;

    public FieldEditorViewModel() { }

    public FieldEditorViewModel(FieldDefinition field)
    {
        Name = field.Name;
        Type = field.Type;
        BitOffset = field.BitOffset;
        BitLength = field.BitLength;
        Scale = field.Scale;
        Offset = field.Offset;
        Unit = field.Unit;
        Description = field.Description;
    }

    public FieldDefinition ToDefinition() => new()
    {
        Name = Name,
        Type = Type,
        BitOffset = BitOffset,
        BitLength = BitLength,
        Scale = Scale,
        Offset = Offset,
        Unit = Unit,
        Description = Description
    };
}

public class SchemaEditorResult
{
    public PacketSchema Schema { get; set; } = null!;
    public string? FilePath { get; set; }
}

public partial class SchemaEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = "New Schema";

    [ObservableProperty]
    private string? _lastSavedPath;

    [ObservableProperty]
    private string? _syncWordHex;

    partial void OnSyncWordHexChanged(string? value)
    {
        if (value == null) return;
        var sanitized = new string(value.Where(c => "0123456789ABCDEFabcdef".Contains(c)).ToArray()).ToUpper();
        if (sanitized != value) SyncWordHex = sanitized;
    }

    [ObservableProperty]
    private Endianness _endianness = Endianness.Little;

    [ObservableProperty]
    private FieldEditorViewModel? _selectedField;

    public ObservableCollection<FieldEditorViewModel> Fields { get; } = [];

    public event Action<SchemaEditorResult?>? RequestClose;

    [RelayCommand]
    private void AddField()
    {
        int nextOffset = 0;
        if (Fields.Count > 0)
        {
            var last = Fields[^1];
            int size = GetTypeBitCount(last.Type);
            nextOffset = last.BitOffset + (last.BitLength > 0 ? last.BitLength : size);
        }

        var newField = new FieldEditorViewModel 
        { 
            Name = $"Field_{Fields.Count + 1}",
            BitOffset = nextOffset 
        };
        Fields.Add(newField);
        SelectedField = newField;
        SaveAndCloseCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveField(FieldEditorViewModel field) {
        if (SelectedField == field) SelectedField = null;
        Fields.Remove(field);
        SaveAndCloseCommand.NotifyCanExecuteChanged();
    }

    private bool CanSave => Fields.Count > 0;

    [RelayCommand]
    private async Task<bool> SaveToFileAsync()
    {
        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel == null) return false;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save Schema File",
            DefaultExtension = "yaml",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("YAML Schema") { Patterns = ["*.yaml", "*.yml"] }]
        });

        if (file != null)
        {
            var schema = BuildSchema();
            var loader = new SchemaLoader();
            var yaml = loader.Save(schema);
            await File.WriteAllTextAsync(file.Path.LocalPath, yaml);
            LastSavedPath = file.Path.LocalPath;
            return true;
        }
        return false;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAndCloseAsync()
    {
        if (await SaveToFileAsync())
        {
            RequestClose?.Invoke(new SchemaEditorResult 
            { 
                Schema = BuildSchema(),
                FilePath = LastSavedPath 
            });
        }
    }

    [RelayCommand]
    private async Task OpenFromFileAsync()
    {
        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Schema File",
            AllowMultiple = false,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("YAML Schema") { Patterns = ["*.yaml", "*.yml"] }]
        });

        if (files.Count > 0)
        {
            var yaml = await File.ReadAllTextAsync(files[0].Path.LocalPath);
            var schema = new SchemaLoader().Load(yaml);
            LoadFromSchema(schema);
            LastSavedPath = files[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(null);

    public FieldType[] AvailableTypes { get; } = Enum.GetValues<FieldType>();
    public Endianness[] AvailableEndianness { get; } = Enum.GetValues<Endianness>();

    public SchemaEditorViewModel(PacketSchema? existingSchema = null)
    {
        if (existingSchema != null)
        {
            LoadFromSchema(existingSchema);
        }
    }

    private void LoadFromSchema(PacketSchema schema)
    {
        Name = schema.Name;
        Endianness = schema.Endianness;
        SyncWordHex = schema.SyncWord?.ToString("X") ?? "";
        Fields.Clear();
        foreach (var f in schema.Fields)
            Fields.Add(new FieldEditorViewModel(f));
        
        if (Fields.Count > 0) SelectedField = Fields[0];
        SaveAndCloseCommand.NotifyCanExecuteChanged();
    }

    private PacketSchema BuildSchema()
    {
        uint? syncWord = null;
        if (!string.IsNullOrEmpty(SyncWordHex))
        {
            if (uint.TryParse(SyncWordHex, System.Globalization.NumberStyles.HexNumber, null, out uint val))
                syncWord = val;
        }

        return new PacketSchema
        {
            Name = Name,
            SyncWord = syncWord,
            Endianness = Endianness,
            Fields = Fields.Select(f => f.ToDefinition()).ToList()
        };
    }

    private int GetTypeBitCount(FieldType type) => type switch
    {
        FieldType.Uint8 or FieldType.Int8 or FieldType.Bool => 8,
        FieldType.Uint16 or FieldType.Int16 => 16,
        FieldType.Uint32 or FieldType.Int32 or FieldType.Float32 => 32,
        FieldType.Uint64 or FieldType.Int64 or FieldType.Float64 => 64,
        _ => 0
    };
}
