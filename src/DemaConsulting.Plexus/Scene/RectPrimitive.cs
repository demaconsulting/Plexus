namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     A rectangle, optionally filled, stroked and with rounded corners.
/// </summary>
/// <remarks>
///     The stroke is centered on the rectangle's edge. A null fill or stroke means "not drawn".
///     <see cref="ScenePrimitive.Bounds"/> is the rectangle drawn in, stroke included.
/// </remarks>
public sealed class RectPrimitive : ScenePrimitive
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RectPrimitive"/> class.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="fill">The fill color, or null for no fill.</param>
    /// <param name="stroke">The stroke color, or null for no stroke.</param>
    /// <param name="strokeThickness">The stroke thickness.</param>
    /// <param name="cornerRadius">The corner radius; 0 gives square corners.</param>
    /// <param name="element">The owning model element.</param>
    public RectPrimitive(
        Rect rect,
        Color? fill,
        Color? stroke,
        double strokeThickness = 1,
        double cornerRadius = 0,
        DiagramElement? element = null)
        : base(stroke is null ? rect : rect.Inflate(strokeThickness / 2), element)
    {
        Rect = rect;
        Fill = fill;
        Stroke = stroke;
        StrokeThickness = strokeThickness;
        CornerRadius = cornerRadius;
    }

    /// <summary>
    ///     Gets the rectangle.
    /// </summary>
    public Rect Rect { get; }

    /// <summary>
    ///     Gets the fill color, or null for no fill.
    /// </summary>
    public Color? Fill { get; }

    /// <summary>
    ///     Gets the stroke color, or null for no stroke.
    /// </summary>
    public Color? Stroke { get; }

    /// <summary>
    ///     Gets the stroke thickness.
    /// </summary>
    public double StrokeThickness { get; }

    /// <summary>
    ///     Gets the corner radius.
    /// </summary>
    public double CornerRadius { get; }
}
