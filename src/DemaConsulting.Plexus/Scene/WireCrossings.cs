namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     A place where a horizontal wire hops over a vertical wire of a different net.
/// </summary>
/// <param name="X">The X coordinate of the crossing.</param>
/// <param name="Y">The Y coordinate of the crossing, which is the Y of the hopping wire.</param>
/// <param name="Radius">The radius of the hop.</param>
internal readonly record struct WireHop(double X, double Y, double Radius);

/// <summary>
///     One wire as seen by crossing detection.
/// </summary>
/// <param name="Points">The displayed polyline, end points included.</param>
/// <param name="Net">The net the wire belongs to; wires of the same net never hop over each other.</param>
/// <param name="Thickness">The stroke thickness of the wire.</param>
/// <param name="Junctions">The junction points of the wire's net, which a hop keeps clear of.</param>
internal sealed record WireRun(IReadOnlyList<Point> Points, object Net, double Thickness, IReadOnlyList<Point> Junctions);

/// <summary>
///     Finds the crossings of wires of different nets, where the horizontal wire is drawn with a hop.
/// </summary>
/// <remarks>
///     Only horizontal and vertical segments take part. A crossing counts when the crossing point lies
///     strictly inside both segments (so never at a bend, an end or a point where a wire touches another
///     wire's corner), the vertical segment is long enough for the crossing to be clear of its own corner
///     curves, and no junction dot of either net sits at the crossing. The horizontal wire hops, so every
///     crossing is reported exactly once, to the wire that draws it.
/// </remarks>
internal static class WireCrossings
{
    /// <summary>
    ///     Tolerance below which two coordinates are the same.
    /// </summary>
    private const double Epsilon = 1e-3;

    /// <summary>
    ///     The extra room a hop leaves between its arc and the wire it crosses, in diagram units.
    /// </summary>
    private const double Gap = 1;

    /// <summary>
    ///     Finds the hops of every wire.
    /// </summary>
    /// <param name="wires">The wires in a deterministic order.</param>
    /// <param name="radius">The theme's hop radius for a wire of the reference thickness.</param>
    /// <param name="referenceThickness">The theme's wire thickness; thicker wires hop with a proportionally larger radius.</param>
    /// <param name="cornerClearance">How far from the end of a vertical segment a crossing must be, so it is not on a corner curve.</param>
    /// <param name="dotRadius">The radius of a junction dot, which a hop keeps clear of.</param>
    /// <returns>For each wire, in the same order, the hops it draws.</returns>
    public static List<WireHop>[] Find(
        IReadOnlyList<WireRun> wires,
        double radius,
        double referenceThickness,
        double cornerClearance,
        double dotRadius)
    {
        var result = new List<WireHop>[wires.Count];
        for (var w = 0; w < wires.Count; w++)
        {
            result[w] = [];
            var wire = wires[w];
            var scale = referenceThickness > 0 ? Math.Max(1, wire.Thickness / referenceThickness) : 1;
            for (var i = 0; i + 1 < wire.Points.Count; i++)
            {
                var a = wire.Points[i];
                var b = wire.Points[i + 1];
                if (Math.Abs(a.Y - b.Y) >= Epsilon || Math.Abs(a.X - b.X) < Epsilon)
                {
                    continue;
                }

                for (var o = 0; o < wires.Count; o++)
                {
                    var other = wires[o];
                    if (ReferenceEquals(other.Net, wire.Net))
                    {
                        continue;
                    }

                    for (var j = 0; j + 1 < other.Points.Count; j++)
                    {
                        var c = other.Points[j];
                        var d = other.Points[j + 1];
                        if (!IsCrossing(a, b, c, d, cornerClearance))
                        {
                            continue;
                        }

                        var hop = new WireHop(
                            c.X,
                            a.Y,
                            Math.Max(radius * scale, ((wire.Thickness + other.Thickness) / 2) + Gap));
                        if (!NearJunction(wire.Junctions, hop, dotRadius) && !AtJunction(other.Junctions, hop))
                        {
                            result[w].Add(hop);
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Determines whether a horizontal segment crosses a vertical one at a point inside both.
    /// </summary>
    /// <param name="a">One end of the horizontal segment.</param>
    /// <param name="b">The other end of the horizontal segment.</param>
    /// <param name="c">One end of the other segment.</param>
    /// <param name="d">The other end of the other segment.</param>
    /// <param name="cornerClearance">How far from the ends of the vertical segment the crossing must be.</param>
    /// <returns><see langword="true"/> for a clean interior crossing.</returns>
    private static bool IsCrossing(Point a, Point b, Point c, Point d, double cornerClearance)
    {
        if (Math.Abs(c.X - d.X) >= Epsilon || Math.Abs(c.Y - d.Y) < Epsilon)
        {
            return false;
        }

        var inside = c.X > Math.Min(a.X, b.X) + Epsilon && c.X < Math.Max(a.X, b.X) - Epsilon;
        var margin = Math.Max(cornerClearance, 2 * Epsilon);
        return inside && a.Y >= Math.Min(c.Y, d.Y) + margin && a.Y <= Math.Max(c.Y, d.Y) - margin;
    }

    /// <summary>
    ///     Determines whether a junction of the hopping wire's net lies where the hop would be drawn.
    /// </summary>
    /// <param name="junctions">The junction points.</param>
    /// <param name="hop">The hop.</param>
    /// <param name="dotRadius">The radius of a junction dot.</param>
    /// <returns><see langword="true"/> when a dot would touch the hop.</returns>
    private static bool NearJunction(IReadOnlyList<Point> junctions, WireHop hop, double dotRadius) =>
        junctions.Any(j => Math.Abs(j.Y - hop.Y) < Epsilon && Math.Abs(j.X - hop.X) < hop.Radius + dotRadius);

    /// <summary>
    ///     Determines whether a junction lies exactly at a crossing.
    /// </summary>
    /// <param name="junctions">The junction points.</param>
    /// <param name="hop">The hop.</param>
    /// <returns><see langword="true"/> when a junction is at the crossing point.</returns>
    private static bool AtJunction(IReadOnlyList<Point> junctions, WireHop hop) =>
        junctions.Any(j => Math.Abs(j.Y - hop.Y) < Epsilon && Math.Abs(j.X - hop.X) < Epsilon);
}
