using System.Text.Json.Serialization;

namespace DemaConsulting.Plexus;

/// <summary>
///     The persisted state of one <see cref="Node"/>.
/// </summary>
/// <remarks>
///     Wires reference nodes by <see cref="Id"/>. The host payload (<see cref="Data"/>) is embedded as
///     raw JSON text produced by <see cref="NodeType.DataType"/> or <see cref="NodeType.DataSerializer"/>;
///     it is absent when the node has no payload or its type declares no way to serialize one.
/// </remarks>
public sealed record NodeSnapshot
{
    /// <summary>
    ///     Gets the element id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     Gets the key of the node type.
    /// </summary>
    public required string TypeKey { get; init; }

    /// <summary>
    ///     Gets the node name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets the bounding box.
    /// </summary>
    public required RectSnapshot Bounds { get; init; }

    /// <summary>
    ///     Gets the property values by name: declared properties first (declaration order), then ad-hoc ones.
    /// </summary>
    public IReadOnlyDictionary<string, PropertySnapshot> Properties { get; init; } = new Dictionary<string, PropertySnapshot>();

    /// <summary>
    ///     Gets the host payload as JSON text, or null when there is none.
    /// </summary>
    /// <remarks>
    ///     Written as an embedded JSON value (not as an escaped string) so the document stays readable.
    /// </remarks>
    [JsonConverter(typeof(RawJsonStringConverter))]
    public string? Data { get; init; }

    /// <summary>
    ///     Gets the live payload object carried between <see cref="DiagramPage.Duplicate"/> and the importer
    ///     for node types that cannot serialize their payload; never serialized.
    /// </summary>
    [JsonIgnore]
    internal object? DataReference { get; init; }
}
