namespace DemaConsulting.Plexus;

/// <summary>
///     A point in a snapshot, stored as plain numbers so the JSON shape does not depend on the geometry types.
/// </summary>
/// <remarks>
///     Serializes as <c>{"x": 1.5, "y": 2}</c>. Both members are required when reading.
/// </remarks>
public sealed record PointSnapshot
{
    /// <summary>
    ///     Gets the horizontal coordinate.
    /// </summary>
    public required double X { get; init; }

    /// <summary>
    ///     Gets the vertical coordinate.
    /// </summary>
    public required double Y { get; init; }

    /// <summary>
    ///     Creates a snapshot of a point.
    /// </summary>
    /// <param name="point">The point to capture.</param>
    /// <returns>The snapshot.</returns>
    public static PointSnapshot From(Point point) => new() { X = point.X, Y = point.Y };

    /// <summary>
    ///     Converts the snapshot back to a point.
    /// </summary>
    /// <returns>The point; the values are not validated here (loading code validates them).</returns>
    public Point ToPoint() => new(X, Y);
}
