namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Text-independent node geometry shared by the model, the wire routers and the painters.
/// </summary>
/// <remarks>
///     Port anchors and default node sizes depend only on <see cref="DiagramTheme"/> metrics, the
///     number of ports and the port labels (measured with a fixed deterministic estimate, never a
///     painter's measurer), so the model can place and route wires without a painter.
///     Painters must derive their port drawing from the same functions.
///     All members are pure and thread-safe.
/// </remarks>
public static class NodeGeometry
{
    /// <summary>
    ///     Factor by which estimated label widths are widened so real fonts rarely exceed them.
    /// </summary>
    private const double LabelWidthSafety = 1.05;

    /// <summary>
    ///     The deterministic measurer behind port anchors; never replaced by a painter's measurer.
    /// </summary>
    private static readonly ApproximateTextMeasurer AnchorMeasurer = new();
    /// <summary>
    ///     Computes the default size of a node of the given type.
    /// </summary>
    /// <remarks>
    ///     A size declared on the type wins; otherwise the theme's default width and a height large
    ///     enough for the longer of the two port columns are used, so ports never overflow the box.
    /// </remarks>
    /// <param name="type">The node type.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The size a new node of this type starts with.</returns>
    public static Size DefaultNodeSize(NodeType type, DiagramTheme theme)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(theme);

        // A host-declared size is authoritative
        if (type.DefaultSize is { } declared)
        {
            return declared;
        }

        // Otherwise grow the default height to fit the longer port column
        var portCount = Math.Max(type.Inputs.Count, type.Outputs.Count);
        return new Size(theme.DefaultNodeWidth, Math.Max(theme.DefaultNodeHeight, RequiredHeight(portCount, theme)));
    }

    /// <summary>
    ///     Computes the node height needed to show a column of ports.
    /// </summary>
    /// <remarks>
    ///     With <see cref="PortAlignment.Top"/> the column starts <see cref="DiagramTheme.FirstPortOffset"/>
    ///     below the header and ends <see cref="DiagramTheme.PortBottomPadding"/> above the bottom.
    ///     With <see cref="PortAlignment.Center"/> the column is centered, so the topmost anchor must clear
    ///     the header by <see cref="DiagramTheme.FirstPortOffset"/> and the bottommost anchor must clear the
    ///     bottom by <see cref="DiagramTheme.PortBottomPadding"/>; the height is the larger of the two
    ///     symmetric requirements, i.e. twice the larger clearance (header included) plus the column's span.
    /// </remarks>
    /// <param name="portCount">Number of ports in the longest column.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The minimum height that keeps every port anchor inside the box with padding.</returns>
    public static double RequiredHeight(int portCount, DiagramTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (portCount <= 0)
        {
            return theme.HeaderHeight;
        }

        var span = (portCount - 1) * theme.PortPitch;
        if (theme.PortAlignment == PortAlignment.Top)
        {
            return theme.HeaderHeight + theme.FirstPortOffset + span + theme.PortBottomPadding;
        }

        // Centered: the stack is symmetric about the middle, so both ends need the larger clearance
        var clearance = Math.Max(theme.HeaderHeight + theme.FirstPortOffset, theme.PortBottomPadding);
        return (2 * clearance) + span;
    }

    /// <summary>
    ///     Computes the point on a node box edge at a port's row, where the port triangle is attached.
    /// </summary>
    /// <remarks>
    ///     Inputs sit on the left edge and outputs on the right edge; the index selects the row.
    ///     With <see cref="PortAlignment.Center"/> the column of <paramref name="count"/> ports is
    ///     centered on the box: row <c>i</c> lies at <c>Top + Height/2 + (i - (count-1)/2) * PortPitch</c>.
    ///     With <see cref="PortAlignment.Top"/> it lies at <c>Top + HeaderHeight + FirstPortOffset + i * PortPitch</c>
    ///     and <paramref name="count"/> is ignored.
    ///     This is not where wires attach (see <see cref="PortAnchor(Rect, PortDirection, int, int, string, DiagramTheme)"/>).
    /// </remarks>
    /// <param name="bounds">The node's bounding box.</param>
    /// <param name="direction">Whether the port is an input or an output.</param>
    /// <param name="index">Zero-based position of the port within its column.</param>
    /// <param name="count">Number of ports in that column (inputs or outputs of the node type).</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The edge point in diagram coordinates.</returns>
    public static Point PortEdge(Rect bounds, PortDirection direction, int index, int count, DiagramTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var x = direction == PortDirection.Input ? bounds.Left : bounds.Right;
        var y = theme.PortAlignment == PortAlignment.Top
            ? bounds.Top + theme.HeaderHeight + theme.FirstPortOffset + (index * theme.PortPitch)
            : bounds.Top + (bounds.Height / 2) + ((index - ((count - 1) / 2.0)) * theme.PortPitch);
        return new Point(x, y);
    }

    /// <summary>
    ///     Estimates the width of a port label deterministically, slightly on the generous side.
    /// </summary>
    /// <remarks>
    ///     The estimate comes from the <see cref="ApproximateTextMeasurer"/> with the theme's port label
    ///     style, widened by a safety factor and rounded up, so real fonts rarely exceed it. It never
    ///     depends on a painter, which is what keeps port anchors identical in the model, the routers
    ///     and every painter. If a real font is wider than the estimate the label simply crowds the
    ///     wire end a little; if it is narrower the gap grows.
    /// </remarks>
    /// <param name="label">The label text; empty text has zero width.</param>
    /// <param name="theme">The theme supplying the label style.</param>
    /// <returns>The reserved label width in diagram units.</returns>
    public static double PortLabelWidth(string label, DiagramTheme theme)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(theme);
        if (label.Length == 0)
        {
            return 0;
        }

        return Math.Ceiling(AnchorMeasurer.Measure(label, theme.PortLabelTextStyle).Width * LabelWidthSafety);
    }

    /// <summary>
    ///     Computes how far a port's wire attachment point lies beyond the node box edge.
    /// </summary>
    /// <remarks>
    ///     The reach covers the port triangle, then (for a non-empty label) a gap, the reserved label
    ///     width and a second gap. A port without a label attaches at the outer end of its triangle.
    /// </remarks>
    /// <param name="label">The port label.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The distance from the box edge to the anchor, always positive.</returns>
    public static double PortReach(string label, DiagramTheme theme)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(theme);
        return label.Length == 0
            ? theme.PortTriangleLength
            : theme.PortTriangleLength + theme.PortLabelGap + PortLabelWidth(label, theme) + theme.PortLabelGap;
    }

    /// <summary>
    ///     Computes the anchor point of a port: where its wires attach.
    /// </summary>
    /// <remarks>
    ///     The anchor lies at the outer end of the port's triangle and label, beyond the box edge:
    ///     inputs to the left of the box, outputs to the right. Wires leave an output (and arrive at
    ///     an input) horizontally at this point. It depends only on theme metrics, the port's row and
    ///     its label, never on a painter.
    /// </remarks>
    /// <param name="bounds">The node's bounding box.</param>
    /// <param name="direction">Whether the port is an input or an output.</param>
    /// <param name="index">Zero-based position of the port within its column.</param>
    /// <param name="count">Number of ports in that column (inputs or outputs of the node type).</param>
    /// <param name="label">The port's label.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The anchor point in diagram coordinates.</returns>
    public static Point PortAnchor(Rect bounds, PortDirection direction, int index, int count, string label, DiagramTheme theme) =>
        PortAnchor(PortEdge(bounds, direction, index, count, theme), direction, label, theme);

    /// <summary>
    ///     Computes the anchor point of a port from the point on the box edge it hangs from.
    /// </summary>
    /// <param name="edge">The port's row on the box edge, see <see cref="PortEdge"/>.</param>
    /// <param name="direction">Whether the port is an input or an output.</param>
    /// <param name="label">The port's label.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The anchor point in diagram coordinates.</returns>
    public static Point PortAnchor(Point edge, PortDirection direction, string label, DiagramTheme theme)
    {
        var reach = PortReach(label, theme);
        return new Point(direction == PortDirection.Input ? edge.X - reach : edge.X + reach, edge.Y);
    }

    /// <summary>
    ///     Computes the deterministic extent of everything drawn for a node: box, name, summary lines and ports.
    /// </summary>
    /// <remarks>
    ///     The name label sits above the box, the summary lines below it and the port triangles and
    ///     labels beside it, out to each port's wire anchor (see <see cref="PortReach"/>). Texts are
    ///     measured with the same fixed estimate as port anchors (never a painter's measurer) and
    ///     widened by the same safety factor, so this extent is identical in the model, the scene,
    ///     hit testing and every painter. Group frames enclose this extent rather than only the
    ///     node boxes, which is what keeps labels from crossing a frame edge.
    /// </remarks>
    /// <param name="node">The node.</param>
    /// <param name="theme">The theme supplying metrics and text styles.</param>
    /// <returns>The node's visual extent in diagram coordinates; it always contains <see cref="Node.Bounds"/>.</returns>
    public static Rect VisualBounds(Node node, DiagramTheme theme)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(theme);
        var box = node.Bounds;
        var extent = box;

        // The instance name is centered just above the box
        var nameSize = AnchorMeasurer.Measure(node.Name, theme.NameTextStyle);
        extent = extent.Union(CenteredText(box, nameSize, box.Top - theme.TextGap - nameSize.Height));

        // Summary lines stack below the box, each centered under it
        var y = box.Bottom + theme.TextGap;
        foreach (var line in node.Type.GetSummaryLines(node))
        {
            var size = AnchorMeasurer.Measure(line, theme.SummaryTextStyle);
            extent = extent.Union(CenteredText(box, size, y));
            y += size.Height;
        }

        // Ports reach sideways from the box edge out to their anchors, one row tall
        extent = ExtendByPorts(extent, box, PortDirection.Input, node.Type.Inputs, theme);
        return ExtendByPorts(extent, box, PortDirection.Output, node.Type.Outputs, theme);
    }

    /// <summary>
    ///     Computes the rectangle of a text centered horizontally on a box, widened by the label safety factor.
    /// </summary>
    /// <param name="box">The box to center on.</param>
    /// <param name="size">The measured text size.</param>
    /// <param name="top">The top of the text rectangle.</param>
    /// <returns>The text rectangle.</returns>
    private static Rect CenteredText(Rect box, Size size, double top)
    {
        var width = size.Width * LabelWidthSafety;
        return new Rect(box.Left + ((box.Width - width) / 2), top, width, size.Height);
    }

    /// <summary>
    ///     Grows an extent to cover one column of ports, from the box edge to each port's anchor.
    /// </summary>
    /// <param name="extent">The extent so far.</param>
    /// <param name="box">The node box.</param>
    /// <param name="direction">The column's direction.</param>
    /// <param name="ports">The column's port declarations.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The grown extent.</returns>
    private static Rect ExtendByPorts(Rect extent, Rect box, PortDirection direction, IList<PortDefinition> ports, DiagramTheme theme)
    {
        for (var i = 0; i < ports.Count; i++)
        {
            var edge = PortEdge(box, direction, i, ports.Count, theme);
            var anchor = PortAnchor(edge, direction, ports[i].Label, theme);
            var left = Math.Min(edge.X, anchor.X);
            extent = extent.Union(new Rect(left, edge.Y - (theme.PortTriangleHeight / 2), Math.Abs(anchor.X - edge.X), theme.PortTriangleHeight));
        }

        return extent;
    }

    /// <summary>
    ///     Computes the obstacle boxes a node contributes to wire routing.
    /// </summary>
    /// <remarks>
    ///     The first box is the node box inflated by <see cref="DiagramTheme.ObstacleMargin"/>. Every
    ///     labelled port whose label reaches beyond that margin adds a strip from the box edge to its
    ///     anchor (one port row tall), so wires of other nodes never run across a port label. A
    ///     port's own wire is unaffected because its stub leaves from the end of its own strip.
    /// </remarks>
    /// <param name="bounds">The node's bounding box.</param>
    /// <param name="type">The node's type, which supplies the ports.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The obstacle boxes.</returns>
    public static IReadOnlyList<Rect> RoutingObstacles(Rect bounds, NodeType type, DiagramTheme theme)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(theme);
        var boxes = new List<Rect> { bounds.Inflate(theme.ObstacleMargin) };
        AddPortStrips(boxes, bounds, PortDirection.Input, type.Inputs, theme);
        AddPortStrips(boxes, bounds, PortDirection.Output, type.Outputs, theme);
        return boxes;
    }

    /// <summary>
    ///     Adds the label strips of one column of ports to an obstacle list.
    /// </summary>
    /// <param name="boxes">The list to add to.</param>
    /// <param name="bounds">The node's bounding box.</param>
    /// <param name="direction">The column's direction.</param>
    /// <param name="ports">The column's port declarations.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    private static void AddPortStrips(List<Rect> boxes, Rect bounds, PortDirection direction, IList<PortDefinition> ports, DiagramTheme theme)
    {
        for (var i = 0; i < ports.Count; i++)
        {
            // Strips are only needed where the label reaches beyond the margin box already covering the edge
            var label = ports[i].Label;
            var reach = PortReach(label, theme);
            if (label.Length == 0 || reach <= theme.ObstacleMargin)
            {
                continue;
            }

            var edge = PortEdge(bounds, direction, i, ports.Count, theme);
            var outer = direction == PortDirection.Input ? edge.X - reach : edge.X + reach;
            var half = theme.PortPitch / 2;
            var left = Math.Min(edge.X, outer);
            boxes.Add(new Rect(left, edge.Y - half, reach, theme.PortPitch));
        }
    }
}
