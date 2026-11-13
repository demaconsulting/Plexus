using System.Text.Json.Serialization;

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
    ///     Initializes a new instance of the <see cref="GroupSnapshot"/> class.
    /// </summary>
    public GroupSnapshot()
    {
        // Nothing to initialize: every member is set by an object initializer or keeps its default
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="GroupSnapshot"/> class from JSON.
    /// </summary>
    /// <remarks>
    ///     Binding the members that have initializers through this constructor makes a member missing from the JSON
    ///     read as null on every runtime, as the .NET 8 to 10 source generator does; from .NET 11 the generator would
    ///     otherwise keep the property initializer (see the snapshot design).
    /// </remarks>
    /// <param name="memberIds">The member ids read from the JSON, or null when missing.</param>
    [JsonConstructor]
    internal GroupSnapshot(IReadOnlyList<string>? memberIds)
    {
        MemberIds = memberIds!;
    }

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
