namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     The kind of a <see cref="PathCommand"/>.
/// </summary>
public enum PathCommandKind
{
    /// <summary>Starts a new figure at a point.</summary>
    MoveTo,

    /// <summary>Draws a straight line to a point.</summary>
    LineTo,

    /// <summary>Draws a cubic Bezier curve to a point through two control points.</summary>
    CubicTo,

    /// <summary>Closes the current figure with a straight line back to its start.</summary>
    Close,
}

/// <summary>
///     One drawing command of a <see cref="PathPrimitive"/>.
/// </summary>
/// <remarks>
///     Use the factory methods; fields not used by a command kind are left at zero.
/// </remarks>
/// <param name="Kind">The command kind.</param>
/// <param name="End">The point the command ends at (unused by <see cref="PathCommandKind.Close"/>).</param>
/// <param name="Control1">The first Bezier control point (cubic only).</param>
/// <param name="Control2">The second Bezier control point (cubic only).</param>
public readonly record struct PathCommand(PathCommandKind Kind, Point End, Point Control1, Point Control2)
{
    /// <summary>
    ///     Creates a command that starts a new figure.
    /// </summary>
    /// <param name="point">The start point.</param>
    /// <returns>The command.</returns>
    public static PathCommand MoveTo(Point point) => new(PathCommandKind.MoveTo, point, default, default);

    /// <summary>
    ///     Creates a command that draws a straight line.
    /// </summary>
    /// <param name="point">The end point of the line.</param>
    /// <returns>The command.</returns>
    public static PathCommand LineTo(Point point) => new(PathCommandKind.LineTo, point, default, default);

    /// <summary>
    ///     Creates a command that draws a cubic Bezier curve.
    /// </summary>
    /// <param name="control1">The first control point.</param>
    /// <param name="control2">The second control point.</param>
    /// <param name="end">The end point of the curve.</param>
    /// <returns>The command.</returns>
    public static PathCommand CubicTo(Point control1, Point control2, Point end) =>
        new(PathCommandKind.CubicTo, end, control1, control2);

    /// <summary>
    ///     Creates a command that closes the current figure.
    /// </summary>
    /// <returns>The command.</returns>
    public static PathCommand Close() => new(PathCommandKind.Close, default, default, default);
}

/// <summary>
///     A path of lines and cubic curves, optionally filled and stroked; used for wires, port triangles and glyphs.
/// </summary>
/// <remarks>
///     A path is a flat list of <see cref="PathCommand"/>s; each <see cref="PathCommandKind.MoveTo"/>
///     starts a new figure. Open figures are not implicitly closed when filled.
///     Inherited from <see cref="ScenePrimitive"/>: <see cref="ScenePrimitive.Bounds"/> and <see cref="ScenePrimitive.Element"/> (the owning model element, or null).
/// </remarks>
public sealed class PathPrimitive : ScenePrimitive
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PathPrimitive"/> class.
    /// </summary>
    /// <param name="commands">The path commands (copied).</param>
    /// <param name="fill">The fill color, or null for no fill.</param>
    /// <param name="stroke">The stroke color, or null for no stroke.</param>
    /// <param name="strokeThickness">The stroke thickness.</param>
    /// <param name="element">The owning model element.</param>
    public PathPrimitive(
        IReadOnlyList<PathCommand> commands,
        Color? fill,
        Color? stroke,
        double strokeThickness = 1,
        DiagramElement? element = null)
        : base(ComputeBounds(commands, stroke is null ? 0 : strokeThickness), element)
    {
        Commands = [.. commands];
        Fill = fill;
        Stroke = stroke;
        StrokeThickness = strokeThickness;
    }

    /// <summary>
    ///     Gets the path commands in drawing order.
    /// </summary>
    public IReadOnlyList<PathCommand> Commands { get; }

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
    ///     Computes a conservative bounding box: all end and control points, inflated by half the stroke.
    /// </summary>
    /// <param name="commands">The path commands.</param>
    /// <param name="thickness">The stroke thickness, or zero when not stroked.</param>
    /// <returns>The bounds; empty when the path has no points.</returns>
    private static Rect ComputeBounds(IReadOnlyList<PathCommand> commands, double thickness)
    {
        var any = false;
        double minX = 0, minY = 0, maxX = 0, maxY = 0;
        foreach (var command in commands)
        {
            if (command.Kind == PathCommandKind.Close)
            {
                continue;
            }

            // Control points bound the curve, so including them is a safe over-estimate
            var points = command.Kind == PathCommandKind.CubicTo
                ? new[] { command.Control1, command.Control2, command.End }
                : [command.End];
            foreach (var point in points)
            {
                if (!any)
                {
                    (minX, minY, maxX, maxY) = (point.X, point.Y, point.X, point.Y);
                    any = true;
                    continue;
                }

                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY).Inflate(thickness / 2);
    }
}
