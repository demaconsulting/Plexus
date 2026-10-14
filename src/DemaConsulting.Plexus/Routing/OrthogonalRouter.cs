namespace DemaConsulting.Plexus.Routing;

/// <summary>
///     The default wire router: orthogonal wires that route around node boxes and leave and enter ports through short stubs.
/// </summary>
/// <remarks>
///     The router searches a sparse orthogonal visibility grid built from obstacle edges and the
///     two stub points with A*, charging a penalty per bend so wires prefer few, long segments.
///     Outputs are left through a stub to the right and inputs are entered through a stub from the
///     left (or whichever sides the request names). The search first runs in a window around the
///     two ports, which keeps routing one wire among hundreds of nodes well below a millisecond
///     for the usual short wire, and only widens to the whole page when that fails. When no path
///     exists at all, for instance because a port is walled in, it falls back to the
///     <see cref="SimpleRouter"/> path so a wire is always produced. When the request carries the
///     segments of existing wires, the search also charges <see cref="OverlapPenalty"/> for running
///     on top of a segment of another net (a net is every wire from the same output port) and
///     <see cref="CrossingPenalty"/> for crossing one, and offers parallel tracks beside the middle
///     channel so different nets end up side by side while wires of one net may share a trunk. The router is deterministic,
///     stateless and thread-safe.
/// </remarks>
public sealed class OrthogonalRouter : IWireRouter
{
    /// <summary>
    ///     Tolerance below which two coordinates are considered equal.
    /// </summary>
    private const double Epsilon = 1e-9;

    /// <summary>
    ///     How far beyond the two ports the first, windowed search attempt may stray.
    /// </summary>
    private const double WindowPadding = 60;

    /// <summary>
    ///     Fallback used for requests that name no meaningful route.
    /// </summary>
    private static readonly SimpleRouter Fallback = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="OrthogonalRouter"/> class with the default routing penalties.
    /// </summary>
    public OrthogonalRouter()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Gets or initializes the cost charged for every bend, measured in the same units as segment length; the default is 20.
    /// </summary>
    /// <remarks>
    ///     Larger values give straighter wires at the price of longer detours; 20 means the router accepts a detour of up to
    ///     20 diagram units to save one bend; for example <c>new OrthogonalRouter { BendPenalty = 40 }</c>.
    /// </remarks>
    public double BendPenalty { get; init; } = 20;

    /// <summary>
    ///     Gets or initializes the extra cost charged for every unit of length a wire runs on top of a segment of another net; the default is 10.
    /// </summary>
    /// <remarks>
    ///     High on purpose: sharing a track with another net makes both wires ambiguous, so the router
    ///     prefers any detour of a few bends. Set it to zero to disable net separation. Segments of
    ///     the wire's own net are never penalized, so wires leaving one output may still share a trunk.
    /// </remarks>
    public double OverlapPenalty { get; init; } = 10;

    /// <summary>
    ///     Gets or initializes the extra cost charged for crossing a segment of another net at a right angle; the default is 8.
    /// </summary>
    /// <remarks>
    ///     Small compared with <see cref="OverlapPenalty"/>: crossings are acceptable, overlaps are not.
    /// </remarks>
    public double CrossingPenalty { get; init; } = 8;

    /// <summary>
    ///     Routes one wire around the obstacles of a request with right-angle bends, leaving and entering the ports through stubs.
    /// </summary>
    /// <remarks>
    ///     Falls back to the <see cref="SimpleRouter"/> elbow when no path exists. The returned list is post-processed by the
    ///     diagram like every router's result (collinear and zero-length bends are removed).
    /// </remarks>
    /// <param name="request">The endpoints, directions, obstacles and existing wire segments.</param>
    /// <returns>The interior bend points from start to end, not including the endpoints; empty for a straight wire.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    public IReadOnlyList<Point> Route(RouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The wire really starts and ends at the ports; the search runs between the ends of the stubs
        var stub = Math.Max(0, request.StubLength);
        var startDirection = DirectionIndex(request.StartDirection);
        var endDirection = DirectionIndex(request.EndDirection);
        var from = Advance(request.Start, startDirection, stub);
        var to = Advance(request.End, endDirection, stub);

        // The wire arrives at the end stub traveling into the end node, i.e. opposite to the side it enters through
        var arrival = endDirection ^ 1;
        var foreign = ForeignSegments.Create(request.ExistingSegments, request.Net, OverlapPenalty, CrossingPenalty);
        var path = OrthogonalSearch.Find(
                request.Obstacles, from, to, startDirection, arrival, request.GridSize, BendPenalty, WindowPadding + stub, bounded: true, foreign)
            ?? OrthogonalSearch.Find(
                request.Obstacles, from, to, startDirection, arrival, request.GridSize, BendPenalty, WindowPadding + stub, bounded: false, foreign);

        // No path means the ports are enclosed; a simple elbow is better than no wire
        if (path is null)
        {
            return Fallback.Route(request);
        }

        // Join the stubs onto the path, then drop every point that is not a real bend
        var polyline = new List<Point>(path.Count + 2) { request.Start };
        polyline.AddRange(path);
        polyline.Add(request.End);
        return RemoveRedundantPoints(polyline);
    }

    /// <summary>
    ///     Converts a port side into a direction index: 0 right, 1 left, 2 down, 3 up.
    /// </summary>
    /// <param name="side">The side the wire leaves or enters through.</param>
    /// <returns>The direction index of leaving through that side.</returns>
    private static int DirectionIndex(PortSide side) => side switch
    {
        PortSide.Right => 0,
        PortSide.Left => 1,
        PortSide.Bottom => 2,
        _ => 3,
    };

    /// <summary>
    ///     Moves a point along a direction index.
    /// </summary>
    /// <param name="point">The origin.</param>
    /// <param name="direction">Direction index: 0 right, 1 left, 2 down, 3 up.</param>
    /// <param name="distance">The distance to move.</param>
    /// <returns>The moved point.</returns>
    private static Point Advance(Point point, int direction, double distance) => direction switch
    {
        0 => point.Offset(distance, 0),
        1 => point.Offset(-distance, 0),
        2 => point.Offset(0, distance),
        _ => point.Offset(0, -distance),
    };

    /// <summary>
    ///     Reduces a polyline to its real bends and returns only the interior points.
    /// </summary>
    /// <remarks>
    ///     Repeated points and points lying on a straight run between their neighbors carry no
    ///     information; removing them leaves exactly the bend points a wire stores.
    /// </remarks>
    /// <param name="polyline">The full polyline including both end points.</param>
    /// <returns>The interior bend points.</returns>
    private static List<Point> RemoveRedundantPoints(List<Point> polyline)
    {
        // First drop consecutive duplicates (a zero-length stub produces one)
        var distinct = new List<Point>(polyline.Count);
        foreach (var point in polyline)
        {
            if (distinct.Count == 0 || Math.Abs(point.X - distinct[^1].X) > Epsilon || Math.Abs(point.Y - distinct[^1].Y) > Epsilon)
            {
                distinct.Add(point);
            }
        }

        // Then keep a point only when the direction changes there
        var result = new List<Point>();
        for (var i = 1; i < distinct.Count - 1; i++)
        {
            var previous = distinct[i - 1];
            var current = distinct[i];
            var next = distinct[i + 1];
            var straightHorizontally = Math.Abs(previous.Y - current.Y) < Epsilon && Math.Abs(current.Y - next.Y) < Epsilon;
            var straightVertically = Math.Abs(previous.X - current.X) < Epsilon && Math.Abs(current.X - next.X) < Epsilon;
            if (!straightHorizontally && !straightVertically)
            {
                result.Add(current);
            }
        }

        return result;
    }
}
