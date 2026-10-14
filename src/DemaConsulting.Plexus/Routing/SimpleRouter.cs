namespace DemaConsulting.Plexus.Routing;

/// <summary>
///     A cheap, obstacle-ignoring router that produces a three-segment elbow (Z-shaped) wire.
/// </summary>
/// <remarks>
///     Hosts that prefer predictable, minimal wires can assign an instance to
///     <see cref="Diagram.Router"/>. When the end port lies ahead of the start port the wire leaves
///     horizontally, runs vertically at the midpoint and enters horizontally. When the end lies
///     behind the start, a five-segment loop is produced so the wire still leaves and enters through
///     short stubs. Only horizontal port sides are meaningful; top and bottom sides are treated as
///     the horizontal side with the same sign. The router is stateless and thread-safe.
/// </remarks>
public sealed class SimpleRouter : IWireRouter
{
    /// <summary>
    ///     Tolerance below which two coordinates are considered equal.
    /// </summary>
    private const double Epsilon = 1e-9;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SimpleRouter"/> class.
    /// </summary>
    public SimpleRouter()
    {
        // Nothing to initialize: the router is stateless
    }

    /// <summary>
    ///     Routes one wire as a three-segment elbow (or a five-segment loop when the end lies behind the start), ignoring obstacles.
    /// </summary>
    /// <param name="request">The endpoints, directions and stub length; obstacles are not used.</param>
    /// <returns>The interior bend points from start to end, not including the endpoints; empty when both ports are level and face each other.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    public IReadOnlyList<Point> Route(RouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Work out where the short stubs end; the elbow is routed between those points
        var stub = Math.Max(0, request.StubLength);
        var startSign = Sign(request.StartDirection);
        var endSign = Sign(request.EndDirection);
        var stubStart = request.Start.X + (startSign * stub);
        var stubEnd = request.End.X + (endSign * stub);
        var sameY = Math.Abs(request.Start.Y - request.End.Y) < Epsilon;

        // Ahead case: the end is in front of the start, so a single vertical run at the midpoint suffices
        var ahead = startSign != endSign && startSign * (stubEnd - stubStart) >= 0;
        if (ahead)
        {
            if (sameY)
            {
                return [];
            }

            var midX = SnapBetween(request.GridSize, (stubStart + stubEnd) / 2, stubStart, stubEnd);
            return [new Point(midX, request.Start.Y), new Point(midX, request.End.Y)];
        }

        // Behind case: leave through the start stub, run along a horizontal channel and enter through the end stub
        var channelY = sameY
            ? request.Start.Y + (2 * Math.Max(stub, 1))
            : Snap(request.GridSize, (request.Start.Y + request.End.Y) / 2);
        return
        [
            new Point(stubStart, request.Start.Y),
            new Point(stubStart, channelY),
            new Point(stubEnd, channelY),
            new Point(stubEnd, request.End.Y),
        ];
    }

    /// <summary>
    ///     Converts a port side into the horizontal travel sign of a wire leaving through it.
    /// </summary>
    /// <param name="side">The port side.</param>
    /// <returns>-1 for left and top, +1 for right and bottom.</returns>
    private static int Sign(PortSide side) => side is PortSide.Left or PortSide.Top ? -1 : 1;

    /// <summary>
    ///     Snaps a coordinate to the nearest grid line when snapping is enabled.
    /// </summary>
    /// <param name="grid">Grid size; zero or negative disables snapping.</param>
    /// <param name="value">The coordinate to snap.</param>
    /// <returns>The snapped coordinate.</returns>
    private static double Snap(double grid, double value) =>
        grid > 0 ? Math.Round(value / grid, MidpointRounding.AwayFromZero) * grid : value;

    /// <summary>
    ///     Snaps a coordinate to the grid only if the snapped value stays between two limits.
    /// </summary>
    /// <remarks>
    ///     Snapping must never push the vertical run outside the stubs, otherwise the wire would
    ///     double back through the node it leaves.
    /// </remarks>
    /// <param name="grid">Grid size; zero or negative disables snapping.</param>
    /// <param name="value">The coordinate to snap.</param>
    /// <param name="limitA">One limit of the allowed range.</param>
    /// <param name="limitB">The other limit of the allowed range.</param>
    /// <returns>The snapped coordinate when it stays within the limits, otherwise the original value.</returns>
    private static double SnapBetween(double grid, double value, double limitA, double limitB)
    {
        var snapped = Snap(grid, value);
        var low = Math.Min(limitA, limitB);
        var high = Math.Max(limitA, limitB);
        return snapped >= low && snapped <= high ? snapped : value;
    }
}
