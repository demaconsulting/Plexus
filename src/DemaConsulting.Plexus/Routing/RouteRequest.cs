namespace DemaConsulting.Plexus.Routing;

/// <summary>
///     Everything a wire router needs to compute the interior bend points of one wire.
/// </summary>
/// <remarks>
///     A plain data holder, so routers receive an immutable snapshot of the
///     page geometry and can be tested without a diagram. Obstacles are already inflated by the
///     theme's obstacle margin and include the boxes of the wire's own end nodes, so a router must
///     leave and enter ports through stub segments instead of crossing those boxes.
/// </remarks>
/// <example>
///     Call a router directly, for example in a unit test.
///     <code>
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Routing;
///
///     var request = new RouteRequest
///     {
///         Start = new Point(0, 0),
///         End = new Point(300, 100),
///         Obstacles = [new Rect(100, -20, 80, 140)],     // one node box in the way
///         StubLength = 15,
///     };
///     IReadOnlyList&lt;Point&gt; bends = new OrthogonalRouter().Route(request);   // interior bend points only
///     </code>
/// </example>
public sealed class RouteRequest
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RouteRequest"/> class with default values: a left-to-right wire without obstacles.
    /// </summary>
    public RouteRequest()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Gets or initializes the anchor of the source (output) port where the wire starts.
    /// </summary>
    public Point Start { get; init; }

    /// <summary>
    ///     Gets or initializes the anchor of the target (input) port where the wire ends.
    /// </summary>
    public Point End { get; init; }

    /// <summary>
    ///     Gets or initializes the side the wire leaves the start node through; the default is <see cref="PortSide.Right"/> (outputs exit to the right).
    /// </summary>
    public PortSide StartDirection { get; init; } = PortSide.Right;

    /// <summary>
    ///     Gets or initializes the side the wire enters the end node through; the default is <see cref="PortSide.Left"/> (inputs are entered from the left).
    /// </summary>
    public PortSide EndDirection { get; init; } = PortSide.Left;

    /// <summary>
    ///     Gets or initializes the obstacle boxes (node boxes inflated by the obstacle margin) to route around; empty by default.
    /// </summary>
    public IReadOnlyList<Rect> Obstacles { get; init; } = [];

    /// <summary>
    ///     Gets or initializes the grid size routers may snap bend points to; zero or negative disables snapping. The default is 15.
    /// </summary>
    public double GridSize { get; init; } = 15;

    /// <summary>
    ///     Gets or initializes the identity of the net the wire being routed belongs to; -1 (the default) means the net is unknown.
    /// </summary>
    /// <remarks>
    ///     A net is every wire that starts at the same output port. Existing segments with the same
    ///     value are treated as the wire's own net and may be shared; all others should be kept clear of.
    /// </remarks>
    public int Net { get; init; } = -1;

    /// <summary>
    ///     Gets or initializes the segments of the wires that already exist, used to keep different nets apart.
    /// </summary>
    /// <remarks>
    ///     Optional: an empty list (the default) routes as if the page had no other wires. Routers
    ///     that do not care about other wires simply ignore it.
    /// </remarks>
    public IReadOnlyList<RouteSegment> ExistingSegments { get; init; } = [];

    /// <summary>
    ///     Gets or initializes the length of the straight stub a wire leaves or enters a port with; the default is 15 (the theme's <see cref="Scene.DiagramTheme.WireStubLength"/>).
    /// </summary>
    public double StubLength { get; init; } = 15;
}
