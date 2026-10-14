using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <summary>
///     A connection from an output port to an input port, drawn as a polyline through its bend points.
/// </summary>
/// <remarks>
///     A wire stores only its interior bend points; its end points always come from the current
///     port anchors, so moving a node never leaves a wire detached. Create wires with
///     <see cref="DiagramPage.Connect(Port, Port, IReadOnlyList{Point}?, string?)"/> or <see cref="Port.ConnectTo"/>.
///     <see cref="Source"/> is always an output and <see cref="Target"/> an input, whichever order the ports were
///     passed in. Not thread-safe.
///     <para>
///         Inherited from <see cref="DiagramElement"/> (the member tables of this page list only what <see cref="Wire"/> declares): <see cref="DiagramElement.Id"/>,
///         <see cref="DiagramElement.Page"/> (null while the wire is removed), <see cref="DiagramElement.Parent"/> (always null: wires are not group members),
///         <see cref="DiagramElement.IsVisible"/>, <see cref="DiagramElement.GetBounds"/> (the bounding box of the whole polyline), <see cref="DiagramElement.Remove"/>
///         and the <see cref="DiagramElement.PropertyChanged"/> event.
///     </para>
/// </remarks>
/// <example>
///     Connect two nodes, then inspect, reshape, re-route and reconnect the wire. Every step is one undo entry.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///     var a = page.AddNode("demo.Source", 0, 0, name: "A");
///     var b = page.AddNode("demo.Sink", 240, 60, name: "B");
///     var c = page.AddNode("demo.Sink", 240, 180, name: "C");
///
///     Wire wire = page.Connect("A.out", "B.in");
///     Console.WriteLine($"{wire.Source} -&gt; {wire.Target}");                  // "A.out -> B.in"
///     Console.WriteLine(wire.Route.Count);                                // interior bends only; the ends are the port anchors
///     Console.WriteLine(wire.GetPoints().Count);                          // anchors plus bends
///
///     // Replace the bends yourself (here: a straight line when both ends are level). The stored route is normalized:
///     // zero-length and collinear bends are dropped, so Route may be shorter than the list you passed in.
///     wire.SetRoute([new Point(120, 0), new Point(120, 60)]);
///
///     wire.Reroute();                                                     // discard manual edits; let the router decide again
///     wire.Reconnect(null, c.Input("in"));                                // keep the source, move the target; throws with the reason if refused
///     Console.WriteLine(wire.Target);                                     // "C.in"
///     </code>
/// </example>
public sealed partial class Wire : DiagramElement
{
    /// <summary>
    ///     The interior bend points.
    /// </summary>
    private Point[] _route;

    /// <summary>
    ///     The output port the wire starts at.
    /// </summary>
    private Port _source;

    /// <summary>
    ///     The input port the wire ends at.
    /// </summary>
    private Port _target;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Wire"/> class.
    /// </summary>
    /// <param name="id">The element id.</param>
    /// <param name="source">The output port.</param>
    /// <param name="target">The input port.</param>
    /// <param name="route">The interior bend points (copied).</param>
    internal Wire(string id, Port source, Port target, IEnumerable<Point> route)
        : base(id)
    {
        _source = source;
        _target = target;
        _route = [.. route];
    }

    /// <summary>
    ///     Gets the output port the wire starts at; it changes only through <see cref="Reconnect"/>.
    /// </summary>
    public Port Source => _source;

    /// <summary>
    ///     Gets the input port the wire ends at; it changes only through <see cref="Reconnect"/>.
    /// </summary>
    public Port Target => _target;

    /// <summary>
    ///     Gets the interior bend points from start to end; end points are not included.
    /// </summary>
    public IReadOnlyList<Point> Route => _route;

    /// <summary>
    ///     Gets the complete polyline: the source anchor, the bend points and the target anchor.
    /// </summary>
    /// <returns>The points from start to end.</returns>
    public IReadOnlyList<Point> GetPoints() => [Source.Anchor, .. _route, Target.Anchor];

    /// <summary>
    ///     Gets the bounding box of the full polyline (anchors and bend points).
    /// </summary>
    /// <returns>The bounding rectangle; it may have zero width or height for a straight wire.</returns>
    public override Rect GetBounds()
    {
        var points = GetPoints();
        var box = new Rect(points[0].X, points[0].Y, 0, 0);
        for (var i = 1; i < points.Count; i++)
        {
            box = box.Union(new Rect(points[i].X, points[i].Y, 0, 0));
        }

        return box;
    }

    /// <summary>
    ///     Replaces the bend points (undoable).
    /// </summary>
    /// <remarks>
    ///     The route is normalized before it is stored: bends that lie on a straight run between their
    ///     neighbors and zero-length segments are removed (see <see cref="RouteCleaner"/>), which never
    ///     changes the drawn shape. <see cref="Route"/> therefore may be shorter than the list passed in.
    /// </remarks>
    /// <param name="route">The new interior bend points; copied.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="route"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the wire is removed from its page.</exception>
    public void SetRoute(IReadOnlyList<Point> route)
    {
        ArgumentNullException.ThrowIfNull(route);
        StoreRoute(route, "Edit wire route");
    }

    /// <summary>
    ///     Recomputes the bend points with the diagram's router (undoable), discarding manual edits.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the wire is removed from its page.</exception>
    public void Reroute()
    {
        var page = RequirePage();
        SetRoute(page.ComputeRoute(Source, Target, this));
    }

    /// <summary>
    ///     Adds a small bump to the segment nearest a point so it can be dragged to reshape the wire (undoable).
    /// </summary>
    /// <remarks>
    ///     This is what double-clicking a wire segment does in the editor: the segment gets a short
    ///     jog of one grid step (see <see cref="RouteEditor.InsertJog"/>) and the new middle piece
    ///     becomes a normal draggable segment. The rest of the wire keeps its shape.
    /// </remarks>
    /// <param name="segmentIndex">The segment, as an index into <see cref="GetPoints"/> (see <see cref="Scene.HitResult.SegmentIndex"/>).</param>
    /// <param name="near">The place along the segment where the jog is centered.</param>
    /// <returns><see langword="true"/> when the jog was added; <see langword="false"/> when the segment cannot take one (diagonal route, bad index).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the wire is removed from its page.</exception>
    public bool AddJog(int segmentIndex, Point near)
    {
        var page = RequirePage();
        var jogged = RouteEditor.InsertJog(Source.Anchor, _route, Target.Anchor, segmentIndex, near, page.Diagram.Theme.GridSize);
        if (jogged is null)
        {
            return false;
        }

        StoreRoute(jogged, "Add wire jog");
        return true;
    }

    /// <summary>
    ///     Connects the wire to different ports (undoable), re-routing it and keeping its id.
    /// </summary>
    /// <remarks>
    ///     The new pair of ports is validated like <see cref="DiagramPage.Connect(Port, Port, IReadOnlyList{Point}?, string?)"/>
    ///     (see <see cref="DiagramPage.CanReconnect"/>); the wire itself does not count as a duplicate. A
    ///     null argument keeps that end where it is, and passing the current ports changes nothing and
    ///     records nothing. The route is recomputed by the diagram's router, so manual edits are
    ///     discarded. The whole change is one history entry.
    /// </remarks>
    /// <param name="newSource">The new output port, or null to keep the current one.</param>
    /// <param name="newTarget">The new input port, or null to keep the current one.</param>
    /// <exception cref="InvalidOperationException">Thrown, with the reason, when the new connection is not allowed or the wire is removed from its page.</exception>
    public void Reconnect(Port? newSource, Port? newTarget)
    {
        var page = RequirePage();
        var check = page.CanReconnect(this, newSource, newTarget);
        if (!check.IsAllowed)
        {
            throw new InvalidOperationException(check.Reason);
        }

        var source = newSource ?? Source;
        var target = newTarget ?? Target;
        if (ReferenceEquals(source, Source) && ReferenceEquals(target, Target))
        {
            return;
        }

        var route = page.ComputeRoute(source, target, this);
        page.Diagram.History.Execute(new ReconnectCommand(this, (Source, Target, _route), (source, target, route)));
    }

    /// <summary>
    ///     Normalizes and stores a route as one undoable step with the given description.
    /// </summary>
    /// <param name="route">The new interior bend points.</param>
    /// <param name="description">The undo description.</param>
    private void StoreRoute(IReadOnlyList<Point> route, string description)
    {
        var page = RequirePage();
        var copy = RouteCleaner.Clean(Source.Anchor, route, Target.Anchor).ToArray();
        if (_route.AsSpan().SequenceEqual(copy))
        {
            return;
        }

        page.Diagram.History.Execute(new SetValueCommand<Point[]>(
            description,
            this,
            nameof(Route),
            _route,
            copy,
            SetRouteCore));
    }

    /// <summary>
    ///     Stores the bend points and raises change notifications; used by commands.
    /// </summary>
    /// <param name="route">The bend points to store.</param>
    internal void SetRouteCore(Point[] route)
    {
        _route = route;
        NotifyChanged(nameof(Route));
    }

    /// <summary>
    ///     Stores new ports and the matching route and raises change notifications; used by <see cref="ReconnectCommand"/>.
    /// </summary>
    /// <param name="source">The output port.</param>
    /// <param name="target">The input port.</param>
    /// <param name="route">The bend points.</param>
    internal void ApplyEnds(Port source, Port target, Point[] route)
    {
        _source = source;
        _target = target;
        _route = route;
        NotifyChanged(nameof(Source));
        NotifyChanged(nameof(Target));
        NotifyChanged(nameof(Route));
    }
}
