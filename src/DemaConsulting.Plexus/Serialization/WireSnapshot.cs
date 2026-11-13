using System.Text.Json.Serialization;

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
    ///     Initializes a new instance of the <see cref="WireSnapshot"/> class.
    /// </summary>
    public WireSnapshot()
    {
        // Nothing to initialize: every member is set by an object initializer or keeps its default
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="WireSnapshot"/> class from JSON.
    /// </summary>
    /// <remarks>
    ///     Binding the members that have initializers through this constructor makes a member missing from the JSON
    ///     read as null on every runtime, as the .NET 8 to 10 source generator does; from .NET 11 the generator would
    ///     otherwise keep the property initializer (see the snapshot design).
    /// </remarks>
    /// <param name="route">The stored route read from the JSON, or null when missing.</param>
    [JsonConstructor]
    internal WireSnapshot(IReadOnlyList<PointSnapshot>? route)
    {
        Route = route!;
    }

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
