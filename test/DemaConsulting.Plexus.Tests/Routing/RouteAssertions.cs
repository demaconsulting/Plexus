using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Shared geometric assertions over routed wires: orthogonality and obstacle clearance.
/// </summary>
internal static class RouteAssertions
{
    /// <summary>
    ///     Tolerance for coordinate comparisons.
    /// </summary>
    private const double Epsilon = 1e-6;

    /// <summary>
    ///     Builds the full polyline of a routed wire from its request and interior points.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="route">The interior bend points.</param>
    /// <returns>Start, bend points, end.</returns>
    public static List<Point> Polyline(RouteRequest request, IReadOnlyList<Point> route) =>
        [request.Start, .. route, request.End];

    /// <summary>
    ///     Determines whether every segment of a polyline is horizontal or vertical.
    /// </summary>
    /// <param name="polyline">The polyline.</param>
    /// <returns><see langword="true"/> when all segments are axis-aligned.</returns>
    public static bool IsOrthogonal(IReadOnlyList<Point> polyline)
    {
        for (var i = 0; i + 1 < polyline.Count; i++)
        {
            var a = polyline[i];
            var b = polyline[i + 1];
            if (Math.Abs(a.X - b.X) > Epsilon && Math.Abs(a.Y - b.Y) > Epsilon)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether an axis-aligned segment passes through the open interior of a box.
    /// </summary>
    /// <param name="a">One end of the segment.</param>
    /// <param name="b">The other end of the segment.</param>
    /// <param name="box">The box.</param>
    /// <returns><see langword="true"/> when the segment enters the box interior.</returns>
    public static bool CrossesInterior(Point a, Point b, Rect box) =>
        Math.Max(a.X, b.X) > box.Left + Epsilon
        && Math.Min(a.X, b.X) < box.Right - Epsilon
        && Math.Max(a.Y, b.Y) > box.Top + Epsilon
        && Math.Min(a.Y, b.Y) < box.Bottom - Epsilon;

    /// <summary>
    ///     Determines whether any segment of a polyline passes through the interior of any obstacle.
    /// </summary>
    /// <param name="polyline">The polyline.</param>
    /// <param name="obstacles">The obstacles.</param>
    /// <returns><see langword="true"/> when some segment crosses some obstacle interior.</returns>
    public static bool CrossesAny(IReadOnlyList<Point> polyline, IReadOnlyList<Rect> obstacles)
    {
        for (var i = 0; i + 1 < polyline.Count; i++)
        {
            if (obstacles.Any(box => CrossesInterior(polyline[i], polyline[i + 1], box)))
            {
                return true;
            }
        }

        return false;
    }
}
