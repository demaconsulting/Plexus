namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     One hit-testable region of a scene, recorded while the scene is built from the same geometry that is drawn.
/// </summary>
/// <remarks>
///     A target is either a rectangle, a polyline (wires), or a rectangular border band (expanded
///     group frames, whose interior must stay transparent to clicks). Targets are kept in draw
///     order so the last matching target is the topmost one.
/// </remarks>
internal sealed class HitTarget
{
    /// <summary>
    ///     The wire's own half thickness is added to the tolerance when testing polylines.
    /// </summary>
    private readonly double _halfThickness;

    /// <summary>
    ///     The polyline of a wire target, or null for rectangle targets.
    /// </summary>
    private readonly IReadOnlyList<Point>? _polyline;

    /// <summary>
    ///     Whether only the border band of <see cref="Area"/> counts as a hit.
    /// </summary>
    private readonly bool _borderOnly;

    /// <summary>
    ///     The bend index of a bend handle target, otherwise -1.
    /// </summary>
    private readonly int _bendIndex = -1;

    /// <summary>
    ///     Initializes a new rectangle hit target.
    /// </summary>
    /// <param name="kind">The kind of hit the target reports.</param>
    /// <param name="element">The element hit.</param>
    /// <param name="port">The port hit, for port targets.</param>
    /// <param name="area">The region of the target.</param>
    /// <param name="borderOnly">True when only a thin band along the border of the region counts.</param>
    public HitTarget(HitKind kind, DiagramElement element, Port? port, Rect area, bool borderOnly = false)
    {
        Kind = kind;
        Element = element;
        Port = port;
        Area = area;
        _borderOnly = borderOnly;
    }

    /// <summary>
    ///     Initializes a new polyline hit target for a wire.
    /// </summary>
    /// <param name="element">The wire.</param>
    /// <param name="polyline">The displayed polyline.</param>
    /// <param name="thickness">The stroke thickness of the wire.</param>
    public HitTarget(DiagramElement element, IReadOnlyList<Point> polyline, double thickness)
    {
        Kind = HitKind.Wire;
        Element = element;
        _polyline = polyline;
        _halfThickness = thickness / 2;

        // The broad-phase area is the polyline's bounding box
        var area = new Rect(polyline[0].X, polyline[0].Y, 0, 0);
        foreach (var point in polyline)
        {
            area = area.Union(new Rect(point.X, point.Y, 0, 0));
        }

        Area = area.Inflate(_halfThickness);
    }

    /// <summary>
    ///     Initializes a new rectangle hit target for a wire bend handle.
    /// </summary>
    /// <param name="wire">The selected wire.</param>
    /// <param name="bendIndex">The index of the bend within the wire's route.</param>
    /// <param name="area">The region of the handle.</param>
    public HitTarget(Wire wire, int bendIndex, Rect area)
    {
        Kind = HitKind.WireBend;
        Element = wire;
        Area = area;
        _bendIndex = bendIndex;
    }

    /// <summary>
    ///     Gets the kind of hit this target reports.
    /// </summary>
    public HitKind Kind { get; }

    /// <summary>
    ///     Gets the element the target belongs to.
    /// </summary>
    public DiagramElement Element { get; }

    /// <summary>
    ///     Gets the port for port targets, otherwise null.
    /// </summary>
    public Port? Port { get; }

    /// <summary>
    ///     Gets the region of the target (for polylines, the bounding box); also its spatial index bounds.
    /// </summary>
    public Rect Area { get; }

    /// <summary>
    ///     Tests a point against the target.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <param name="tolerance">The extra reach, in diagram units, for thin targets (wires and group borders).</param>
    /// <returns>The hit result, or null when the point misses.</returns>
    public HitResult? Test(Point point, double tolerance)
    {
        // Wires: find the closest segment within reach
        if (_polyline is not null)
        {
            return TestPolyline(point, tolerance + _halfThickness);
        }

        // Group frames: only a band along the border is hittable, the interior stays transparent
        if (_borderOnly)
        {
            var band = Math.Max(tolerance, 2);
            return Area.Inflate(band).Contains(point) && !Area.Inflate(-band).Contains(point)
                ? new HitResult(Kind, Element)
                : null;
        }

        return Area.Contains(point) ? new HitResult(Kind, Element, Port, BendIndex: _bendIndex) : null;
    }

    /// <summary>
    ///     Finds the segment of the polyline closest to a point within a reach.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="reach">The maximum distance from the segment.</param>
    /// <returns>A wire hit naming the closest segment, or null when no segment is within reach.</returns>
    private HitResult? TestPolyline(Point point, double reach)
    {
        var bestIndex = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i + 1 < _polyline!.Count; i++)
        {
            var distance = DistanceToSegment(point, _polyline[i], _polyline[i + 1]);
            if (distance <= reach && distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex < 0 ? null : new HitResult(HitKind.Wire, Element, null, bestIndex);
    }

    /// <summary>
    ///     Computes the distance from a point to a line segment.
    /// </summary>
    /// <param name="p">The point.</param>
    /// <param name="a">One end of the segment.</param>
    /// <param name="b">The other end of the segment.</param>
    /// <returns>The shortest distance.</returns>
    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = (dx * dx) + (dy * dy);

        // Project the point onto the segment, clamped to its ends
        var t = lengthSquared <= 0 ? 0 : Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared, 0, 1);
        var cx = a.X + (t * dx);
        var cy = a.Y + (t * dy);
        return Math.Sqrt(((p.X - cx) * (p.X - cx)) + ((p.Y - cy) * (p.Y - cy)));
    }
}
