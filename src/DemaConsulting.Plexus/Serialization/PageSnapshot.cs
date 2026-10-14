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
