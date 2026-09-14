using System.Text.Json.Serialization;
using MicaFlyouts.Domain.Settings;

namespace MicaFlyouts.Infrastructure.Settings;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(SettingsSnapshot))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
