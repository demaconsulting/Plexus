namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     The computed geometry of a node: its box, title, icon, name, port rows and summary lines.
/// </summary>
/// <remarks>
///     One layout serves rendering, hit testing and custom node visuals, so what is drawn is what
///     is hit. Port anchors are independent of the <see cref="ITextMeasurer"/> (they use a fixed
///     deterministic label-width estimate) and always equal <see cref="Plexus.Port.Anchor"/>;
///     only label, name and summary rectangles depend on the measurer. Create
///     layouts with <see cref="Compute"/>. A layout is an immutable snapshot of the node's state
///     at the time it was computed.
/// </remarks>
public sealed class NodeLayout
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeLayout"/> class.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="box">The node box.</param>
    /// <param name="title">The title text and rectangle.</param>
    /// <param name="iconBounds">The icon rectangle, or null when the node type has no icon.</param>
    /// <param name="name">The instance name text and rectangle.</param>
    /// <param name="summaryLines">The summary lines.</param>
    /// <param name="ports">The port layouts, inputs first.</param>
    private NodeLayout(
        Node node,
        Rect box,
        LayoutText title,
        Rect? iconBounds,
        LayoutText name,
        IReadOnlyList<LayoutText> summaryLines,
        IReadOnlyList<PortLayout> ports)
    {
        Node = node;
        Box = box;
        Title = title;
        IconBounds = iconBounds;
        Name = name;
        SummaryLines = summaryLines;
        Ports = ports;

        // The overall bounds cover everything drawn for the node, labels included
        var bounds = box.Union(name.Bounds);
        foreach (var line in summaryLines)
        {
            bounds = bounds.Union(line.Bounds);
        }

        foreach (var port in ports)
        {
            bounds = bounds.Union(port.Label.Bounds).Union(port.Triangle);
        }

        Bounds = bounds;
    }

    /// <summary>
    ///     Gets the node this layout describes.
    /// </summary>
    public Node Node { get; }

    /// <summary>
    ///     Gets the node box: the filled rectangle, equal to <see cref="Plexus.Node.Bounds"/>.
    /// </summary>
    public Rect Box { get; }

    /// <summary>
    ///     Gets the bounds of everything drawn for the node: box, name, summary and port labels.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    ///     Gets the node type title and the header band it is centered in.
    /// </summary>
    public LayoutText Title { get; }

    /// <summary>
    ///     Gets the rectangle reserved for the icon, or null when the node type has no icon.
    /// </summary>
    public Rect? IconBounds { get; }

    /// <summary>
    ///     Gets the instance name and its rectangle above the box.
    /// </summary>
    public LayoutText Name { get; }

    /// <summary>
    ///     Gets the property summary lines, each with the rectangle it is centered in below the box.
    /// </summary>
    public IReadOnlyList<LayoutText> SummaryLines { get; }

    /// <summary>
    ///     Gets the port layouts: all inputs top to bottom, then all outputs top to bottom.
    /// </summary>
    public IReadOnlyList<PortLayout> Ports { get; }

    /// <summary>
    ///     Computes the layout of a node.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Port anchors come from <see cref="NodeGeometry"/> and never from the measurer, so a layout
    ///         computed with any measurer agrees with the model and the routers. Labels sit beside
    ///         their triangles, centered on the port row, between the box and the anchor.
    ///     </para>
    ///     <para>
    ///         Port geometry - port rows, anchors, triangles, label gaps and hit areas - always uses the node's own
    ///         diagram theme (<see cref="Diagram.Theme"/>, the classic theme for a node that is not on a page), the
    ///         theme the model uses for <see cref="Plexus.Port.Anchor"/>, stored routes and obstacles. So a
    ///         <paramref name="theme"/> that differs from it (a scene, render or editor override) changes the look but
    ///         never where ports and wire ends are.
    ///     </para>
    /// </remarks>
    /// <param name="node">The node to lay out.</param>
    /// <param name="theme">
    ///     The theme supplying colors, fonts, text styles and the decoration metrics (header band, icon box, text gaps);
    ///     port positions, triangles and hit areas always use the node's own diagram theme so they stay at the wire ends.
    /// </param>
    /// <param name="measurer">The measurer used to size text.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static NodeLayout Compute(Node node, DiagramTheme theme, ITextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(measurer);

        var box = node.Bounds;

        // The title is centered in the header band across the top of the box
        var title = new LayoutText(node.Type.Title, new Rect(box.Left, box.Top, box.Width, theme.HeaderHeight));

        // The icon is centered in the body below the header, shrunk when the box is small
        Rect? icon = null;
        if (node.Type.Icon is not null)
        {
            var bodyHeight = Math.Max(0, box.Height - theme.HeaderHeight);
            var side = Math.Max(0, Math.Min(theme.IconBoxSize, Math.Min(box.Width - 8, bodyHeight - 4)));
            icon = new Rect(
                box.Left + ((box.Width - side) / 2),
                box.Top + theme.HeaderHeight + ((bodyHeight - side) / 2),
                side,
                side);
        }

        // The instance name sits centered just above the box
        var nameSize = measurer.Measure(node.Name, theme.NameTextStyle);
        var name = new LayoutText(
            node.Name,
            new Rect(box.Left + ((box.Width - nameSize.Width) / 2), box.Top - theme.TextGap - nameSize.Height, nameSize.Width, nameSize.Height));

        // Summary lines stack downwards below the box
        var lines = new List<LayoutText>();
        var y = box.Bottom + theme.TextGap;
        foreach (var line in node.Type.GetSummaryLines(node))
        {
            var size = measurer.Measure(line, theme.SummaryTextStyle);
            lines.Add(new LayoutText(line, new Rect(box.Left + ((box.Width - size.Width) / 2), y, size.Width, size.Height)));
            y += size.Height;
        }

        return new NodeLayout(node, box, title, icon, name, lines, ComputePorts(node, node.Theme, theme, measurer));
    }

    /// <summary>
    ///     Computes the layout of every port of a node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="geometry">The node's own diagram theme, which fixes the port positions, triangles and hit areas.</param>
    /// <param name="theme">The display theme, which supplies the label text style.</param>
    /// <param name="measurer">The text measurer.</param>
    /// <returns>The port layouts, inputs first.</returns>
    private static List<PortLayout> ComputePorts(Node node, DiagramTheme geometry, DiagramTheme theme, ITextMeasurer measurer)
    {
        var ports = new List<PortLayout>(node.Ports.Count);
        foreach (var port in node.Ports)
        {
            var isInput = port.Direction == PortDirection.Input;
            var count = isInput ? node.Inputs.Count : node.Outputs.Count;
            var edge = NodeGeometry.PortEdge(node.Bounds, port.Direction, port.Index, count, geometry);
            var anchor = NodeGeometry.PortAnchor(edge, port.Direction, port.Label, geometry);

            // The triangle sits outside the box against the edge and points to the right (flow direction):
            // an input's tip touches the edge, an output's base starts at it
            var triangle = new Rect(
                isInput ? edge.X - geometry.PortTriangleLength : edge.X,
                edge.Y - (geometry.PortTriangleHeight / 2),
                geometry.PortTriangleLength,
                geometry.PortTriangleHeight);

            // The port reacts from just inside the box edge out to its anchor, which covers triangle and label
            var outer = anchor.X;
            var inner = isInput ? edge.X + geometry.PortHitRadius : edge.X - geometry.PortHitRadius;
            var hit = new Rect(
                Math.Min(outer, inner),
                edge.Y - geometry.PortHitRadius,
                Math.Abs(outer - inner),
                2 * geometry.PortHitRadius);

            // The label sits beside the triangle, vertically centered on the port row: left of inputs
            // (right-aligned), right of outputs (left-aligned). Using the real measured width keeps the
            // label hugging the triangle; the wire end (the anchor) uses the deterministic estimate.
            var size = measurer.Measure(port.Label, theme.PortLabelTextStyle);
            var labelX = isInput ? triangle.Left - geometry.PortLabelGap - size.Width : triangle.Right + geometry.PortLabelGap;
            var label = new LayoutText(port.Label, new Rect(labelX, edge.Y - (size.Height / 2), size.Width, size.Height));
            ports.Add(new PortLayout(port, anchor, triangle, hit, label));
        }

        return ports;
    }
}
