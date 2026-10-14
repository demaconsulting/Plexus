namespace DemaConsulting.Plexus;

/// <summary>
///     The persisted state of one <see cref="Wire"/>.
/// </summary>
/// <remarks>
///     The route holds the interior bend points exactly as stored; loading restores them without
///     asking the router, so manual edits survive a round trip.
/// </remarks>
public sealed record WireSnapshot
{
    /// <summary>
    ///     Gets the element id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     Gets the id of the node the wire starts at.
    /// </summary>
    public required string SourceNodeId { get; init; }

    /// <summary>
    ///     Gets the name of the output port the wire starts at.
    /// </summary>
    public required string SourcePort { get; init; }

    /// <summary>
    ///     Gets the id of the node the wire ends at.
    /// </summary>
    public required string TargetNodeId { get; init; }

    /// <summary>
    ///     Gets the name of the input port the wire ends at.
    /// </summary>
    public required string TargetPort { get; init; }

    /// <summary>
    ///     Gets the interior bend points from start to end.
    /// </summary>
    public IReadOnlyList<PointSnapshot> Route { get; init; } = [];
}
