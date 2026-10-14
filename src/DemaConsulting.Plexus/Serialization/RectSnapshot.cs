namespace DemaConsulting.Plexus;

/// <summary>
///     A rectangle in a snapshot, stored as plain numbers so the JSON shape does not depend on the geometry types.
/// </summary>
/// <remarks>
///     Serializes as <c>{"x": 10, "y": 20, "width": 90, "height": 60}</c>. All members are required when reading.
/// </remarks>
public sealed record RectSnapshot
{
    /// <summary>
    ///     Gets the left edge.
    /// </summary>
    public required double X { get; init; }

    /// <summary>
    ///     Gets the top edge.
    /// </summary>
    public required double Y { get; init; }

    /// <summary>
    ///     Gets the width.
    /// </summary>
    public required double Width { get; init; }

    /// <summary>
    ///     Gets the height.
    /// </summary>
    public required double Height { get; init; }

    /// <summary>
    ///     Creates a snapshot of a rectangle.
    /// </summary>
    /// <param name="rect">The rectangle to capture.</param>
    /// <returns>The snapshot.</returns>
    public static RectSnapshot From(Rect rect) => new() { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height };

    /// <summary>
    ///     Converts the snapshot back to a rectangle.
    /// </summary>
    /// <returns>The rectangle; the values are not validated here (loading code validates them).</returns>
    public Rect ToRect() => new(X, Y, Width, Height);
}
