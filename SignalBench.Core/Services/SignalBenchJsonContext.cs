using System.Text.Json.Serialization;
using SignalBench.Core.Models;

namespace SignalBench.Core.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UserSettings))]
public partial class SignalBenchJsonContext : JsonSerializerContext
{
}
