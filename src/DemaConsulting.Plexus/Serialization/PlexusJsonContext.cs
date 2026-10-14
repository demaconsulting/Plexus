using System.Text.Json.Serialization;

namespace DemaConsulting.Plexus;

/// <summary>
///     Source-generated System.Text.Json metadata for the snapshot and clipboard shapes.
/// </summary>
/// <remarks>
///     Using generated metadata means serialization is driven only by the types listed here: no type
///     names are ever read from or written to the JSON and nothing is resolved by reflection, which
///     keeps untrusted clipboard input from selecting types. The naming policy and null handling are
///     declared here so every context instance produces the same shape; indentation and depth limits
///     are supplied per instance by <see cref="PlexusJson"/>.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DiagramSnapshot))]
[JsonSerializable(typeof(ClipboardEnvelope))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string))]
internal sealed partial class PlexusJsonContext : JsonSerializerContext
{
}
