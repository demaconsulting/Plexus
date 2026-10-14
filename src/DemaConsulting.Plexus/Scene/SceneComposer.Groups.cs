namespace DemaConsulting.Plexus.Scene;

/// <content>
///     Group parts of the scene build: expanded frames and collapsed summary boxes with proxy ports.
/// </content>
internal sealed partial class SceneComposer
{
    /// <summary>
    ///     Finds the groups drawn as collapsed boxes and the wires that cross their boundaries.
    /// </summary>
    /// <remarks>
    ///     Only the outermost collapsed group of a nest is drawn; inner groups and all members are
    ///     hidden with it. Each wire with exactly one end inside a collapsed group (or the two ends
    ///     inside different collapsed groups) contributes a proxy port to that group.
    /// </remarks>
    private void FindCollapsedGroups()
    {
        foreach (var group in _page.Groups.Where(g => g.IsCollapsed && !g.IsHiddenByCollapse))
        {
            _collapsed[group] = new CollapsedGroupInfo(group);
        }

        foreach (var wire in _page.Wires)
        {
            var sourceOwner = CollapsedOwner(wire.Source.Node);
            var targetOwner = CollapsedOwner(wire.Target.Node);
            if (ReferenceEquals(sourceOwner, targetOwner))
            {
                continue;
            }

            if (sourceOwner is not null)
            {
                _collapsed[sourceOwner].AddOutput(wire.Source);
            }

            if (targetOwner is not null)
            {
                _collapsed[targetOwner].AddInput(wire.Target);
            }
        }

        // Boxes are sized only now that every proxy port is known
        foreach (var info in _collapsed.Values)
        {
            info.FinishLayout(_theme);
        }
    }

    /// <summary>
    ///     Finds the outermost collapsed group that contains an element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The outermost collapsed ancestor, or null when the element is not hidden by a collapse.</returns>
    private static Group? CollapsedOwner(DiagramElement element)
    {
        Group? owner = null;
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent.IsCollapsed)
            {
                owner = parent;
            }
        }

        return owner;
    }

    /// <summary>
    ///     Adds an expanded group's translucent frame, title bar, toggle and title.
    /// </summary>
    /// <param name="group">The visible expanded group.</param>
    private void AddExpandedGroup(Group group)
    {
        var frame = group.Bounds;
        if (!IsVisible(frame))
        {
            return;
        }

        var titleBar = new Rect(frame.Left, frame.Top, frame.Width, _theme.GroupTitleHeight);
        var toggle = ToggleRect(titleBar);

        // Publish the geometry that is about to be drawn and hit-tested, so hosts never re-derive it
        _groupLayouts[group] = new GroupLayout(group, false, frame, titleBar, toggle, [], []);
        _primitives.Add(new RectPrimitive(frame, _theme.GroupFill, _theme.GroupBorder, 1, 2, group));
        _primitives.Add(new RectPrimitive(titleBar, _theme.GroupTitleFill, null, 0, 2, group));
        AddToggle(group, toggle, expanded: true);
        AddGroupTitle(group, titleBar, toggle, _theme.GroupTitleTextStyle);
        AddHighlight(group, frame, 2);

        // Only the border band and the title bar react; the interior stays transparent to clicks
        _targets.Add(new HitTarget(HitKind.Group, group, null, frame, borderOnly: true));
        _targets.Add(new HitTarget(HitKind.GroupHeader, group, null, titleBar));
        _targets.Add(new HitTarget(HitKind.GroupCollapseToggle, group, null, toggle));
    }

    /// <summary>
    ///     Adds a collapsed group's summary box, title, toggle and proxy ports.
    /// </summary>
    /// <param name="info">The collapsed group's computed look.</param>
    private void AddCollapsedGroup(CollapsedGroupInfo info)
    {
        var group = info.Group;
        var box = info.Rect;

        // Lay out the proxy ports first: their labels extend the group's drawn bounds for culling
        var proxies = LayoutProxies(info);
        var extent = box;
        foreach (var proxy in proxies)
        {
            extent = extent.Union(proxy.Label.Bounds);
        }

        if (!IsVisible(extent))
        {
            return;
        }

        _primitives.Add(new RectPrimitive(box, _theme.NodeFill, _theme.NodeBorder, _theme.NodeBorderThickness, _theme.NodeCornerRadius, group));
        var titleBar = new Rect(box.Left, box.Top, box.Width, _theme.GroupTitleHeight);
        var toggle = ToggleRect(titleBar);

        // Publish the geometry that is about to be drawn and hit-tested, so hosts never re-derive it
        _groupLayouts[group] = new GroupLayout(
            group,
            true,
            box,
            titleBar,
            toggle,
            [.. proxies.Where(p => p.IsInput).Select(p => p.ToPublic())],
            [.. proxies.Where(p => !p.IsInput).Select(p => p.ToPublic())]);
        AddToggle(group, toggle, expanded: false);
        AddGroupTitle(group, titleBar, toggle, _theme.HeaderTextStyle);

        // Each proxy mirrors the node port look: colored triangle pointing right, label outside the box
        foreach (var proxy in proxies)
        {
            var t = proxy.Triangle;
            _primitives.Add(new PathPrimitive(
                [
                    PathCommand.MoveTo(new Point(t.Left, t.Top)),
                    PathCommand.LineTo(new Point(t.Right, t.Top + (t.Height / 2))),
                    PathCommand.LineTo(new Point(t.Left, t.Bottom)),
                    PathCommand.Close(),
                ],
                proxy.Color,
                null,
                1,
                group));
            var alignment = proxy.IsInput ? TextAlignment.Right : TextAlignment.Left;
            _primitives.Add(new TextPrimitive(proxy.Label.Text, proxy.Label.Bounds, _theme.PortLabelTextStyle with { Alignment = alignment }, group));
        }

        AddHighlight(group, box, _theme.NodeCornerRadius);
        _targets.Add(new HitTarget(HitKind.CollapsedGroup, group, null, box));
        _targets.Add(new HitTarget(HitKind.GroupCollapseToggle, group, null, toggle));
    }

    /// <summary>
    ///     Computes the look of every proxy port of a collapsed group.
    /// </summary>
    /// <param name="info">The collapsed group's computed look.</param>
    /// <returns>The proxies: inputs first, then outputs.</returns>
    private List<ProxyLayout> LayoutProxies(CollapsedGroupInfo info)
    {
        var proxies = new List<ProxyLayout>(info.Inputs.Count + info.Outputs.Count);
        var types = _page.Diagram.Types.PortTypes;
        for (var i = 0; i < info.Inputs.Count; i++)
        {
            proxies.Add(CreateProxy(info.Inputs[i], info.Edge(i, true, _theme), true, types));
        }

        for (var i = 0; i < info.Outputs.Count; i++)
        {
            proxies.Add(CreateProxy(info.Outputs[i], info.Edge(i, false, _theme), false, types));
        }

        return proxies;
    }

    /// <summary>
    ///     Computes the triangle and label of one proxy port.
    /// </summary>
    /// <param name="port">The inner port the proxy stands for.</param>
    /// <param name="edge">The proxy's row on the box edge.</param>
    /// <param name="isInput">Whether the proxy sits on the left edge.</param>
    /// <param name="types">The port type registry for colors.</param>
    /// <returns>The proxy layout.</returns>
    private ProxyLayout CreateProxy(Port port, Point edge, bool isInput, PortTypeRegistry types)
    {
        // Like node ports, the triangle sits outside the box against the edge
        var triangle = new Rect(
            isInput ? edge.X - _theme.PortTriangleLength : edge.X,
            edge.Y - (_theme.PortTriangleHeight / 2),
            _theme.PortTriangleLength,
            _theme.PortTriangleHeight);

        // Labels name the inner port ("node.port") and sit beside the triangle, centered on the row
        var text = CollapsedGroupInfo.LabelOf(port);
        var size = _measurer.Measure(text, _theme.PortLabelTextStyle);
        var x = isInput ? triangle.Left - _theme.PortLabelGap - size.Width : triangle.Right + _theme.PortLabelGap;
        var label = new LayoutText(text, new Rect(x, edge.Y - (size.Height / 2), size.Width, size.Height));
        var anchor = NodeGeometry.PortAnchor(edge, isInput ? PortDirection.Input : PortDirection.Output, text, _theme);
        return new ProxyLayout(port, anchor, isInput, triangle, label, types.Get(port.TypeKey).Color);
    }

    /// <summary>
    ///     Computes the rectangle of the collapse/expand toggle at the left of a title bar; it is both the drawn box and the hit area.
    /// </summary>
    /// <param name="titleBar">The title bar rectangle.</param>
    /// <returns>The toggle's rectangle.</returns>
    private Rect ToggleRect(Rect titleBar)
    {
        var size = _theme.GroupToggleSize;
        return new Rect(titleBar.Left + 6, titleBar.Top + ((titleBar.Height - size) / 2), size, size);
    }

    /// <summary>
    ///     Adds the collapse/expand toggle glyph (a small box with a minus or plus).
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="toggle">The toggle rectangle, see <see cref="ToggleRect"/>.</param>
    /// <param name="expanded">True to draw a minus (collapse), false to draw a plus (expand).</param>
    private void AddToggle(Group group, Rect toggle, bool expanded)
    {
        var size = toggle.Width;
        var color = _theme.GroupTitleText;
        _primitives.Add(new RectPrimitive(toggle, null, color, 1, 2, group));

        // The horizontal bar is always there; the vertical bar turns the minus into a plus
        var center = toggle.Center;
        var arm = (size / 2) - 3;
        var commands = new List<PathCommand>
        {
            PathCommand.MoveTo(new Point(center.X - arm, center.Y)),
            PathCommand.LineTo(new Point(center.X + arm, center.Y)),
        };
        if (!expanded)
        {
            commands.Add(PathCommand.MoveTo(new Point(center.X, center.Y - arm)));
            commands.Add(PathCommand.LineTo(new Point(center.X, center.Y + arm)));
        }

        _primitives.Add(new PathPrimitive(commands, null, color, 1.5, group));
    }

    /// <summary>
    ///     Adds a group's title text to the right of its toggle.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="titleBar">The title bar rectangle.</param>
    /// <param name="toggle">The toggle rectangle.</param>
    /// <param name="style">The text style.</param>
    private void AddGroupTitle(Group group, Rect titleBar, Rect toggle, TextStyle style)
    {
        var left = toggle.Right + 4;
        var width = Math.Max(0, titleBar.Right - left - 4);
        if (group.Title.Length > 0 && width > 0)
        {
            _primitives.Add(new TextPrimitive(group.Title, new Rect(left, titleBar.Top, width, titleBar.Height), style, group));
        }
    }

    /// <summary>
    ///     The computed look of one proxy port of a collapsed group.
    /// </summary>
    /// <param name="Port">The inner port the proxy stands for.</param>
    /// <param name="Anchor">The point displayed wires attach to.</param>
    /// <param name="IsInput">Whether the proxy is on the left edge.</param>
    /// <param name="Triangle">The triangle rectangle.</param>
    /// <param name="Label">The label text and rectangle.</param>
    /// <param name="Color">The port type color.</param>
    private sealed record ProxyLayout(Port Port, Point Anchor, bool IsInput, Rect Triangle, LayoutText Label, Color Color)
    {
        /// <summary>
        ///     Converts the proxy to its public layout record.
        /// </summary>
        /// <returns>The public layout.</returns>
        public GroupProxyPortLayout ToPublic() => new(Port, Anchor, Triangle, Label);
    }
}
