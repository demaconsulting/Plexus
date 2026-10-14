using System.Text.Json;
using System.Text.Json.Serialization;

namespace DemaConsulting.Plexus;

/// <summary>
///     One property value in a snapshot: the value's kind plus its JSON-safe value.
/// </summary>
/// <remarks>
///     Storing the kind next to the value makes round trips exact (an <c>int</c> stays an <c>int</c>,
///     a <c>long</c> stays a <c>long</c>) and lets loaders detect a mismatch with the target property
///     definition. Serializes as <c>{"kind": "double", "value": 1.5}</c>; colors are strings of the
///     form <c>"#AARRGGBB"</c>. Note that <see cref="JsonElement"/> has reference equality, so compare
///     snapshots through their JSON text rather than with record equality.
/// </remarks>
public sealed record PropertySnapshot
{
    /// <summary>
    ///     Gets the kind of the value.
    /// </summary>
    [JsonConverter(typeof(PropertyKindJsonConverter))]
    public required PropertyValueKind Kind { get; init; }

    /// <summary>
    ///     Gets the value as JSON: a boolean, number or string matching <see cref="Kind"/>.
    /// </summary>
    public required JsonElement Value { get; init; }
}
