using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <content>
///     Creation API: nodes, wires and annotations, plus connection validation and routing.
/// </content>
public sealed partial class DiagramPage
{
    /// <summary>
    ///     Adds a node of a registered type at a position (undoable).
    /// </summary>
    /// <remarks>
    ///     When the type declares <see cref="NodeType.CreateData"/>, the new node starts with a fresh payload from it.
    /// </remarks>
    /// <param name="typeKey">The key of a type registered in <see cref="Diagram.Types"/>.</param>
    /// <param name="x">The left edge of the node.</param>
    /// <param name="y">The top edge of the node.</param>
    /// <param name="name">The node name; null generates a unique <c>{TypeKeyLastSegment}{n}</c> name (for example <c>Source1</c>).</param>
    /// <param name="id">The element id; null generates one.</param>
    /// <returns>The new node.</returns>
    /// <exception cref="ArgumentException">Thrown when the type key is unknown, the name is invalid or duplicated, or the id is in use.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed from its diagram.</exception>
    public Node AddNode(string typeKey, double x, double y, string? name = null, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(typeKey);
        if (!Diagram.Types.TryGet(typeKey, out var type) || type is null)
        {
            throw new ArgumentException($"Unknown node type '{typeKey}'.", nameof(typeKey));
        }

        return AddNode(type, new Point(x, y), name, id);
    }

    /// <summary>
    ///     Adds a node of a registered type at a position (undoable).
    /// </summary>
    /// <remarks>
    ///     When the type declares <see cref="NodeType.CreateData"/>, the new node starts with a fresh payload from it.
    /// </remarks>
    /// <param name="type">A node type registered in <see cref="Diagram.Types"/>.</param>
    /// <param name="position">The top-left corner of the node.</param>
    /// <param name="name">The node name; null generates a unique <c>{TypeKeyLastSegment}{n}</c> name (for example <c>Source1</c>).</param>
    /// <param name="id">The element id; null generates one.</param>
    /// <returns>The new node.</returns>
    /// <exception cref="ArgumentException">Thrown when the type is not registered, the name is invalid or duplicated, or the id is in use.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed from its diagram.</exception>
    public Node AddNode(NodeType type, Point position, string? name = null, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        EnsureAttached();
        if (!Diagram.Types.TryGet(type.Key, out var registered) || !ReferenceEquals(registered, type))
        {
            throw new ArgumentException($"Node type '{type.Key}' is not registered in this diagram's type registry.", nameof(type));
        }

        EnsureFinite(position.X, nameof(position));
        EnsureFinite(position.Y, nameof(position));

        // Resolve and validate identity before creating anything
        var elementId = ResolveId(id);
        string nodeName;
        if (name is null)
        {
            nodeName = Diagram.GenerateNodeName(type);
        }
        else
        {
            nodeName = name.Trim();
            var reason = Diagram.ValidateNodeName(nodeName, null);
            if (reason is not null)
            {
                throw new ArgumentException(reason, nameof(name));
            }
        }

        var size = NodeGeometry.DefaultNodeSize(type, Diagram.Theme);
        var node = new Node(elementId, type, nodeName, new Rect(position, size));

        // New nodes start with the type's default payload (restored nodes keep what was stored instead)
        node.InitializeData(type.CreateInitialData());
        Diagram.History.Execute(new ElementPresenceCommand("Add node", this, node, add: true));
        return node;
    }

    /// <summary>
    ///     Adds a free-floating text annotation (undoable).
    /// </summary>
    /// <param name="text">The note text.</param>
    /// <param name="bounds">The note's bounding box.</param>
    /// <param name="id">The element id; null generates one.</param>
    /// <returns>The new annotation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the bounds are invalid or the id is in use.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed from its diagram.</exception>
    public Annotation AddAnnotation(string text, Rect bounds, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureAttached();
        EnsureValidBounds(bounds, nameof(bounds));
        var annotation = new Annotation(ResolveId(id), text, bounds);
        Diagram.History.Execute(new ElementPresenceCommand("Add annotation", this, annotation, add: true));
        return annotation;
    }

    /// <summary>
    ///     Determines whether two ports may be connected, without connecting them.
    /// </summary>
    /// <remarks>
    ///     The ports may be passed in either order. The default rules (the structural checks plus
    ///     <see cref="ConnectionRules.Default"/>) are:
    ///     <list type="bullet">
    ///         <item><description>one port must be an output and the other an input (direction);</description></item>
    ///         <item><description>both ports must belong to nodes on this page;</description></item>
    ///         <item><description>the two ports must be on different nodes (a node cannot feed itself);</description></item>
    ///         <item><description>the same output may not already be wired to the same input (no duplicates);</description></item>
    ///         <item><description>an input accepts one wire unless its <see cref="PortDefinition.AllowsMultipleWires"/> is set.</description></item>
    ///     </list>
    ///     Port types are NOT compared by default: an output of type <c>number</c> may feed an input of type <c>bool</c>.
    ///     Add <see cref="ConnectionRules.SameType"/> (for example <c>ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType)</c>)
    ///     to <see cref="Diagram.ConnectionRules"/> to require matching types. The structural checks always run first;
    ///     the diagram's rule is then asked with the output as source and the input as target.
    /// </remarks>
    /// <param name="a">One of the two ports (an output or an input); the order of the two does not matter.</param>
    /// <param name="b">The other port.</param>
    /// <returns>The decision; a denial always carries a reason.</returns>
    public ConnectionCheck CanConnect(Port a, Port b) => CheckConnection(a, b, null);

    /// <summary>
    ///     Determines whether an existing wire could be moved to different ports, without changing it.
    /// </summary>
    /// <remarks>
    ///     The same rules as <see cref="CanConnect"/> apply to the wire's new ports, except that the wire
    ///     itself does not count as a duplicate of its own connection, so keeping one end and moving the
    ///     other is judged on the new pair alone. A null port keeps the wire's current source or target.
    ///     Passing null for both, or the wire's current ports, is "no change" and is always allowed.
    ///     Moving the target onto an input that already holds another wire is refused unless that input
    ///     allows fan-in, while keeping the wire on its own current target is fine.
    ///     While the connection rule runs, <see cref="WireBeingReconnected"/> is the wire under check and the rule is called as
    ///     <c>(output, input)</c>: the first argument is the new (or kept) source output, the second the new (or kept) target input.
    /// </remarks>
    /// <param name="wire">The wire that would be reconnected.</param>
    /// <param name="newSource">The new output port, or null to keep the current one.</param>
    /// <param name="newTarget">The new input port, or null to keep the current one.</param>
    /// <returns>The decision; a denial always carries a reason.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="wire"/> is null.</exception>
    public ConnectionCheck CanReconnect(Wire wire, Port? newSource, Port? newTarget)
    {
        ArgumentNullException.ThrowIfNull(wire);
        if (!ReferenceEquals(wire.Page, this))
        {
            return ConnectionCheck.Denied("The wire is not on this page.");
        }

        // Each end must keep its direction: only an output can be a source and only an input a target
        var source = newSource ?? wire.Source;
        var target = newTarget ?? wire.Target;
        if (ReferenceEquals(source, wire.Source) && ReferenceEquals(target, wire.Target))
        {
            // Nothing moves, so there is nothing to judge (a host rule must not veto a wire for existing)
            return ConnectionCheck.Allowed;
        }

        if (source.Direction != PortDirection.Output || target.Direction != PortDirection.Input)
        {
            return ConnectionCheck.Denied("A wire must connect an output to an input.");
        }

        return CheckConnection(source, target, wire);
    }

    /// <summary>
    ///     Gets the wire a <see cref="CanReconnect"/> check is currently moving, or null when no reconnect is being checked.
    /// </summary>
    /// <remarks>
    ///     Set only for the duration of the call to the connection rule (<see cref="ConnectionValidator"/>), on the page the wire is on:
    ///     read it as <c>from.Node.Page?.WireBeingReconnected</c>. A rule that counts the wires of a port must leave this wire out, because it is
    ///     still attached to its old ports while it is judged; <see cref="Port.CountWiresIgnoringReconnect"/> does that for you.
    ///     It is null while <see cref="CanConnect"/> and <see cref="Connect(Port, Port, IReadOnlyList{Point}?, string?)"/> run.
    /// </remarks>
    /// <example>
    ///     A rule that allows at most two wires per output, written by hand.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///     using DemaConsulting.Plexus.Geometry;
    ///
    ///     // A rule that allows at most two wires per output; it also runs when a wire is moved, hence the check for the wire being reconnected
    ///     ConnectionValidator atMostTwo = (source, target) =&gt;
    ///     {
    ///         var moving = source.Node.Page?.WireBeingReconnected;                     // null unless a reconnect is checked
    ///         var others = source.Wires.Count(w =&gt; !ReferenceEquals(w, moving));        // ignore the wire being moved
    ///         return others &lt; 2 ? ConnectionCheck.Allowed : ConnectionCheck.Denied($"{source} already feeds two inputs.");
    ///     };
    ///     // or simply: ConnectionRules.MaxOutgoingWires(2)
    ///     </code>
    /// </example>
    public Wire? WireBeingReconnected { get; private set; }

    /// <summary>
    ///     Applies the connection rules to a pair of ports, optionally ignoring one wire when looking for duplicates.
    /// </summary>
    /// <param name="a">One port.</param>
    /// <param name="b">The other port.</param>
    /// <param name="ignored">A wire that does not count as an existing connection, or null.</param>
    /// <returns>The decision; a denial always carries a reason.</returns>
    private ConnectionCheck CheckConnection(Port a, Port b, Wire? ignored)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (!IsAttached)
        {
            return ConnectionCheck.Denied("The page is not part of a diagram.");
        }

        if (!ReferenceEquals(a.Node.Page, this) || !ReferenceEquals(b.Node.Page, this))
        {
            return ConnectionCheck.Denied("Both ports must belong to nodes on this page.");
        }

        if (a.Direction == b.Direction)
        {
            return ConnectionCheck.Denied("A wire must connect an output to an input.");
        }

        var (source, target) = Order(a, b);
        if (ReferenceEquals(source.Node, target.Node))
        {
            return ConnectionCheck.Denied("A node cannot be connected to itself.");
        }

        if (_wires.Any(w => !ReferenceEquals(w, ignored) && ReferenceEquals(w.Source, source) && ReferenceEquals(w.Target, target)))
        {
            return ConnectionCheck.Denied("These ports are already connected.");
        }

        // Structural checks passed; delegate policy to the connection rules. While a reconnect is judged the
        // wire being moved is published so rules that count wires (single-input) do not count it against itself
        var previous = WireBeingReconnected;
        WireBeingReconnected = ignored;
        ConnectionCheck hostResult;
        try
        {
            hostResult = Diagram.ConnectionRules(source, target);
        }
        finally
        {
            WireBeingReconnected = previous;
        }

        return hostResult.IsAllowed || hostResult.Reason is not null
            ? hostResult
            : ConnectionCheck.Denied("The connection is not allowed by the connection rules.");
    }

    /// <summary>
    ///     Connects two ports with a wire (undoable).
    /// </summary>
    /// <remarks>
    ///     The ports may be passed in either order; the wire always runs from the output to the input.
    ///     The connection must pass the default rules summarized on <see cref="CanConnect"/> (direction, same page, different nodes,
    ///     no duplicate wire, one wire per input unless <see cref="PortDefinition.AllowsMultipleWires"/>; matching port types only
    ///     with <see cref="ConnectionRules.SameType"/>); otherwise an <see cref="InvalidOperationException"/> carries the reason.
    ///     When <paramref name="route"/> is null the diagram's router computes the bend points
    ///     against the nodes that exist on the page at this moment: nodes added afterwards are not
    ///     avoided until the wire is re-routed (see <see cref="Wire.Reroute"/> and
    ///     <see cref="RerouteAll"/>), so when building a diagram in code add the nodes first and
    ///     connect them afterwards.
    /// </remarks>
    /// <param name="from">The output (or input) port to connect; the order is normalized so the wire always runs output to input.</param>
    /// <param name="to">The input (or output) port to connect.</param>
    /// <param name="route">The interior bend points (the stored route is normalized); null routes automatically.</param>
    /// <param name="id">The element id; null generates one.</param>
    /// <returns>The new wire.</returns>
    /// <exception cref="InvalidOperationException">Thrown, with the reason, when the connection is not allowed or the page is removed.</exception>
    /// <exception cref="ArgumentException">Thrown when the id is in use.</exception>
    public Wire Connect(Port from, Port to, IReadOnlyList<Point>? route = null, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        EnsureAttached();

        var check = CanConnect(from, to);
        if (!check.IsAllowed)
        {
            throw new InvalidOperationException(check.Reason);
        }

        var (source, target) = Order(from, to);
        var elementId = ResolveId(id);

        // A supplied route is normalized like every stored route: no collinear bends, no zero-length segments
        var points = route is null
            ? ComputeRoute(source, target)
            : RouteCleaner.Clean(source.Anchor, route, target.Anchor).ToArray();
        var wire = new Wire(elementId, source, target, points);
        Diagram.History.Execute(new ElementPresenceCommand("Connect", this, wire, add: true));
        return wire;
    }

    /// <summary>
    ///     Computes a route for a wire with the diagram's router, using current or hypothetical node bounds.
    /// </summary>
    /// <remarks>
    ///     The wire is routed around the wires that already exist on the page (apart from
    ///     <paramref name="replacing"/>), so different nets do not run on top of each other.
    /// </remarks>
    /// <param name="source">The output port.</param>
    /// <param name="target">The input port.</param>
    /// <param name="replacing">The wire being re-routed, which must not count as traffic, or null for a new wire.</param>
    /// <returns>The interior bend points.</returns>
    internal Point[] ComputeRoute(Port source, Port target, Wire? replacing = null) =>
        ComputeRoute(source, target, BuildObstacles(null), null, BuildTraffic(replacing), BuildGroupObstacles());

    /// <summary>
    ///     Computes a route for a wire with the diagram's router.
    /// </summary>
    /// <param name="source">The output port.</param>
    /// <param name="target">The input port.</param>
    /// <param name="obstacles">Prepared obstacle boxes, see <see cref="BuildObstacles"/>.</param>
    /// <param name="overrides">Hypothetical node bounds that replace the current ones, or null.</param>
    /// <param name="traffic">The wires to keep clear of, or null to route as if the page had no other wires.</param>
    /// <param name="groups">The prepared group frames, or null to route as if the page had no groups.</param>
    /// <returns>The interior bend points.</returns>
    internal Point[] ComputeRoute(
        Port source,
        Port target,
        IReadOnlyList<Rect> obstacles,
        IReadOnlyDictionary<Node, Rect>? overrides,
        WireTraffic? traffic = null,
        GroupObstacles? groups = null)
    {
        var theme = Diagram.Theme;
        var request = new RouteRequest
        {
            Start = AnchorOf(source, overrides),
            End = AnchorOf(target, overrides),
            StartDirection = PortSide.Right,
            EndDirection = PortSide.Left,
            Obstacles = groups?.For(obstacles, source.Node, target.Node) ?? obstacles,
            GridSize = theme.GridSize,
            StubLength = theme.WireStubLength,
            Net = traffic?.NetOf(source, target) ?? -1,
            ExistingSegments = traffic?.Segments ?? [],
        };

        // A router returning null is treated as "straight line" rather than crashing an interactive edit
        var route = Diagram.Router.Route(request)?.ToArray() ?? [];
        return [.. RouteCleaner.Clean(request.Start, route, request.End)];
    }

    /// <summary>
    ///     Gets the anchor of a port, honoring hypothetical node bounds.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="overrides">Hypothetical bounds that replace the current ones, or null.</param>
    /// <returns>The anchor in diagram coordinates.</returns>
    internal Point AnchorOf(Port port, IReadOnlyDictionary<Node, Rect>? overrides) =>
        NodeGeometry.PortAnchor(BoundsOf(port.Node, overrides), port.Direction, port.Index, port.ColumnCount, port.Label, Diagram.Theme);

    /// <summary>
    ///     Collects the segments of the wires on the page, as the traffic a new route has to keep clear of.
    /// </summary>
    /// <param name="excluded">A wire to leave out (the one being re-routed), or null.</param>
    /// <returns>The traffic of every other wire, in page order.</returns>
    internal WireTraffic BuildTraffic(Wire? excluded)
    {
        var traffic = new WireTraffic();
        foreach (var wire in _wires)
        {
            if (!ReferenceEquals(wire, excluded) && !wire.Source.Node.IsHiddenByCollapse && !wire.Target.Node.IsHiddenByCollapse)
            {
                traffic.Add(wire.Source, wire.Target, wire.Source.Anchor, wire.Route, wire.Target.Anchor);
            }
        }

        return traffic;
    }

    /// <summary>
    ///     Builds the obstacle list handed to routers: every visible node box inflated by the theme margin, plus the port label strips.
    /// </summary>
    /// <param name="overrides">Hypothetical node bounds that replace the current ones, or null.</param>
    /// <returns>The obstacle boxes.</returns>
    internal List<Rect> BuildObstacles(IReadOnlyDictionary<Node, Rect>? overrides)
    {
        var theme = Diagram.Theme;
        return [.. _nodes.Where(n => !n.IsHiddenByCollapse).SelectMany(n => NodeGeometry.RoutingObstacles(BoundsOf(n, overrides), n.Type, theme))];
    }

    /// <summary>
    ///     Prepares the group frames wires have to avoid (see <see cref="DiagramTheme.GroupsAreObstacles"/>).
    /// </summary>
    /// <remarks>
    ///     The frames are the current group bounds. Hypothetical node bounds are not applied: a group
    ///     whose fitted frame would change contains a moved node, and a wire attached to that node is
    ///     exempt from that group anyway.
    /// </remarks>
    /// <returns>The group frames.</returns>
    internal GroupObstacles BuildGroupObstacles() => new(_groups, Diagram.Theme, includeCollapsed: true);

    /// <summary>
    ///     Gets a node's bounds, honoring hypothetical overrides.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="overrides">Hypothetical bounds, or null.</param>
    /// <returns>The overriding bounds when present, otherwise the node's current bounds.</returns>
    private static Rect BoundsOf(Node node, IReadOnlyDictionary<Node, Rect>? overrides) =>
        overrides is not null && overrides.TryGetValue(node, out var bounds) ? bounds : node.Bounds;

    /// <summary>
    ///     Orders two ports of opposite directions as (output, input).
    /// </summary>
    /// <param name="a">One port.</param>
    /// <param name="b">The other port.</param>
    /// <returns>The source (output) and target (input).</returns>
    private static (Port Source, Port Target) Order(Port a, Port b) =>
        a.Direction == PortDirection.Output ? (a, b) : (b, a);

    /// <summary>
    ///     Returns a validated element id, generating one when none was supplied.
    /// </summary>
    /// <param name="id">The requested id, or null.</param>
    /// <returns>The id to use.</returns>
    /// <exception cref="ArgumentException">Thrown when the id is blank or already in use.</exception>
    private string ResolveId(string? id)
    {
        if (id is null)
        {
            return Guid.NewGuid().ToString("N");
        }

        Diagram.EnsureIdAvailable(id);
        return id;
    }

    /// <summary>
    ///     Throws when a coordinate is NaN or infinite.
    /// </summary>
    /// <param name="value">The coordinate.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <exception cref="ArgumentException">Thrown when the value is not finite.</exception>
    private static void EnsureFinite(double value, string paramName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentException("Coordinates must be finite numbers.", paramName);
        }
    }

    /// <summary>
    ///     Throws when a rectangle has non-finite values or a negative size.
    /// </summary>
    /// <param name="bounds">The rectangle.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <exception cref="ArgumentException">Thrown when the rectangle is invalid.</exception>
    private static void EnsureValidBounds(Rect bounds, string paramName)
    {
        if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)
            || bounds.Width < 0 || bounds.Height < 0)
        {
            throw new ArgumentException("Bounds must be finite with a non-negative size.", paramName);
        }
    }
}
