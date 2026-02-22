using ReactiveUI;
using SignalBench.Core.Models.Schema;
using SignalBench.Core.Services;
using System.Collections.ObjectModel;
using System.Reactive;

namespace SignalBench.ViewModels;

public class FieldEditorViewModel : ViewModelBase
{
    private string _name = "NewField";
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private FieldType _type = FieldType.Uint16;
    public FieldType Type
    {
        get => _type;
        set => this.RaiseAndSetIfChanged(ref _type, value);
    }

    private int _bitOffset;
    public int BitOffset
    {
        get => _bitOffset;
        set => this.RaiseAndSetIfChanged(ref _bitOffset, value);
    }

    private int _bitLength;
    public int BitLength
    {
        get => _bitLength;
        set => this.RaiseAndSetIfChanged(ref _bitLength, value);
    }

    public FieldEditorViewModel() { }

    public FieldEditorViewModel(FieldDefinition field)
    {
        Name = field.Name;
        Type = field.Type;
        BitOffset = field.BitOffset;
        BitLength = field.BitLength;
    }

    public FieldDefinition ToDefinition() => new()
    {
        Name = Name,
        Type = Type,
        BitOffset = BitOffset,
        BitLength = BitLength
    };
}

public class SchemaEditorResult
{
    public PacketSchema Schema { get; set; } = null!;
    public string? FilePath { get; set; }
}

public class SchemaEditorViewModel : ViewModelBase
{
    private string _name = "New Schema";
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private string? _lastSavedPath;
    public string? LastSavedPath
    {
        get => _lastSavedPath;
        set => this.RaiseAndSetIfChanged(ref _lastSavedPath, value);
    }

    private Endianness _endianness = Endianness.Little;
    public Endianness Endianness
    {
        get => _endianness;
        set => this.RaiseAndSetIfChanged(ref _endianness, value);
    }

    public ObservableCollection<FieldEditorViewModel> Fields { get; } = [];

    public ReactiveCommand<Unit, Unit> AddFieldCommand { get; }
    public ReactiveCommand<FieldEditorViewModel, Unit> RemoveFieldCommand { get; }
    public ReactiveCommand<Unit, SchemaEditorResult?> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveToFileCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFromFileCommand { get; }
    public ReactiveCommand<Unit, SchemaEditorResult?> CancelCommand { get; }

    public FieldType[] AvailableTypes { get; } = (FieldType[])Enum.GetValues(typeof(FieldType));
    public Endianness[] AvailableEndianness { get; } = (Endianness[])Enum.GetValues(typeof(Endianness));

    public SchemaEditorViewModel(PacketSchema? existingSchema = null)
    {
        if (existingSchema != null)
        {
            LoadFromSchema(existingSchema);
        }

        AddFieldCommand = ReactiveCommand.Create(AddField);
        RemoveFieldCommand = ReactiveCommand.Create<FieldEditorViewModel>(f => Fields.Remove(f));
        SaveToFileCommand = ReactiveCommand.CreateFromTask(SaveToFileAsync);
        OpenFromFileCommand = ReactiveCommand.CreateFromTask(OpenFromFileAsync);
        
        SaveCommand = ReactiveCommand.Create<SchemaEditorResult?>(() => {
            return new SchemaEditorResult 
            { 
                Schema = BuildSchema(),
                FilePath = LastSavedPath 
            };
        });

        CancelCommand = ReactiveCommand.Create(() => (SchemaEditorResult?)null);
    }

    private void LoadFromSchema(PacketSchema schema)
    {
        Name = schema.Name;
        Endianness = schema.Endianness;
        Fields.Clear();
        foreach (var f in schema.Fields)
            Fields.Add(new FieldEditorViewModel(f));
    }

    private PacketSchema BuildSchema()
    {
        return new PacketSchema
        {
            Name = Name,
            Endianness = Endianness,
            Fields = Fields.Select(f => f.ToDefinition()).ToList()
        };
    }

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

    private async Task SaveToFileAsync()
    {
        var topLevel = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (topLevel == null) return;

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
        }
    }

    private void AddField()
    {
        int nextOffset = 0;
        if (Fields.Count > 0)
        {
            var last = Fields[^1];
            int size = GetTypeBitCount(last.Type);
            nextOffset = last.BitOffset + (last.BitLength > 0 ? last.BitLength : size);
        }

        Fields.Add(new FieldEditorViewModel 
        { 
            Name = $"Field_{Fields.Count + 1}",
            BitOffset = nextOffset 
        });
    }

    private int GetTypeBitCount(FieldType type) => type switch
    {
        FieldType.Uint8 or FieldType.Int8 => 8,
        FieldType.Uint16 or FieldType.Int16 => 16,
        FieldType.Uint32 or FieldType.Int32 or FieldType.Float32 => 32,
        FieldType.Uint64 or FieldType.Float64 => 64,
        _ => 0
    };
}
