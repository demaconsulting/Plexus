using System.Text.Json.Serialization;

namespace DemaConsulting.Plexus;

/// <summary>
///     The JSON document placed on the clipboard: a format marker and version plus the copied elements.
/// </summary>
/// <remarks>
///     The element lists have the same shape as a <see cref="PageSnapshot"/> so copy, paste and
///     duplicate share one clone path. Element ids are only meaningful inside the document (wires and
///     groups refer to them); pasting always generates fresh ids.
/// </remarks>
internal sealed record ClipboardEnvelope
{
    /// <summary>
    ///     Gets the format marker, always <see cref="PlexusClipboard.FormatId"/>.
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    ///     Gets the format version.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    ///     Gets the copied nodes.
    /// </summary>
    public IReadOnlyList<NodeSnapshot> Nodes { get; init; } = [];

    /// <summary>
    ///     Gets the copied wires (only those whose both ends were copied).
    /// </summary>
    public IReadOnlyList<WireSnapshot> Wires { get; init; } = [];

    /// <summary>
    ///     Gets the copied annotations.
    /// </summary>
    public IReadOnlyList<AnnotationSnapshot> Annotations { get; init; } = [];

    /// <summary>
    ///     Gets the copied groups.
    /// </summary>
    public IReadOnlyList<GroupSnapshot> Groups { get; init; } = [];

    /// <summary>
    ///     Gets the ids of all copied elements in z-order, or null for the default order.
    /// </summary>
    public IReadOnlyList<string>? ZOrder { get; init; }

    /// <summary>
    ///     Gets the element lists as importer input.
    /// </summary>
    /// <returns>The content.</returns>
    [JsonIgnore]
    public ElementContent Content => new(Nodes, Wires, Annotations, Groups, ZOrder);
}
