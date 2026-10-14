namespace DemaConsulting.Plexus.Geometry;

/// <summary>
///     An immutable axis-aligned rectangle in diagram coordinates.
/// </summary>
/// <remarks>
///     The rectangle is described by its top-left corner and its extent, matching how nodes,
///     annotations and groups store their bounds. All derived values (edges, center) are
///     computed on demand so the struct stays a plain value type.
/// </remarks>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The horizontal extent.</param>
/// <param name="Height">The vertical extent.</param>
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Rect"/> struct from a location and a size.
    /// </summary>
    /// <param name="position">The top-left corner.</param>
    /// <param name="size">The extent of the rectangle.</param>
    public Rect(Point position, Size size)
        : this(position.X, position.Y, size.Width, size.Height)
    {
    }

    /// <summary>
    ///     Gets the left edge.
    /// </summary>
    public double Left => X;

    /// <summary>
    ///     Gets the top edge.
    /// </summary>
    public double Top => Y;

    /// <summary>
    ///     Gets the right edge (<see cref="X"/> plus <see cref="Width"/>).
    /// </summary>
    public double Right => X + Width;

    /// <summary>
    ///     Gets the bottom edge (<see cref="Y"/> plus <see cref="Height"/>).
    /// </summary>
    public double Bottom => Y + Height;

    /// <summary>
    ///     Gets the top-left corner.
    /// </summary>
    public Point Position => new(X, Y);

    /// <summary>
    ///     Gets the extent of the rectangle.
    /// </summary>
    public Size Size => new(Width, Height);

    /// <summary>
    ///     Gets the center point.
    /// </summary>
    public Point Center => new(X + (Width / 2), Y + (Height / 2));

    /// <summary>
    ///     Gets a value indicating whether the rectangle has no area.
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    ///     Builds the smallest rectangle containing two corner points, in either order.
    /// </summary>
    /// <param name="a">The first corner.</param>
    /// <param name="b">The opposite corner.</param>
    /// <returns>The normalized rectangle.</returns>
    public static Rect FromPoints(Point a, Point b)
    {
        // Normalize so callers can pass corners in any order (for example a rubber-band drag)
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return new Rect(left, top, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    /// <summary>
    ///     Determines whether a point lies inside the rectangle (edges inclusive).
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><see langword="true"/> when the point is inside or on the border.</returns>
    public bool Contains(Point point) =>
        point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;

    /// <summary>
    ///     Determines whether another rectangle lies completely inside this one.
    /// </summary>
    /// <param name="other">The rectangle to test.</param>
    /// <returns><see langword="true"/> when <paramref name="other"/> is fully contained.</returns>
    public bool Contains(Rect other) =>
        other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;

    /// <summary>
    ///     Determines whether two rectangles overlap or touch.
    /// </summary>
    /// <param name="other">The rectangle to test.</param>
    /// <returns><see langword="true"/> when the rectangles share at least one point.</returns>
    public bool Intersects(Rect other) =>
        other.Left <= Right && other.Right >= Left && other.Top <= Bottom && other.Bottom >= Top;

    /// <summary>
    ///     Returns the smallest rectangle containing both rectangles.
    /// </summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>The union rectangle.</returns>
    public Rect Union(Rect other)
    {
        // Take the extreme edges of both rectangles
        var left = Math.Min(Left, other.Left);
        var top = Math.Min(Top, other.Top);
        var right = Math.Max(Right, other.Right);
        var bottom = Math.Max(Bottom, other.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    ///     Returns a copy grown by the same amount on every side (shrunk when negative).
    /// </summary>
    /// <param name="amount">The amount to add to each side.</param>
    /// <returns>The inflated rectangle.</returns>
    public Rect Inflate(double amount) =>
        new(X - amount, Y - amount, Width + (2 * amount), Height + (2 * amount));

    /// <summary>
    ///     Returns a copy translated by the given deltas.
    /// </summary>
    /// <param name="dx">Horizontal delta.</param>
    /// <param name="dy">Vertical delta.</param>
    /// <returns>The translated rectangle.</returns>
    public Rect Offset(double dx, double dy) => new(X + dx, Y + dy, Width, Height);

    /// <summary>
    ///     Returns a copy with the same size placed at a new top-left corner.
    /// </summary>
    /// <param name="position">The new top-left corner.</param>
    /// <returns>The relocated rectangle.</returns>
    public Rect WithPosition(Point position) => new(position.X, position.Y, Width, Height);
}
