namespace DemaConsulting.Plexus.Routing;

/// <summary>
///     One straight piece of an already routed wire, tagged with the net the wire belongs to.
/// </summary>
/// <remarks>
///     Routers receive the segments of the wires that already exist (see
///     <see cref="RouteRequest.ExistingSegments"/>) so a new wire can keep clear of wires that carry
///     a different signal. A net is every wire that starts at the same output port: wires of one
///     net may share a trunk, wires of different nets should not run on top of each other.
/// </remarks>
/// <param name="Start">One end of the segment.</param>
/// <param name="End">The other end of the segment.</param>
/// <param name="Net">The identity of the net the wire belongs to; wires with equal values carry the same signal.</param>
public readonly record struct RouteSegment(Point Start, Point End, int Net);
