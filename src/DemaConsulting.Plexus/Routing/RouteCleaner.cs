namespace DemaConsulting.Plexus.Routing;

/// <summary>
///     Normalizes a wire route without changing the shape that is drawn: merges collinear bends and removes zero-length segments.
/// </summary>
/// <remarks>
///     Manual edits tend to leave bends that no longer bend (dragging a segment into line with its
///     neighbor) and segments that shrank to nothing (dragging a bend onto its neighbor). Neither is
///     visible, but both clutter the stored route and its handles, so <see cref="Wire.SetRoute"/>
///     runs every route through <see cref="Clean"/>. The cleaner never moves a point, never reorders
///     points and keeps genuine reversals (a wire that doubles back on itself) because removing them
///     would change the drawn shape. The result depends only on the input, so it is deterministic.
/// </remarks>
public static class RouteCleaner
{
    /// <summary>
    ///     Tolerance below which two coordinates, or a cross product, count as equal.
    /// </summary>
    private const double Epsilon = 1e-6;

    /// <summary>
    ///     Removes duplicate points and bends that lie on a straight run between their neighbors.
    /// </summary>
    /// <param name="start">The point the wire starts at (the source anchor); it is never part of the result.</param>
    /// <param name="route">The interior bend points.</param>
    /// <param name="end">The point the wire ends at (the target anchor); it is never part of the result.</param>
    /// <returns>The cleaned interior bend points; the same shape drawn with as few bends as possible.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="route"/> is null.</exception>
    public static IReadOnlyList<Point> Clean(Point start, IReadOnlyList<Point> route, Point end)
    {
        ArgumentNullException.ThrowIfNull(route);

        // Build the polyline incrementally: skip repeated points and pop a middle point as soon as it becomes redundant
        var line = new List<Point>(route.Count + 2) { start };
        foreach (var point in route)
        {
            Push(line, point, isEnd: false);
        }

        Push(line, end, isEnd: true);

        // The last point is the end point itself: a bend equal to it would be a zero-length final segment
        return line.Count <= 2 ? [] : line.GetRange(1, line.Count - 2);
    }

    /// <summary>
    ///     Appends a point to a polyline and removes whatever it made redundant.
    /// </summary>
    /// <param name="line">The polyline built so far; its first point is the fixed start.</param>
    /// <param name="point">The point to append.</param>
    /// <param name="isEnd">Whether the point is the fixed end point.</param>
    private static void Push(List<Point> line, Point point, bool isEnd)
    {
        // A repeated point adds a zero-length segment. An interior duplicate is simply skipped; a bend that equals
        // the end point is dropped instead, because the end point itself must stay the last point
        if (isEnd)
        {
            while (line.Count > 1 && Same(line[^1], point))
            {
                line.RemoveAt(line.Count - 1);
            }
        }
        else if (Same(line[^1], point))
        {
            return;
        }

        line.Add(point);

        // The middle of the last three points is redundant when it lies on the straight run between them
        while (line.Count >= 3 && IsRedundant(line[^3], line[^2], line[^1]))
        {
            line.RemoveAt(line.Count - 2);
        }
    }

    /// <summary>
    ///     Determines whether a middle point lies on the straight run from the first to the last point.
    /// </summary>
    /// <param name="a">The point before.</param>
    /// <param name="b">The middle point.</param>
    /// <param name="c">The point after.</param>
    /// <returns><see langword="true"/> when the three points are collinear and travel in the same direction.</returns>
    private static bool IsRedundant(Point a, Point b, Point c)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var bcx = c.X - b.X;
        var bcy = c.Y - b.Y;

        // Collinear (no turn) and not a reversal (the dot product is positive)
        var cross = (abx * bcy) - (aby * bcx);
        var dot = (abx * bcx) + (aby * bcy);
        return Math.Abs(cross) <= Epsilon * Math.Max(1, Math.Abs(dot)) && dot > 0;
    }

    /// <summary>
    ///     Determines whether two points coincide within the tolerance.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns><see langword="true"/> when both coordinates agree.</returns>
    private static bool Same(Point a, Point b) => Math.Abs(a.X - b.X) <= Epsilon && Math.Abs(a.Y - b.Y) <= Epsilon;
}
