using DemaConsulting.Plexus.Routing;

namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Builds one <see cref="DiagramScene"/>: turns a page into ordered primitives and hit targets.
/// </summary>
/// <remarks>
///     The composer is a single-use worker behind <see cref="SceneBuilder.Build"/>. Drawing order is:
///     background grid, expanded group frames (outermost first), wires, then nodes, annotations and
///     collapsed groups in the page's z-order, each followed by its selection or hover outline.
///     Hit targets are recorded in the same order from the same geometry, which is what keeps hit
///     testing identical to rendering. This file holds the shared state, the build sequence and the
///     node, annotation and wire parts; group parts live in the other partial file.
/// </remarks>
internal sealed partial class SceneComposer
{
    /// <summary>
    ///     The page being drawn.
    /// </summary>
    private readonly DiagramPage _page;

    /// <summary>
    ///     The theme in effect.
    /// </summary>
    private readonly DiagramTheme _theme;

    /// <summary>
    ///     The text measurer in effect.
    /// </summary>
    private readonly ITextMeasurer _measurer;

    /// <summary>
    ///     The options of the build.
    /// </summary>
    private readonly SceneOptions _options;

    /// <summary>
    ///     The selected elements.
    /// </summary>
    private readonly HashSet<DiagramElement> _selection;

    /// <summary>
    ///     The primitives collected so far, in drawing order.
    /// </summary>
    private readonly List<ScenePrimitive> _primitives = [];

    /// <summary>
    ///     The hit targets collected so far, in drawing order.
    /// </summary>
    private readonly List<HitTarget> _targets = [];

    /// <summary>
    ///     The layouts of the nodes that were built.
    /// </summary>
    private readonly Dictionary<Node, NodeLayout> _layouts = [];

    /// <summary>
    ///     The displayed polylines of the wires that were built.
    /// </summary>
    private readonly Dictionary<Wire, IReadOnlyList<Point>> _wirePoints = [];

    /// <summary>
    ///     The selected wires that were built with their displayed polyline and thickness, which get handles.
    /// </summary>
    private readonly List<(Wire Wire, IReadOnlyList<Point> Points, double Thickness)> _selectedWires = [];

    /// <summary>
    ///     The collapsed groups that are drawn as boxes (outermost collapsed groups only).
    /// </summary>
    private readonly Dictionary<Group, CollapsedGroupInfo> _collapsed = [];

    /// <summary>
    ///     The layouts of the groups that were built.
    /// </summary>
    private readonly Dictionary<Group, GroupLayout> _groupLayouts = [];

    /// <summary>
    ///     Obstacles for display routes, built on first use.
    /// </summary>
    private List<Rect>? _obstacles;

    /// <summary>
    ///     The frames of the expanded groups display routes may have to avoid, built on first use.
    /// </summary>
    private GroupObstacles? _groupObstacles;

    /// <summary>
    ///     The wire segments display routes keep clear of, built on first use.
    /// </summary>
    private WireTraffic? _traffic;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SceneComposer"/> class.
    /// </summary>
    /// <param name="page">The page to draw.</param>
    /// <param name="options">The build options.</param>
    public SceneComposer(DiagramPage page, SceneOptions options)
    {
        _page = page;
        _options = options;
        _theme = options.Theme ?? page.Diagram.Theme;
        _measurer = options.TextMeasurer ?? new ApproximateTextMeasurer();
        _selection = [.. options.Selection];
    }

    /// <summary>
    ///     Runs the build.
    /// </summary>
    /// <returns>The finished scene.</returns>
    public DiagramScene Compose()
    {
        // Work out which groups are drawn as boxes and which wires cross their boundary
        FindCollapsedGroups();

        // Expanded group frames go at the very back, outermost first so nested frames stay visible
        foreach (var group in _page.Groups
                     .Where(g => !g.IsCollapsed && !g.IsHiddenByCollapse)
                     .OrderBy(Depth))
        {
            AddExpandedGroup(group);
        }

        // Every wire's displayed polyline is worked out first, because a wire's corners depend on the junctions
        // of its net and its hops on the wires of other nets
        var wires = PrepareWires();
        var dots = AnalyzeWires(wires);
        foreach (var wire in wires)
        {
            AddWire(wire);
        }

        AddJunctions(dots);

        // Everything else follows the page's z-order
        foreach (var element in _page.Elements)
        {
            switch (element)
            {
                case Node node when !node.IsHiddenByCollapse:
                    AddNode(node);
                    break;
                case Annotation annotation when !annotation.IsHiddenByCollapse:
                    AddAnnotation(annotation);
                    break;
                case Group group when _collapsed.TryGetValue(group, out var info):
                    AddCollapsedGroup(info);
                    break;
            }
        }

        AddWireHandles();
        return Finish();
    }

    /// <summary>
    ///     One wire to be drawn: its displayed polyline, resolved look and what its net and neighbors do to its drawing.
    /// </summary>
    private sealed class WireDraw
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="WireDraw"/> class.
        /// </summary>
        /// <param name="wire">The wire.</param>
        /// <param name="points">The displayed polyline.</param>
        /// <param name="style">The resolved wire style, or null when the host styler gave none.</param>
        /// <param name="color">The wire color.</param>
        /// <param name="thickness">The stroke thickness.</param>
        public WireDraw(Wire wire, List<Point> points, WireStyle? style, Color color, double thickness)
        {
            Wire = wire;
            Points = points;
            Style = style;
            Color = color;
            Thickness = thickness;
        }

        /// <summary>
        ///     Gets the wire.
        /// </summary>
        public Wire Wire { get; }

        /// <summary>
        ///     Gets the displayed polyline.
        /// </summary>
        public List<Point> Points { get; }

        /// <summary>
        ///     Gets the resolved wire style, or null when the host styler gave none.
        /// </summary>
        public WireStyle? Style { get; }

        /// <summary>
        ///     Gets the wire color.
        /// </summary>
        public Color Color { get; }

        /// <summary>
        ///     Gets the stroke thickness.
        /// </summary>
        public double Thickness { get; }

        /// <summary>
        ///     Gets or sets the junction points of the wire's net; corners at these points are drawn square.
        /// </summary>
        public IReadOnlyList<Point> Junctions { get; set; } = [];

        /// <summary>
        ///     Gets or sets the crossings this wire hops over.
        /// </summary>
        public IReadOnlyList<WireHop> Hops { get; set; } = [];
    }

    /// <summary>
    ///     Works out the displayed polyline and look of every visible wire, in page order.
    /// </summary>
    /// <returns>The wires that are drawn (wires hidden inside a collapsed group are left out).</returns>
    private List<WireDraw> PrepareWires()
    {
        var wires = new List<WireDraw>(_page.Wires.Count);
        foreach (var wire in _page.Wires)
        {
            var points = DisplayPoints(wire);
            if (points is null)
            {
                continue;
            }

            // The host's styler may override any member; whatever it leaves null comes from the theme
            var style = _page.Diagram.ResolveWireStyle(wire);
            wires.Add(new WireDraw(wire, points, style, style?.Color ?? _theme.WireColor, style?.Thickness ?? _theme.WireThickness));
        }

        return wires;
    }

    /// <summary>
    ///     Finds the junctions of every net and the crossings between nets, and records them on the wires.
    /// </summary>
    /// <remarks>
    ///     A net is every wire that starts at the same output port, extended through inputs that allow
    ///     fan-in: wires that end at the same such input belong to one net with the wires of their sources,
    ///     so the point where they converge is marked with a dot like a fan-out branch. Junctions are found
    ///     per net, so wires of different nets never make a junction. Crossings are only looked for when the theme has
    ///     <see cref="DiagramTheme.WireCrossingJumps"/> on. Both searches see all wires, including those
    ///     outside the culling bounds, so what is drawn does not depend on the visible area.
    /// </remarks>
    /// <param name="wires">The wires to analyze.</param>
    /// <returns>The junction points, each with the wire that owns the dot and the dot's radius.</returns>
    private List<(Point Point, WireDraw Owner, double Radius)> AnalyzeWires(List<WireDraw> wires)
    {
        var dots = new List<(Point, WireDraw, double)>();
        var nets = FindNets(wires);
        foreach (var net in wires.GroupBy(w => nets[w]))
        {
            var members = net.ToList();
            if (members.Count < 2)
            {
                continue;
            }

            var junctions = WireJunctions.Find([.. members.Select(m => m.Points)]);
            var points = junctions.Select(j => j.Point).ToList();
            foreach (var member in members)
            {
                member.Junctions = points;
            }

            // A dot grows with the heaviest wire of its net so it stays visible on thick wires
            var scale = _theme.WireThickness > 0 ? Math.Max(1, members.Max(m => m.Thickness) / _theme.WireThickness) : 1;
            foreach (var (point, owner) in junctions)
            {
                dots.Add((point, members[owner], _theme.WireJunctionRadius * scale));
            }
        }

        if (_theme.WireCrossingJumps && _theme.WireJumpRadius > 0)
        {
            var hops = WireCrossings.Find(
                [.. wires.Select(w => new WireRun(w.Points, nets[w], w.Thickness, w.Junctions))],
                _theme.WireJumpRadius,
                _theme.WireThickness,
                _theme.WireCornerRadius,
                _theme.WireJunctionDots ? _theme.WireJunctionRadius : 0);
            for (var i = 0; i < wires.Count; i++)
            {
                wires[i].Hops = hops[i];
            }
        }

        return dots;
    }

    /// <summary>
    ///     Groups wires into nets: wires from one output port, joined through inputs that allow fan-in.
    /// </summary>
    /// <param name="wires">The wires to group.</param>
    /// <returns>For each wire, a port that names its net (equal for wires of the same net).</returns>
    private static Dictionary<WireDraw, Port> FindNets(List<WireDraw> wires)
    {
        // A small union-find over ports: a wire links its source to its target when the target accepts fan-in
        var parents = new Dictionary<Port, Port>();
        Port Find(Port port)
        {
            if (!parents.TryGetValue(port, out var parent))
            {
                parents[port] = port;
                return port;
            }

            if (ReferenceEquals(parent, port))
            {
                return port;
            }

            var root = Find(parent);
            parents[port] = root;
            return root;
        }

        foreach (var link in wires.Select(w => w.Wire))
        {
            var source = Find(link.Source);
            if (link.Target.AllowsMultipleWires)
            {
                var target = Find(link.Target);
                if (!ReferenceEquals(source, target))
                {
                    parents[target] = source;
                }
            }
        }

        return wires.ToDictionary(w => w, w => Find(w.Wire.Source));
    }

    /// <summary>
    ///     Adds a filled dot wherever wires of one net branch apart.
    /// </summary>
    /// <remarks>
    ///     The dots are drawn above all wires but below the nodes, in the color of the wire that owns the
    ///     branch point.
    /// </remarks>
    /// <param name="dots">The junction points, each with the wire that owns the dot and the dot's radius.</param>
    private void AddJunctions(List<(Point Point, WireDraw Owner, double Radius)> dots)
    {
        if (!_theme.WireJunctionDots || _theme.WireJunctionRadius <= 0)
        {
            return;
        }

        foreach (var (point, owner, radius) in dots)
        {
            var dot = new Rect(point.X - radius, point.Y - radius, 2 * radius, 2 * radius);
            if (IsVisible(dot))
            {
                _primitives.Add(new RectPrimitive(dot, owner.Color, null, 1, radius, owner.Wire));
            }
        }
    }

    /// <summary>
    ///     Adds the handles of every selected wire and the hit targets that make them, and the wire's segments, win over nodes.
    /// </summary>
    /// <remarks>
    ///     Called after all other elements so the handles are drawn on top and their hit targets are
    ///     the topmost ones: handles beat segments, and segments of a selected wire beat nodes.
    /// </remarks>
    private void AddWireHandles()
    {
        var size = _theme.WireHandleSize;
        foreach (var (wire, points, thickness) in _selectedWires)
        {
            if (!IsVisible(wire.GetBounds().Inflate(size)))
            {
                continue;
            }

            _targets.Add(new HitTarget(wire, points, thickness));

            // Bends are small squares: the route's interior points
            for (var i = 0; i < wire.Route.Count; i++)
            {
                var bend = points[i + 1];
                var area = new Rect(bend.X - (size / 2), bend.Y - (size / 2), size, size);
                _primitives.Add(new RectPrimitive(area, Color.White, _theme.SelectionColor, 1.5, 0, wire));
                _targets.Add(new HitTarget(wire, i, area.Inflate(2)));
            }

            // The ends are filled circles on the ports' anchors
            AddEndHandle(wire, points[0], HitKind.WireSourceHandle, wire.Source);
            AddEndHandle(wire, points[^1], HitKind.WireTargetHandle, wire.Target);
        }
    }

    /// <summary>
    ///     Adds the handle at one end of a selected wire.
    /// </summary>
    /// <param name="wire">The selected wire.</param>
    /// <param name="anchor">The end point of the wire.</param>
    /// <param name="kind">Whether this is the source or the target handle.</param>
    /// <param name="port">The port the end is attached to.</param>
    private void AddEndHandle(Wire wire, Point anchor, HitKind kind, Port port)
    {
        var size = _theme.WireHandleSize;
        var area = new Rect(anchor.X - (size / 2), anchor.Y - (size / 2), size, size);
        _primitives.Add(new RectPrimitive(area, _theme.SelectionColor, Color.White, 1.5, size / 2, wire));
        _targets.Add(new HitTarget(kind, wire, port, area.Inflate(2)));
    }

    /// <summary>
    ///     Computes the content bounds, adds the grid and creates the scene.
    /// </summary>
    /// <returns>The scene.</returns>
    private DiagramScene Finish()
    {
        var content = default(Rect);
        for (var i = 0; i < _primitives.Count; i++)
        {
            content = i == 0 ? _primitives[i].Bounds : content.Union(_primitives[i].Bounds);
        }

        // The grid is decoration: it covers the visible area (or the content plus a margin) and sits at the back
        if (_options.ShowGrid && _theme.GridSize > 0)
        {
            var area = _options.Bounds
                ?? (_primitives.Count == 0
                    ? new Rect(0, 0, _theme.GridSize * 20, _theme.GridSize * 20)
                    : content.Inflate(_theme.GridSize * 4));
            _primitives.Insert(0, new GridPrimitive(area, _theme.GridSize, _theme.GridDotColor));
        }

        return new DiagramScene(_page, _theme, _primitives, content, _targets, _layouts, _wirePoints, _groupLayouts);
    }

    /// <summary>
    ///     Determines whether a rectangle is within the culling bounds.
    /// </summary>
    /// <param name="area">The rectangle.</param>
    /// <returns><see langword="true"/> when no culling bounds are set or the rectangle intersects them.</returns>
    private bool IsVisible(Rect area) => _options.Bounds is not { } visible || visible.Intersects(area);

    /// <summary>
    ///     Counts how many groups enclose an element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The nesting depth; zero for top-level elements.</returns>
    private static int Depth(DiagramElement element)
    {
        var depth = 0;
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
        {
            depth++;
        }

        return depth;
    }

    /// <summary>
    ///     Adds the outline drawn around a selected or hovered element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="area">The element's outline rectangle.</param>
    /// <param name="cornerRadius">The corner radius of the element's own outline.</param>
    private void AddHighlight(DiagramElement element, Rect area, double cornerRadius)
    {
        if (_selection.Contains(element))
        {
            _primitives.Add(new RectPrimitive(area.Inflate(2), null, _theme.SelectionColor, _theme.SelectionThickness, cornerRadius + 2, element));
        }
        else if (ReferenceEquals(_options.Hover, element))
        {
            _primitives.Add(new RectPrimitive(area.Inflate(2), null, _theme.HoverColor, _theme.SelectionThickness, cornerRadius + 2, element));
        }
    }

    /// <summary>
    ///     Adds a node: its visual, its highlight and its hit targets.
    /// </summary>
    /// <param name="node">The visible node.</param>
    private void AddNode(Node node)
    {
        var layout = NodeLayout.Compute(node, _theme, _measurer);
        if (!IsVisible(layout.Bounds))
        {
            return;
        }

        _layouts[node] = layout;

        // The visual only adds primitives; collect them first so the builder can attribute them to the node
        var produced = new List<ScenePrimitive>();
        var context = new NodeVisualContext(
            node,
            layout,
            _theme,
            _measurer,
            _selection.Contains(node),
            ReferenceEquals(_options.Hover, node),
            _page.Diagram.ResolveNodeStyle(node),
            produced);
        (node.Type.Visual ?? StandardNodeVisual.Instance).Build(context);
        foreach (var primitive in produced)
        {
            primitive.Element ??= node;
            _primitives.Add(primitive);
        }

        AddHighlight(node, layout.Box, _theme.NodeCornerRadius);

        // Hit targets from lowest to highest: body, name, ports (ports must win over everything)
        _targets.Add(new HitTarget(HitKind.Node, node, null, layout.Box));
        _targets.Add(new HitTarget(HitKind.NodeName, node, null, layout.Name.Bounds));
        foreach (var port in layout.Ports)
        {
            _targets.Add(new HitTarget(HitKind.Port, node, port.Port, port.HitBounds));
        }
    }

    /// <summary>
    ///     Adds an annotation: its note box, wrapped text, highlight and, when selected, its resize handle.
    /// </summary>
    /// <param name="annotation">The visible annotation.</param>
    private void AddAnnotation(Annotation annotation)
    {
        var bounds = annotation.Bounds;
        if (!IsVisible(bounds))
        {
            return;
        }

        _primitives.Add(new RectPrimitive(bounds, annotation.Fill ?? _theme.AnnotationFill, _theme.AnnotationBorder, 1, 0, annotation));

        // Wrap the text to the inner width and keep only the lines that fit the height
        var inner = bounds.Inflate(-_theme.AnnotationPadding);
        if (inner.Width > 0 && inner.Height > 0)
        {
            var style = _theme.AnnotationTextStyle;
            var y = inner.Top;
            foreach (var line in TextWrapper.Wrap(annotation.Text, inner.Width, style, _measurer))
            {
                var height = _measurer.Measure(line, style).Height;
                if (y + height > inner.Bottom + 0.001)
                {
                    break;
                }

                if (line.Length > 0)
                {
                    _primitives.Add(new TextPrimitive(line, new Rect(inner.Left, y, inner.Width, height), style, annotation));
                }

                y += height;
            }
        }

        AddHighlight(annotation, bounds, 0);
        _targets.Add(new HitTarget(HitKind.Annotation, annotation, null, bounds));

        // The handle exists only while selected, so hit testing offers it exactly when it is drawn
        if (_selection.Contains(annotation))
        {
            var size = _theme.ResizeHandleSize;
            var handle = new Rect(bounds.Right - size, bounds.Bottom - size, size, size);
            _primitives.Add(new RectPrimitive(handle, _theme.SelectionColor, Color.White, 1, 0, annotation));
            _targets.Add(new HitTarget(HitKind.AnnotationResizeHandle, annotation, null, handle));
        }
    }

    /// <summary>
    ///     Adds a wire: its highlight, stroke, arrowhead and hit target.
    /// </summary>
    /// <param name="draw">The wire with its displayed polyline, look, junctions and hops.</param>
    private void AddWire(WireDraw draw)
    {
        var wire = draw.Wire;
        var points = draw.Points;
        var style = draw.Style;
        var color = draw.Color;
        var thickness = draw.Thickness;
        var showArrow = style?.Arrowhead ?? _theme.WireArrowheads;

        // Heavier wires get a proportionately larger head, growing at half the rate of the thickness
        var arrowScale = Math.Max(1, (thickness + _theme.WireThickness) / (2 * _theme.WireThickness));
        var arrowLength = _theme.WireArrowLength * arrowScale;
        var arrowWidth = _theme.WireArrowWidth * arrowScale;

        // The head must sit on a straight final segment; on a very short last segment it shrinks to fit
        var lastLength = WirePaths.LastSegmentLength(points);
        if (lastLength > 0 && lastLength < arrowLength)
        {
            var fit = lastLength / arrowLength;
            arrowLength *= fit;
            arrowWidth *= fit;
        }

        var box = new Rect(points[0].X, points[0].Y, 0, 0);
        foreach (var point in points)
        {
            box = box.Union(new Rect(point.X, point.Y, 0, 0));
        }

        if (!IsVisible(box.Inflate(Math.Max(thickness, arrowWidth))))
        {
            return;
        }

        _wirePoints[wire] = points;
        var arrow = showArrow ? WirePaths.Arrowhead(points, arrowLength, arrowWidth) : null;

        // The corner curves are rounded on the full polyline with room left for the arrowhead, and the stroked
        // line then stops inside the arrowhead so its flat end does not poke past the arrow tip. Corners at the
        // wire's net junctions stay square so the junction dot sits on a crisp T, and crossings of other nets hop
        var commands = WirePaths.Rounded(
            points,
            _theme.WireCornerRadius,
            arrow is null ? 0 : arrowLength,
            arrow is null ? 0 : arrowLength * 0.75,
            _theme.WireSmoothSmallJogs,
            draw.Junctions,
            draw.Hops);

        // Highlights are drawn under the wire as a wider stroke so the wire itself stays crisp on top;
        // they stay solid even when the wire is dashed so a selected wire is easy to see
        if (_selection.Contains(wire))
        {
            _primitives.Add(new PathPrimitive(commands, null, _theme.SelectionColor, thickness + 4, wire));
        }
        else if (ReferenceEquals(_options.Hover, wire))
        {
            _primitives.Add(new PathPrimitive(commands, null, _theme.HoverColor, thickness + 4, wire));
        }

        // Dashes are cut here, not in the painters, so every painter draws the same pattern
        var pattern = style?.GetEffectiveDash() ?? [];
        var stroke = pattern.Length > 0 ? WirePaths.Dashed(commands, pattern) : commands;
        _primitives.Add(new PathPrimitive(stroke, null, color, thickness, wire));

        // The arrowhead is a small filled triangle in the wire color pointing into the input's label
        if (arrow is not null)
        {
            _primitives.Add(new PathPrimitive(arrow, color, null, 1, wire));
        }

        _targets.Add(new HitTarget(wire, points, thickness));

        // A selected wire shows handles; they are added last (see AddWireHandles) so they win over nodes.
        // Wires drawn to a collapsed group use a transient route and are not editable
        if (_selection.Contains(wire) && CollapsedOwner(wire.Source.Node) is null && CollapsedOwner(wire.Target.Node) is null)
        {
            _selectedWires.Add((wire, points, thickness));
        }
    }

    /// <summary>
    ///     Computes the polyline a wire is displayed with.
    /// </summary>
    /// <remarks>
    ///     Wires between visible nodes use their stored route. A wire inside a collapsed group is
    ///     hidden (null). A wire crossing a collapsed group's boundary gets a transient route
    ///     computed now from the proxy port anchors; the wire's stored route is not touched.
    /// </remarks>
    /// <param name="wire">The wire.</param>
    /// <returns>The polyline, or null when the wire is hidden.</returns>
    private List<Point>? DisplayPoints(Wire wire)
    {
        var sourceOwner = CollapsedOwner(wire.Source.Node);
        var targetOwner = CollapsedOwner(wire.Target.Node);
        if (sourceOwner is null && targetOwner is null)
        {
            return [.. wire.GetPoints()];
        }

        // Both ends inside the same collapsed group: nothing crosses the boundary
        if (ReferenceEquals(sourceOwner, targetOwner))
        {
            return null;
        }

        var start = sourceOwner is null ? wire.Source.Anchor : _collapsed[sourceOwner].AnchorOf(wire.Source, _theme);
        var end = targetOwner is null ? wire.Target.Anchor : _collapsed[targetOwner].AnchorOf(wire.Target, _theme);
        var traffic = DisplayTraffic();
        var request = new RouteRequest
        {
            Start = start,
            End = end,
            StartDirection = PortSide.Right,
            EndDirection = PortSide.Left,
            Obstacles = (_groupObstacles ??= new GroupObstacles(_page.Groups, _theme, includeCollapsed: false))
                .For(Obstacles(), wire.Source.Node, wire.Target.Node),
            GridSize = _theme.GridSize,
            StubLength = _theme.WireStubLength,
            Net = traffic.NetOf(wire.Source, wire.Target),
            ExistingSegments = [.. traffic.Segments],
        };
        var route = _page.Diagram.Router.Route(request) ?? [];

        // The display route joins the traffic so display routes computed later keep clear of it too
        traffic.Add(wire.Source, wire.Target, start, route, end);
        return [start, .. route, end];
    }

    /// <summary>
    ///     Gets the segments display routes have to keep clear of, built on first use.
    /// </summary>
    /// <remarks>
    ///     Holds every wire drawn with its stored route (page order, hidden wires and wires attached to
    ///     collapsed groups excluded). Display routes are appended as they are computed, in page order,
    ///     so the result is deterministic. The net of a wire is its source port: for a wire leaving a
    ///     collapsed group that is the inner port the proxy stands for.
    /// </remarks>
    /// <returns>The traffic.</returns>
    private WireTraffic DisplayTraffic()
    {
        if (_traffic is not null)
        {
            return _traffic;
        }

        var traffic = new WireTraffic();
        foreach (var wire in _page.Wires.Where(w => CollapsedOwner(w.Source.Node) is null && CollapsedOwner(w.Target.Node) is null))
        {
            traffic.Add(wire.Source, wire.Target, wire.Source.Anchor, wire.Route, wire.Target.Anchor);
        }

        return _traffic = traffic;
    }

    /// <summary>
    ///     Gets the obstacle boxes for display routes: visible node boxes and collapsed group boxes, inflated by the margin, plus port label strips.
    /// </summary>
    /// <remarks>
    ///     The frames of expanded groups are not included: they depend on the wire (see <see cref="GroupObstacles"/>).
    /// </remarks>
    /// <returns>The obstacles.</returns>
    private List<Rect> Obstacles()
    {
        if (_obstacles is not null)
        {
            return _obstacles;
        }

        var list = new List<Rect>();
        foreach (var node in _page.Nodes.Where(n => !n.IsHiddenByCollapse))
        {
            list.AddRange(NodeGeometry.RoutingObstacles(node.Bounds, node.Type, _theme));
        }

        foreach (var info in _collapsed.Values)
        {
            list.AddRange(info.RoutingObstacles(_theme));
        }

        return _obstacles = list;
    }
}
