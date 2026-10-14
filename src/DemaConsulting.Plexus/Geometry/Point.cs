namespace DemaConsulting.Plexus.Geometry;

/// <summary>
///     An immutable two-dimensional point expressed in diagram coordinates (double precision).
/// </summary>
/// <remarks>
///     Plexus defines its own geometry primitives, rather than reusing a UI framework's, so the
///     core library stays UI-free. Note that the names are shared with Avalonia's types, so a file that imports both
///     <c>Avalonia</c> and <c>DemaConsulting.Plexus.Geometry</c> needs an alias (see the Avalonia package's
///     <c>PlexusAvaloniaExtensions</c> for the converters). Being a readonly record struct, a point has value equality and is
///     safe to share between threads.
/// </remarks>
/// <param name="X">The horizontal coordinate; increases to the right.</param>
/// <param name="Y">The vertical coordinate; increases downward.</param>
public readonly record struct Point(double X, double Y)
{
    /// <summary>
    ///     Gets the point at the origin (0, 0).
    /// </summary>
    public static Point Zero => new(0, 0);

    /// <summary>
    ///     Returns a copy of this point translated by the given deltas.
    /// </summary>
    /// <param name="dx">Horizontal delta.</param>
    /// <param name="dy">Vertical delta.</param>
    /// <returns>The translated point.</returns>
    public Point Offset(double dx, double dy) => new(X + dx, Y + dy);

    /// <summary>
    ///     Adds a size to a point, producing the point displaced by the size's width and height.
    /// </summary>
    /// <param name="point">The base point.</param>
    /// <param name="size">The size interpreted as a displacement.</param>
    /// <returns>The displaced point.</returns>
    public static Point operator +(Point point, Size size) => new(point.X + size.Width, point.Y + size.Height);

    /// <summary>
    ///     Computes the displacement between two points as a pair of deltas stored in a point.
    /// </summary>
    /// <param name="left">The point the displacement ends at.</param>
    /// <param name="right">The point the displacement starts from.</param>
    /// <returns>A point whose coordinates are the component-wise differences.</returns>
    public static Point operator -(Point left, Point right) => new(left.X - right.X, left.Y - right.Y);

    /// <summary>
    ///     Translates a point by a displacement stored in another point.
    /// </summary>
    /// <param name="left">The point to translate.</param>
    /// <param name="right">The displacement.</param>
    /// <returns>The translated point.</returns>
    public static Point operator +(Point left, Point right) => new(left.X + right.X, left.Y + right.Y);
}
