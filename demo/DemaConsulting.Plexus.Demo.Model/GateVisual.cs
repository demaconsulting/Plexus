using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     A custom node visual that draws a classic logic gate symbol instead of the standard node box.
/// </summary>
/// <remarks>
///     This showcases the <see cref="NodeType.Visual"/> escape hatch. The visual extends
///     <see cref="StandardNodeVisual"/> and replaces only the box, title and icon; the instance name,
///     property summary and the typed port triangles with their labels are still produced by the
///     standard parts, and ports keep the positions the node layout gives them. Because the gate is
///     drawn inside the node's own box, hit testing, selection and wire attachment work unchanged. It also
///     honors the node's <see cref="NodeVisualContext.Style"/> (fill, border, title color and opacity),
///     so a <see cref="Diagram.NodeStyler"/> restyles gates like any other node.
/// </remarks>
public sealed class GateVisual : StandardNodeVisual
{
    /// <summary>
    ///     The Bezier control-point factor that approximates a quarter circle.
    /// </summary>
    private const double Quarter = 0.5523;

    /// <summary>
    ///     The depth of the concave back of an OR gate, as a fraction of the gate width.
    /// </summary>
    private const double BackDepth = 0.12;

    /// <summary>
    ///     The width of the extra curve of an XOR gate, in diagram units.
    /// </summary>
    private const double XorGap = 9;

    /// <summary>
    ///     The radius of the inversion bubble of a NOT gate, in diagram units.
    /// </summary>
    private const double BubbleRadius = 6;

    /// <summary>
    ///     The length of the lead lines that join port rows to the gate symbol, in diagram units.
    /// </summary>
    private const double LeadLength = 26;

    /// <summary>
    ///     The symbol this visual draws.
    /// </summary>
    private readonly GateShape _shape;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GateVisual"/> class.
    /// </summary>
    /// <param name="shape">The gate symbol to draw.</param>
    public GateVisual(GateShape shape) => _shape = shape;

    /// <summary>
    ///     Draws the lead lines and the gate symbol in place of the standard box.
    /// </summary>
    /// <param name="context">The visual context.</param>
    protected override void AddBox(NodeVisualContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var box = context.Layout.Box;
        var line = context.Border;
        var fill = context.Fill;
        var thickness = context.BorderThickness + 1;

        // Leads run from each port row on the box edge into the symbol; the symbol is drawn over their inner ends
        foreach (var port in context.Layout.Ports)
        {
            var y = port.Triangle.Top + (port.Triangle.Height / 2);
            var (from, to) = port.Port.Direction == PortDirection.Input
                ? (box.Left, box.Left + LeadLength)
                : (box.Right, box.Right - LeadLength);
            context.Add(new PathPrimitive(
                [PathCommand.MoveTo(new Point(from, y)), PathCommand.LineTo(new Point(to, y))],
                null,
                line,
                thickness));
        }

        // The symbol itself: a filled outline in the node colors
        context.Add(new PathPrimitive(Outline(box), fill, line, thickness));
        if (_shape == GateShape.Not)
        {
            context.Add(new PathPrimitive(Circle(box.Right - BubbleRadius, box.Top + (box.Height / 2), BubbleRadius), fill, line, thickness));
        }
        else if (_shape == GateShape.Xor)
        {
            context.Add(new PathPrimitive(Back(box.Left, box.Top, box.Height, box.Width), null, line, thickness));
        }
    }

    /// <summary>
    ///     Draws the type title in the middle of the symbol rather than in a header band.
    /// </summary>
    /// <param name="context">The visual context.</param>
    protected override void AddTitle(NodeVisualContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var box = context.Layout.Box;

        // The pointed and curved shapes carry more visual weight on the right, so the text sits a little left of center
        var left = box.Left + (_shape is GateShape.Or or GateShape.Xor ? 14 : 4);
        var width = box.Width - (_shape == GateShape.Not ? 26 : 22);
        context.Add(new TextPrimitive(context.Layout.Title.Text, new Rect(left, box.Top, Math.Max(1, width), box.Height), context.HeaderTextStyle));
    }

    /// <summary>
    ///     Skips the halo: a rectangular glow around a gate symbol would not follow its outline.
    /// </summary>
    /// <param name="context">The visual context.</param>
    protected override void AddGlow(NodeVisualContext context)
    {
        // Intentionally empty: the other style members (fill, border, title color, opacity) are honored
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    ///     Skips the icon: the gate symbol is the node's picture.
    /// </summary>
    /// <param name="context">The visual context.</param>
    protected override void AddIcon(NodeVisualContext context)
    {
        // Intentionally empty: the gate symbol drawn by AddBox is the node's picture
        ArgumentNullException.ThrowIfNull(context);
    }

    /// <summary>
    ///     Builds the filled outline of the gate symbol.
    /// </summary>
    /// <param name="box">The node box the symbol fills.</param>
    /// <returns>The path commands.</returns>
    private List<PathCommand> Outline(Rect box)
    {
        var (l, t, r, b) = (box.Left, box.Top, box.Right, box.Bottom);
        var cy = t + (box.Height / 2);
        var w = box.Width;
        var h = box.Height;
        switch (_shape)
        {
            case GateShape.And:
                // Flat back, straight top and bottom, then a half ellipse for the front
                var mid = l + (w / 2);
                var rx = w / 2;
                var ry = h / 2;
                return
                [
                    PathCommand.MoveTo(new Point(l, t)),
                    PathCommand.LineTo(new Point(mid, t)),
                    PathCommand.CubicTo(new Point(mid + (rx * Quarter), t), new Point(r, cy - (ry * Quarter)), new Point(r, cy)),
                    PathCommand.CubicTo(new Point(r, cy + (ry * Quarter)), new Point(mid + (rx * Quarter), b), new Point(mid, b)),
                    PathCommand.LineTo(new Point(l, b)),
                    PathCommand.Close(),
                ];
            case GateShape.Not:
                // A triangle that stops short of the right edge to leave room for the bubble
                return
                [
                    PathCommand.MoveTo(new Point(l, t)),
                    PathCommand.LineTo(new Point(r - (2 * BubbleRadius), cy)),
                    PathCommand.LineTo(new Point(l, b)),
                    PathCommand.Close(),
                ];
            default:
                // OR and XOR share the pointed shape; XOR starts a little further right to make room for its extra curve
                var left = _shape == GateShape.Xor ? l + XorGap : l;
                var width = r - left;
                var commands = new List<PathCommand>
                {
                    PathCommand.MoveTo(new Point(left, t)),
                    PathCommand.CubicTo(new Point(left + (0.5 * width), t), new Point(left + (0.8 * width), t + (0.18 * h)), new Point(r, cy)),
                    PathCommand.CubicTo(new Point(left + (0.8 * width), b - (0.18 * h)), new Point(left + (0.5 * width), b), new Point(left, b)),
                };
                commands.AddRange(Back(left, t, h, width).Skip(1));
                commands.Add(PathCommand.Close());
                return commands;
        }
    }

    /// <summary>
    ///     Builds the concave back curve of an OR or XOR gate, running upward from the bottom-left corner to the top-left corner.
    /// </summary>
    /// <remarks>
    ///     The first command is the move to the bottom-left corner. The OR outline skips it, because
    ///     its front curves already end there, and uses the curve to close the shape.
    /// </remarks>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="height">The gate height.</param>
    /// <param name="width">The gate width.</param>
    /// <returns>The path commands.</returns>
    private static List<PathCommand> Back(double left, double top, double height, double width)
    {
        // Control points are pulled inward so the curve bows into the gate by about BackDepth of its width
        var inward = left + (BackDepth * width * 4 / 3);
        return
        [
            PathCommand.MoveTo(new Point(left, top + height)),
            PathCommand.CubicTo(new Point(inward, top + (0.7 * height)), new Point(inward, top + (0.3 * height)), new Point(left, top)),
        ];
    }

    /// <summary>
    ///     Builds a circle as four Bezier curves.
    /// </summary>
    /// <param name="cx">The center x coordinate.</param>
    /// <param name="cy">The center y coordinate.</param>
    /// <param name="radius">The radius.</param>
    /// <returns>The path commands.</returns>
    private static List<PathCommand> Circle(double cx, double cy, double radius)
    {
        var k = radius * Quarter;
        return
        [
            PathCommand.MoveTo(new Point(cx + radius, cy)),
            PathCommand.CubicTo(new Point(cx + radius, cy + k), new Point(cx + k, cy + radius), new Point(cx, cy + radius)),
            PathCommand.CubicTo(new Point(cx - k, cy + radius), new Point(cx - radius, cy + k), new Point(cx - radius, cy)),
            PathCommand.CubicTo(new Point(cx - radius, cy - k), new Point(cx - k, cy - radius), new Point(cx, cy - radius)),
            PathCommand.CubicTo(new Point(cx + k, cy - radius), new Point(cx + radius, cy - k), new Point(cx + radius, cy)),
            PathCommand.Close(),
        ];
    }
}
