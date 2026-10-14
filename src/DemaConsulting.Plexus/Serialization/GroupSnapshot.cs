namespace DemaConsulting.Plexus;

/// <summary>
///     The persisted state of one <see cref="Group"/>.
/// </summary>
/// <remarks>
///     Membership is stored on the group (child ids, in member order) so nesting needs no extra
///     parent links. Expanded bounds are computed from the members and therefore not stored.
/// </remarks>
public sealed record GroupSnapshot
{
    /// <summary>
    ///     Gets the element id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     Gets the title shown in the title bar.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    ///     Gets the ids of the direct members (nodes, annotations and nested groups) in member order.
    /// </summary>
    public IReadOnlyList<string> MemberIds { get; init; } = [];

    /// <summary>
    ///     Gets a value indicating whether the group is collapsed.
    /// </summary>
    public bool IsCollapsed { get; init; }

    /// <summary>
    ///     Gets the top-left corner of the group's box while collapsed.
    /// </summary>
    public required PointSnapshot CollapsedPosition { get; init; }
}
