namespace DemaConsulting.Plexus;

/// <summary>
///     The element lists shared by page snapshots and clipboard payloads, as input to the importer.
/// </summary>
/// <remarks>
///     Lists may be null because they come from untrusted JSON where members can be omitted; the
///     importer treats null as empty.
/// </remarks>
/// <param name="Nodes">The node snapshots.</param>
/// <param name="Wires">The wire snapshots.</param>
/// <param name="Annotations">The annotation snapshots.</param>
/// <param name="Groups">The group snapshots.</param>
/// <param name="ZOrder">Element ids bottom-most first, or null for the default order.</param>
internal sealed record ElementContent(
    IReadOnlyList<NodeSnapshot>? Nodes,
    IReadOnlyList<WireSnapshot>? Wires,
    IReadOnlyList<AnnotationSnapshot>? Annotations,
    IReadOnlyList<GroupSnapshot>? Groups,
    IReadOnlyList<string>? ZOrder);
