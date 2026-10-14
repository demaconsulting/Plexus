namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     The computed look of one collapsed group: its summary box and the proxy ports for wires crossing its boundary.
/// </summary>
/// <remarks>
///     Proxy ports are read-only stand-ins for the inner ports whose wires leave or enter the group.
///     There is one proxy per distinct inner port, no matter how many wires use it. Inputs sit on
///     the left edge and outputs on the right edge, like node ports. The box grows taller than the
///     group's theme size when there are more proxies than fit.
/// </remarks>
internal sealed class CollapsedGroupInfo
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CollapsedGroupInfo"/> class.
    /// </summary>
    /// <param name="group">The collapsed group.</param>
    public CollapsedGroupInfo(Group group)
    {
        Group = group;
        Rect = group.Bounds;
    }

    /// <summary>
    ///     Gets the collapsed group.
    /// </summary>
    public Group Group { get; }

    /// <summary>
    ///     Gets the inner ports that wires enter the group through, in first-use order.
    /// </summary>
    public List<Port> Inputs { get; } = [];

    /// <summary>
    ///     Gets the inner ports that wires leave the group through, in first-use order.
    /// </summary>
    public List<Port> Outputs { get; } = [];

    /// <summary>
    ///     Gets the drawn box, which is at least the group's collapsed bounds.
    /// </summary>
    public Rect Rect { get; private set; }

    /// <summary>
    ///     Records that a wire enters the group at an inner input port.
    /// </summary>
    /// <param name="port">The inner input port.</param>
    public void AddInput(Port port)
    {
        if (!Inputs.Contains(port))
        {
            Inputs.Add(port);
        }
    }

    /// <summary>
    ///     Records that a wire leaves the group from an inner output port.
    /// </summary>
    /// <param name="port">The inner output port.</param>
    public void AddOutput(Port port)
    {
        if (!Outputs.Contains(port))
        {
            Outputs.Add(port);
        }
    }

    /// <summary>
    ///     Sizes the box to fit every proxy port; call once all proxies are known.
    /// </summary>
    /// <param name="theme">The theme supplying metrics.</param>
    public void FinishLayout(DiagramTheme theme)
    {
        var rows = Math.Max(Inputs.Count, Outputs.Count);
        var needed = theme.PortAlignment == PortAlignment.Top
            ? theme.GroupTitleHeight + (theme.PortPitch * rows) + 4
            : theme.GroupTitleHeight + (theme.PortPitch * rows);
        var bounds = Group.Bounds;
        Rect = new Rect(bounds.Left, bounds.Top, bounds.Width, Math.Max(bounds.Height, needed));
    }

    /// <summary>
    ///     Gets the point on the box edge a proxy port hangs from.
    /// </summary>
    /// <remarks>
    ///     With <see cref="PortAlignment.Center"/> each column of proxies is centered on the drawn box,
    ///     like node ports; with <see cref="PortAlignment.Top"/> rows start below the title bar.
    /// </remarks>
    /// <param name="index">The zero-based row of the proxy.</param>
    /// <param name="isInput">True for the left edge, false for the right edge.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The edge point.</returns>
    public Point Edge(int index, bool isInput, DiagramTheme theme)
    {
        var x = isInput ? Rect.Left : Rect.Right;
        if (theme.PortAlignment == PortAlignment.Top)
        {
            return new Point(x, Rect.Top + theme.GroupTitleHeight + (theme.PortPitch * (index + 0.5)));
        }

        var count = isInput ? Inputs.Count : Outputs.Count;
        return new Point(x, Rect.Top + (Rect.Height / 2) + ((index - ((count - 1) / 2.0)) * theme.PortPitch));
    }

    /// <summary>
    ///     Gets the text of a proxy port's label, which names the inner port.
    /// </summary>
    /// <param name="port">The inner port.</param>
    /// <returns>The label text.</returns>
    public static string LabelOf(Port port) => port.ToString();

    /// <summary>
    ///     Gets the anchor of a proxy port: the outer end of its triangle and label, like a node port.
    /// </summary>
    /// <param name="index">The zero-based row of the proxy.</param>
    /// <param name="isInput">True for the left edge, false for the right edge.</param>
    /// <param name="label">The proxy's label text.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The anchor point.</returns>
    public Point Anchor(int index, bool isInput, string label, DiagramTheme theme) =>
        NodeGeometry.PortAnchor(Edge(index, isInput, theme), isInput ? PortDirection.Input : PortDirection.Output, label, theme);

    /// <summary>
    ///     Gets the anchor of the proxy standing in for an inner port.
    /// </summary>
    /// <param name="port">The inner port.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The anchor of the proxy.</returns>
    public Point AnchorOf(Port port, DiagramTheme theme) =>
        port.Direction == PortDirection.Input
            ? Anchor(Inputs.IndexOf(port), true, LabelOf(port), theme)
            : Anchor(Outputs.IndexOf(port), false, LabelOf(port), theme);

    /// <summary>
    ///     Gets the obstacle boxes of the collapsed group for wire routing: the box with margin plus its label strips.
    /// </summary>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The obstacle boxes.</returns>
    public IEnumerable<Rect> RoutingObstacles(DiagramTheme theme)
    {
        yield return Rect.Inflate(theme.ObstacleMargin);
        for (var i = 0; i < Inputs.Count; i++)
        {
            yield return Strip(Edge(i, true, theme), PortDirection.Input, LabelOf(Inputs[i]), theme);
        }

        for (var i = 0; i < Outputs.Count; i++)
        {
            yield return Strip(Edge(i, false, theme), PortDirection.Output, LabelOf(Outputs[i]), theme);
        }
    }

    /// <summary>
    ///     Computes the strip covering one proxy's triangle and label.
    /// </summary>
    /// <param name="edge">The proxy's point on the box edge.</param>
    /// <param name="direction">The proxy's direction.</param>
    /// <param name="label">The proxy's label.</param>
    /// <param name="theme">The theme supplying metrics.</param>
    /// <returns>The strip rectangle, one port row tall.</returns>
    private static Rect Strip(Point edge, PortDirection direction, string label, DiagramTheme theme)
    {
        var reach = NodeGeometry.PortReach(label, theme);
        var left = direction == PortDirection.Input ? edge.X - reach : edge.X;
        return new Rect(left, edge.Y - (theme.PortPitch / 2), reach, theme.PortPitch);
    }
}
