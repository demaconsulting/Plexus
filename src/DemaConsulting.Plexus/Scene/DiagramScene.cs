namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     A built, painter-agnostic picture of a page: ordered drawing primitives plus the geometry needed for hit testing.
/// </summary>
/// <remarks>
///     Build scenes with <see cref="SceneBuilder.Build"/>. A scene is an immutable snapshot of the
///     page at build time; rebuild it after the model changes (hosts typically do so on
///     <see cref="Diagram.Changed"/>). Both painters draw <see cref="Primitives"/> in order, and
///     <see cref="HitTest"/> answers from the very geometry that was drawn, so pointer handling
///     can never disagree with the picture. Queries are backed by a uniform-grid spatial index.
///     A built scene is read-only and safe to read from several threads.
/// </remarks>
public sealed class DiagramScene
{
    /// <summary>
    ///     Spatial index over the primitives, by their bounds.
    /// </summary>
    private readonly SpatialIndex<ScenePrimitive> _primitiveIndex = new();

    /// <summary>
    ///     Spatial index over the hit targets, in draw order.
    /// </summary>
    private readonly SpatialIndex<HitTarget> _targetIndex = new();

    /// <summary>
    ///     The layout of each node that was built.
    /// </summary>
    private readonly Dictionary<Node, NodeLayout> _layouts;

    /// <summary>
    ///     The displayed polyline of each wire that was built.
    /// </summary>
    private readonly Dictionary<Wire, IReadOnlyList<Point>> _wirePoints;

    /// <summary>
    ///     The layout of each group that was built.
    /// </summary>
    private readonly Dictionary<Group, GroupLayout> _groupLayouts;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagramScene"/> class.
    /// </summary>
    /// <param name="page">The page the scene shows.</param>
    /// <param name="theme">The theme the scene was built with.</param>
    /// <param name="primitives">The primitives in drawing order.</param>
    /// <param name="bounds">The content bounds.</param>
    /// <param name="targets">The hit targets in drawing order.</param>
    /// <param name="layouts">The node layouts.</param>
    /// <param name="wirePoints">The displayed wire polylines.</param>
    /// <param name="groupLayouts">The group layouts.</param>
    internal DiagramScene(
        DiagramPage page,
        DiagramTheme theme,
        IReadOnlyList<ScenePrimitive> primitives,
        Rect bounds,
        IReadOnlyList<HitTarget> targets,
        Dictionary<Node, NodeLayout> layouts,
        Dictionary<Wire, IReadOnlyList<Point>> wirePoints,
        Dictionary<Group, GroupLayout> groupLayouts)
    {
        Page = page;
        Theme = theme;
        Primitives = primitives;
        Bounds = bounds;
        _layouts = layouts;
        _wirePoints = wirePoints;
        _groupLayouts = groupLayouts;
        foreach (var primitive in primitives)
        {
            _primitiveIndex.Add(primitive, primitive.Bounds);
        }

        foreach (var target in targets)
        {
            _targetIndex.Add(target, target.Area);
        }
    }

    /// <summary>
    ///     Gets the page the scene was built from.
    /// </summary>
    public DiagramPage Page { get; }

    /// <summary>
    ///     Gets the theme the scene was built with.
    /// </summary>
    public DiagramTheme Theme { get; }

    /// <summary>
    ///     Gets all primitives in drawing order (later ones paint over earlier ones).
    /// </summary>
    public IReadOnlyList<ScenePrimitive> Primitives { get; }

    /// <summary>
    ///     Gets the bounds of the content (everything except the background grid); empty for an empty page.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    ///     Finds the primitives whose bounds intersect a region.
    /// </summary>
    /// <param name="area">The region, in diagram coordinates.</param>
    /// <returns>The matching primitives in drawing order.</returns>
    public IReadOnlyList<ScenePrimitive> Query(Rect area) => _primitiveIndex.Query(area);

    /// <summary>
    ///     Determines what lies under a point, topmost element first.
    /// </summary>
    /// <remarks>
    ///     Within a node, ports win over the name and the name over the body; nodes and annotations
    ///     win over wires and wires over group frames, so exactly at a port's anchor the port wins over the wire stub that ends there.
    ///     Expanded group interiors are transparent so
    ///     only the title bar, toggle and border band of a group are hit. The annotation resize
    ///     handle is only hittable while its annotation is selected, matching what is drawn.
    ///     <c>tolerance</c> is a distance in diagram units that widens thin targets: a wire is hit when the point is
    ///     within <c>tolerance</c> of its polyline (a point 3 units off a wire hits with the default, one 30 units off does not).
    ///     Name and label rectangles come from the scene's text measurer; build the scene with the painter's measurer
    ///     (see <see cref="SceneBuilder"/>) when the hit test must agree with the pixels.
    /// </remarks>
    /// <example>
    ///     Find out what is at a position and describe it.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///     using DemaConsulting.Plexus.Geometry;
    ///     using DemaConsulting.Plexus.Scene;
    ///
    ///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
    ///     var page = diagram.Pages.Add("Main");
    ///     page.AddNode("demo.Source", 0, 0, name: "A");
    ///
    ///     var scene = SceneBuilder.Build(page);                                    // defaults: diagram theme, approximate text, grid on
    ///     var hit = scene.HitTest(new Point(40, 40), tolerance: 4);                // diagram coordinates
    ///
    ///     switch (hit.Kind)
    ///     {
    ///         case HitKind.Port:
    ///             Console.WriteLine($"port {hit.Port!.Name} of {hit.Node!.Name}");
    ///             break;
    ///         case HitKind.Node or HitKind.NodeName:
    ///             Console.WriteLine($"node {hit.Node!.Name}");
    ///             break;
    ///         case HitKind.Wire:
    ///             var segment = hit.GetWireSegment();                              // start and end points of the hit segment
    ///             Console.WriteLine($"wire segment {hit.SegmentIndex}: {segment}");
    ///             break;
    ///         default:
    ///             Console.WriteLine(hit.IsHit ? hit.Kind.ToString() : "empty canvas");
    ///             break;
    ///     }
    ///     </code>
    /// </example>
    /// <param name="point">The point in diagram coordinates.</param>
    /// <param name="tolerance">Optional; defaults to 3. The extra reach, in diagram units, for thin targets such as wires and group borders.</param>
    /// <returns>What was hit; <see cref="HitResult.Canvas"/> when nothing was.</returns>
    public HitResult HitTest(Point point, double tolerance = 3)
    {
        var area = new Rect(point.X - tolerance, point.Y - tolerance, 2 * tolerance, 2 * tolerance);
        var candidates = _targetIndex.QueryIndexes(area);

        // Candidates are in draw order; walk backwards so the topmost target wins
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            if (_targetIndex.ItemAt(candidates[i]).Test(point, tolerance) is { } hit)
            {
                return hit;
            }
        }

        return HitResult.Canvas;
    }

    /// <summary>
    ///     Gets the layout the scene computed for a node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="layout">Receives the layout.</param>
    /// <returns><see langword="true"/> when the node was built (visible and not culled).</returns>
    public bool TryGetNodeLayout(Node node, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out NodeLayout? layout)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _layouts.TryGetValue(node, out layout);
    }

    /// <summary>
    ///     Gets the layout the scene computed for a group.
    /// </summary>
    /// <remarks>
    ///     The layout carries the frame, title bar and collapse-toggle rectangles (or, for a collapsed
    ///     group, the box and its proxy ports) exactly as drawn and hit-tested.
    /// </remarks>
    /// <param name="group">The group.</param>
    /// <param name="layout">Receives the layout.</param>
    /// <returns><see langword="true"/> when the group was built (visible, not hidden inside a collapsed group, not culled).</returns>
    public bool TryGetGroupLayout(Group group, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out GroupLayout? layout)
    {
        ArgumentNullException.ThrowIfNull(group);
        return _groupLayouts.TryGetValue(group, out layout);
    }

    /// <summary>
    ///     Gets the polyline a wire is displayed with: source anchor, bend points, target anchor.
    /// </summary>
    /// <remarks>
    ///     For wires attached to collapsed groups this is the transient display route ending at
    ///     the group's proxy ports; the wire's stored route is unaffected.
    /// </remarks>
    /// <param name="wire">The wire.</param>
    /// <param name="points">Receives the points.</param>
    /// <returns><see langword="true"/> when the wire is displayed (not hidden inside a collapsed group, not culled).</returns>
    public bool TryGetWirePoints(Wire wire, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IReadOnlyList<Point>? points)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return _wirePoints.TryGetValue(wire, out points);
    }
}
