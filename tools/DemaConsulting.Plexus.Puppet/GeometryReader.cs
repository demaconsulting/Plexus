using System.Text.Json.Nodes;

namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     A point in physical screen pixels.
/// </summary>
/// <param name="X">The horizontal pixel.</param>
/// <param name="Y">The vertical pixel.</param>
internal readonly record struct ScreenPoint(double X, double Y)
{
    /// <summary>
    ///     Gets the pixel column, rounded to the nearest pixel.
    /// </summary>
    internal int PixelX => (int)Math.Round(X, MidpointRounding.AwayFromZero);

    /// <summary>
    ///     Gets the pixel row, rounded to the nearest pixel.
    /// </summary>
    internal int PixelY => (int)Math.Round(Y, MidpointRounding.AwayFromZero);
}

/// <summary>
///     A rectangle in physical screen pixels.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
internal readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    /// <summary>
    ///     Gets the middle of the rectangle.
    /// </summary>
    internal ScreenPoint Center => new(X + (Width / 2), Y + (Height / 2));

    /// <summary>
    ///     Tests whether a point lies inside the rectangle.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true"/> when the point is inside (edges included).</returns>
    internal bool Contains(ScreenPoint point) =>
        point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;
}

/// <summary>
///     A point described in both coordinate systems by the demo's <c>geometry</c> command.
/// </summary>
/// <param name="DiagramX">The diagram x coordinate.</param>
/// <param name="DiagramY">The diagram y coordinate.</param>
/// <param name="Screen">The physical screen pixel.</param>
/// <param name="Visible">Whether the point is inside the editor.</param>
internal readonly record struct TargetPoint(double DiagramX, double DiagramY, ScreenPoint Screen, bool Visible);

/// <summary>
///     A rectangle described in both coordinate systems by the demo's <c>geometry</c> command.
/// </summary>
/// <param name="DiagramX">The diagram left edge.</param>
/// <param name="DiagramY">The diagram top edge.</param>
/// <param name="DiagramWidth">The diagram width.</param>
/// <param name="DiagramHeight">The diagram height.</param>
/// <param name="Screen">The rectangle in physical screen pixels.</param>
/// <param name="Visible">Whether the rectangle's center is inside the editor.</param>
internal readonly record struct TargetRect(double DiagramX, double DiagramY, double DiagramWidth, double DiagramHeight, ScreenRect Screen, bool Visible);

/// <summary>
///     Reads the JSON the demo's <c>geometry</c> command returns, and picks safe places to press.
/// </summary>
internal static class GeometryReader
{
    /// <summary>
    ///     The places inside a node's box to try, as fractions of its width and height, best first.
    /// </summary>
    private static readonly (double X, double Y)[] BodyCandidates =
    [
        (0.5, 0.5), (0.5, 0.4), (0.5, 0.6), (0.5, 0.7), (0.5, 0.3), (0.35, 0.5), (0.65, 0.5),
    ];

    /// <summary>
    ///     Reads a point description (<c>{diagram:{x,y}, screen:{x,y}, visible}</c>).
    /// </summary>
    /// <param name="node">The description.</param>
    /// <returns>The point.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the description is incomplete.</exception>
    internal static TargetPoint ReadPoint(JsonNode? node)
    {
        var diagram = Require(node, "diagram");
        var screen = Require(node, "screen");
        return new TargetPoint(
            Number(diagram, "x"),
            Number(diagram, "y"),
            new ScreenPoint(Number(screen, "x"), Number(screen, "y")),
            node!["visible"]?.GetValue<bool>() ?? true);
    }

    /// <summary>
    ///     Reads a rectangle description (<c>{diagram:{x,y,width,height}, screen:{...}, visible}</c>).
    /// </summary>
    /// <param name="node">The description.</param>
    /// <returns>The rectangle.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the description is incomplete.</exception>
    internal static TargetRect ReadRect(JsonNode? node)
    {
        var diagram = Require(node, "diagram");
        var screen = Require(node, "screen");
        return new TargetRect(
            Number(diagram, "x"),
            Number(diagram, "y"),
            Number(diagram, "width"),
            Number(diagram, "height"),
            new ScreenRect(Number(screen, "x"), Number(screen, "y"), Number(screen, "width"), Number(screen, "height")),
            node!["visible"]?.GetValue<bool>() ?? true);
    }

    /// <summary>
    ///     Finds a place on a node's box where a press is neither on its name nor on a port.
    /// </summary>
    /// <param name="nodeGeometry">The result of <c>geometry</c> for a node.</param>
    /// <returns>The point to press, in both coordinate systems.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the node is hidden or the geometry is incomplete.</exception>
    internal static TargetPoint BodyPoint(JsonNode nodeGeometry)
    {
        if (nodeGeometry["hidden"]?.GetValue<bool>() ?? false)
        {
            throw new InvalidOperationException("the node is not drawn (it is inside a collapsed group)");
        }

        var bounds = ReadRect(Require(nodeGeometry, "bounds"));
        var avoid = AvoidRects(nodeGeometry);
        foreach (var (fx, fy) in BodyCandidates)
        {
            var screen = new ScreenPoint(bounds.Screen.X + (bounds.Screen.Width * fx), bounds.Screen.Y + (bounds.Screen.Height * fy));
            if (!avoid.Any(rect => rect.Contains(screen)))
            {
                return new TargetPoint(
                    bounds.DiagramX + (bounds.DiagramWidth * fx),
                    bounds.DiagramY + (bounds.DiagramHeight * fy),
                    screen,
                    bounds.Visible);
            }
        }

        // Nothing is free (a tiny node): fall back to the center and let the editor decide
        return new TargetPoint(
            bounds.DiagramX + (bounds.DiagramWidth / 2),
            bounds.DiagramY + (bounds.DiagramHeight / 2),
            bounds.Screen.Center,
            bounds.Visible);
    }

    /// <summary>
    ///     Reads the press point of a port from the geometry of a port or of a node.
    /// </summary>
    /// <param name="portGeometry">The <c>port</c> member of a <c>geometry</c> port result.</param>
    /// <returns>The point inside the port's hit area, or its anchor when the scene did not publish a hit area.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the geometry is incomplete.</exception>
    internal static TargetPoint PortPressPoint(JsonNode portGeometry) =>
        ReadPoint(portGeometry["press"] ?? Require(portGeometry, "anchor"));

    /// <summary>
    ///     Collects the rectangles (name label, port hit areas) a body press must not land in.
    /// </summary>
    /// <param name="nodeGeometry">The node geometry.</param>
    /// <returns>The rectangles in screen pixels.</returns>
    private static List<ScreenRect> AvoidRects(JsonNode nodeGeometry)
    {
        var rects = new List<ScreenRect>();
        rects.AddRange(new[] { "nameRect", "title" }
            .Select(key => nodeGeometry[key])
            .Where(label => label is not null)
            .Select(label => ReadRect(label!).Screen));

        if (nodeGeometry["ports"] is JsonArray ports)
        {
            rects.AddRange(ports.Where(p => p?["hit"] is not null).Select(p => ReadRect(p!["hit"]).Screen));
        }

        return rects;
    }

    /// <summary>
    ///     Gets a required child node.
    /// </summary>
    /// <param name="node">The parent.</param>
    /// <param name="name">The child name.</param>
    /// <returns>The child.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the child is missing.</exception>
    private static JsonNode Require(JsonNode? node, string name) =>
        node?[name] ?? throw new InvalidOperationException($"the geometry reply has no '{name}'");

    /// <summary>
    ///     Reads a number member.
    /// </summary>
    /// <param name="node">The object holding the number.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the member is missing.</exception>
    private static double Number(JsonNode node, string name) =>
        Require(node, name).GetValue<double>();
}
