namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     A background grid of dots covering a rectangle.
/// </summary>
/// <remarks>
///     Dots lie at every multiple of <see cref="Spacing"/> in diagram coordinates (not relative to
///     the rectangle), so the grid stays aligned with snapped node positions at any scroll offset.
///     Only dots inside <see cref="Rect"/> are drawn.
///     <see cref="ScenePrimitive.Element"/> is always null for the grid.
/// </remarks>
public sealed class GridPrimitive : ScenePrimitive
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GridPrimitive"/> class.
    /// </summary>
    /// <param name="rect">The area covered by the grid.</param>
    /// <param name="spacing">The distance between dots; must be positive.</param>
    /// <param name="color">The dot color.</param>
    /// <param name="dotSize">The diameter of a dot.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="spacing"/> is not positive.</exception>
    public GridPrimitive(Rect rect, double spacing, Color color, double dotSize = 1.5)
        : base(rect, null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(spacing, 0);
        Rect = rect;
        Spacing = spacing;
        Color = color;
        DotSize = dotSize;
    }

    /// <summary>
    ///     Gets the area covered by the grid.
    /// </summary>
    public Rect Rect { get; }

    /// <summary>
    ///     Gets the distance between dots.
    /// </summary>
    public double Spacing { get; }

    /// <summary>
    ///     Gets the dot color.
    /// </summary>
    public Color Color { get; }

    /// <summary>
    ///     Gets the diameter of a dot.
    /// </summary>
    public double DotSize { get; }
}
