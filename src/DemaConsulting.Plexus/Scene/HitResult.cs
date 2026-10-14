namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     The result of hit testing a point against a <see cref="DiagramScene"/>.
/// </summary>
/// <remarks>
///     <c>SegmentIndex</c> (for <see cref="HitKind.Wire"/> hits): segment <c>i</c> joins points <c>i</c> and <c>i + 1</c> of the FULL polyline
///     (<see cref="Wire.GetPoints"/>: source anchor, every <see cref="Wire.Route"/> bend, target
///     anchor), not of <see cref="Wire.Route"/> alone. In terms of the route, segment <c>i</c> starts at
///     <c>Route[i - 1]</c> (the source anchor when <c>i == 0</c>) and ends at <c>Route[i]</c> (the
///     target anchor when <c>i == Route.Count</c>). Segment 0 and the last segment are the stubs at the
///     ports. A wire drawn to a collapsed group uses a transient polyline instead; read that from
///     <see cref="DiagramScene.TryGetWirePoints"/>. <see cref="GetWireSegment"/> does the lookup.
///     <c>BendIndex</c> (for <see cref="HitKind.WireBend"/> hits) is the index within <see cref="Wire.Route"/>; bend <c>i</c> is point <c>i + 1</c> of the full polyline.
/// </remarks>
/// <param name="Kind">What was hit.</param>
/// <param name="Element">The model element that was hit; null for <see cref="HitKind.Canvas"/>.</param>
/// <param name="Port">The port, for <see cref="HitKind.Port"/> hits; otherwise null.</param>
/// <param name="SegmentIndex">For <see cref="HitKind.Wire"/> hits, the zero-based index of the hit segment of the full wire polyline (see the remarks); otherwise -1.</param>
/// <param name="BendIndex">For <see cref="HitKind.WireBend"/> hits, the zero-based index of the bend within <see cref="Wire.Route"/>; otherwise -1.</param>
public sealed record HitResult(HitKind Kind, DiagramElement? Element = null, Port? Port = null, int SegmentIndex = -1, int BendIndex = -1)
{
    /// <summary>
    ///     Gets the result for a point over empty canvas.
    /// </summary>
    public static HitResult Canvas { get; } = new(HitKind.Canvas);

    /// <summary>
    ///     Gets the node that was hit, for node, name and port hits; otherwise null.
    /// </summary>
    public Node? Node => Element as Node;

    /// <summary>
    ///     Gets the end points of the hit wire segment.
    /// </summary>
    /// <param name="polyline">
    ///     The polyline <see cref="SegmentIndex"/> refers to, usually from <see cref="DiagramScene.TryGetWirePoints"/>;
    ///     null uses the full polyline of the hit wire, <see cref="Wire.GetPoints"/>.
    /// </param>
    /// <returns>The segment's start and end, or null when this is not a wire hit or the index is outside the polyline.</returns>
    public (Point Start, Point End)? GetWireSegment(IReadOnlyList<Point>? polyline = null)
    {
        if (Kind != HitKind.Wire || Element is not Wire wire)
        {
            return null;
        }

        var points = polyline ?? wire.GetPoints();
        return SegmentIndex >= 0 && SegmentIndex + 1 < points.Count
            ? (points[SegmentIndex], points[SegmentIndex + 1])
            : null;
    }

    /// <summary>
    ///     Gets a value indicating whether anything other than empty canvas was hit.
    /// </summary>
    public bool IsHit => Kind != HitKind.Canvas;
}
