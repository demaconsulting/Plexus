using System.Text.Json.Serialization;

namespace DemaConsulting.Plexus;

/// <summary>
///     The persisted state of one <see cref="DiagramPage"/>.
/// </summary>
/// <remarks>
///     Elements are stored by kind, each list in the page's list order. <see cref="ZOrder"/> restores the
///     interleaving between kinds (bottom-most first); when it is omitted, groups are drawn at the back,
///     followed by nodes, annotations and wires.
/// </remarks>
public sealed record PageSnapshot
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PageSnapshot"/> class.
    /// </summary>
    public PageSnapshot()
    {
        // Nothing to initialize: every member is set by an object initializer or keeps its default
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PageSnapshot"/> class from JSON.
    /// </summary>
    /// <remarks>
    ///     Binding the members that have initializers through this constructor makes a member missing from the JSON
    ///     read as null on every runtime, as the .NET 8 to 10 source generator does; from .NET 11 the generator would
    ///     otherwise keep the property initializer (see the snapshot design).
    /// </remarks>
    /// <param name="nodes">The nodes read from the JSON, or null when missing.</param>
    /// <param name="wires">The wires read from the JSON, or null when missing.</param>
    /// <param name="annotations">The annotations read from the JSON, or null when missing.</param>
    /// <param name="groups">The groups read from the JSON, or null when missing.</param>
    [JsonConstructor]
    internal PageSnapshot(IReadOnlyList<NodeSnapshot>? nodes, IReadOnlyList<WireSnapshot>? wires, IReadOnlyList<AnnotationSnapshot>? annotations, IReadOnlyList<GroupSnapshot>? groups)
    {
        Nodes = nodes!;
        Wires = wires!;
        Annotations = annotations!;
        Groups = groups!;
    }

    /// <summary>
    ///     Gets the page name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets the nodes.
    /// </summary>
    public IReadOnlyList<NodeSnapshot> Nodes { get; init; } = [];

    /// <summary>
    ///     Gets the wires.
    /// </summary>
    public IReadOnlyList<WireSnapshot> Wires { get; init; } = [];

    /// <summary>
    ///     Gets the annotations.
    /// </summary>
    public IReadOnlyList<AnnotationSnapshot> Annotations { get; init; } = [];

    /// <summary>
    ///     Gets the groups.
    /// </summary>
    public IReadOnlyList<GroupSnapshot> Groups { get; init; } = [];

    /// <summary>
    ///     Gets the ids of every element in z-order (bottom-most first), or null for the default order.
    /// </summary>
    public IReadOnlyList<string>? ZOrder { get; init; }
}
