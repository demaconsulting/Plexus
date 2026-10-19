namespace DemaConsulting.Plexus.Routing;

/// <summary>
///     Pure route-editing operations that keep a wire orthogonal: move a segment, move a bend, add a jog.
/// </summary>
/// <remarks>
///     Every operation takes the wire's end points and interior bend points (never the wire itself)
///     and returns the new, cleaned interior bend points, or null when the route cannot be edited
///     that way (a diagonal or zero-length segment is involved, or an index is out of range). That
///     keeps the geometry testable without a diagram and lets a drag recompute the route from the
///     route at the start of the drag on every pointer move. Segment indexes follow
///     <see cref="Scene.HitResult.SegmentIndex"/>: segment <c>i</c> joins point <c>i</c> and point
///     <c>i + 1</c> of the full polyline (start, bends, end). The end points are fixed ports: moving a
///     stub segment (the first or last one) inserts a short jog next to the port so the wire still
///     leaves and enters the port straight, with the first stub always running away to the right and
///     the last one arriving from the left.
/// </remarks>
public static class RouteEditor
{
    /// <summary>
    ///     Tolerance below which two coordinates are equal.
    /// </summary>
    private const double Epsilon = 1e-6;

    /// <summary>
    ///     Moves one segment perpendicular to itself, stretching its two neighbors so the wire stays orthogonal.
    /// </summary>
    /// <param name="start">The source anchor.</param>
    /// <param name="route">The interior bend points.</param>
    /// <param name="end">The target anchor.</param>
    /// <param name="segmentIndex">The segment to move, as an index into the full polyline.</param>
    /// <param name="coordinate">The new Y of a horizontal segment, or the new X of a vertical one.</param>
    /// <param name="stubLength">The length of the straight stub kept at a port when a jog has to be inserted.</param>
    /// <returns>The new interior bend points, or null when the segment cannot be moved.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="route"/> is null.</exception>
    public static IReadOnlyList<Point>? MoveSegment(Point start, IReadOnlyList<Point> route, Point end, int segmentIndex, double coordinate, double stubLength)
    {
        ArgumentNullException.ThrowIfNull(route);
        var points = Polyline(start, route, end);
        return segmentIndex < 0 || segmentIndex >= points.Count - 1
            ? null
            : ApplyMoves(points, [(segmentIndex, coordinate)], stubLength);
    }

    /// <summary>
    ///     Moves a bend point, dragging the coordinates it shares with its two neighbors along so the wire stays orthogonal.
    /// </summary>
    /// <remarks>
    ///     A bend joins a horizontal and a vertical segment. Moving it to a new position moves the
    ///     horizontal segment to the new Y and the vertical segment to the new X; whatever follows
    ///     those segments just stretches. When one of the two segments is a port stub, a jog is
    ///     inserted instead of moving the port.
    /// </remarks>
    /// <param name="start">The source anchor.</param>
    /// <param name="route">The interior bend points.</param>
    /// <param name="end">The target anchor.</param>
    /// <param name="bendIndex">The index of the bend within <paramref name="route"/>.</param>
    /// <param name="to">The new position of the bend.</param>
    /// <param name="stubLength">The length of the straight stub kept at a port when a jog has to be inserted.</param>
    /// <returns>The new interior bend points, or null when the bend cannot be moved.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="route"/> is null.</exception>
    public static IReadOnlyList<Point>? MoveBend(Point start, IReadOnlyList<Point> route, Point end, int bendIndex, Point to, double stubLength)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (bendIndex < 0 || bendIndex >= route.Count)
        {
            return null;
        }

        // Point k = bendIndex + 1 of the full polyline joins segment k - 1 and segment k, which must be perpendicular
        var points = Polyline(start, route, end);
        var k = bendIndex + 1;
        var previousHorizontal = IsHorizontal(points[k - 1], points[k]);
        var nextHorizontal = IsHorizontal(points[k], points[k + 1]);
        if (previousHorizontal == nextHorizontal)
        {
            return null;
        }

        return ApplyMoves(
            points,
            [(k - 1, previousHorizontal ? to.Y : to.X), (k, nextHorizontal ? to.Y : to.X)],
            stubLength);
    }

    /// <summary>
    ///     Inserts a small bump into a segment so the new middle piece can be dragged to reshape the wire.
    /// </summary>
    /// <remarks>
    ///     The bump is two steps of <paramref name="size"/> (four new bends): the segment leaves its
    ///     line, runs parallel one step to the right or down for a short distance and returns. The rest
    ///     of the wire is untouched. The bump is kept clear of the segment's ends and shrinks when the
    ///     segment is short.
    /// </remarks>
    /// <param name="start">The source anchor.</param>
    /// <param name="route">The interior bend points.</param>
    /// <param name="end">The target anchor.</param>
    /// <param name="segmentIndex">The segment to add the bump to, as an index into the full polyline.</param>
    /// <param name="near">The place along the segment where the bump should be centered.</param>
    /// <param name="size">The height of the bump and, at most, half its width; usually the theme's grid size.</param>
    /// <returns>The new interior bend points, or null when the segment cannot take a jog.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="route"/> is null.</exception>
    public static IReadOnlyList<Point>? InsertJog(Point start, IReadOnlyList<Point> route, Point end, int segmentIndex, Point near, double size)
    {
        ArgumentNullException.ThrowIfNull(route);
        var points = Polyline(start, route, end);
        if (segmentIndex < 0 || segmentIndex >= points.Count - 1 || size <= Epsilon || !IsEditable(points))
        {
            return null;
        }

        // Work along the segment's own axis: "along" runs with the wire, "across" is the step the bump takes
        var from = points[segmentIndex];
        var to = points[segmentIndex + 1];
        var horizontal = IsHorizontal(from, to);
        var fromAlong = horizontal ? from.X : from.Y;
        var toAlong = horizontal ? to.X : to.Y;
        var direction = toAlong >= fromAlong ? 1 : -1;
        var length = Math.Abs(toAlong - fromAlong);

        // The bump is at most two steps wide, keeps one half-width clear of the ends and shrinks on short segments
        var half = Math.Min(size / 2, length / 4);
        var low = Math.Min(fromAlong, toAlong) + (2 * half);
        var high = Math.Max(fromAlong, toAlong) - (2 * half);
        var center = Math.Clamp(horizontal ? near.X : near.Y, low, high);
        var across = horizontal ? from.Y : from.X;

        var jog = new List<Point>(4);
        foreach (var (along, side) in new[] { (center - (direction * half), 0.0), (center - (direction * half), size), (center + (direction * half), size), (center + (direction * half), 0.0) })
        {
            jog.Add(horizontal ? new Point(along, across + side) : new Point(across + side, along));
        }

        points.InsertRange(segmentIndex + 1, jog);
        return RouteCleaner.Clean(points[0], points.GetRange(1, points.Count - 2), points[^1]);
    }

    /// <summary>
    ///     Builds the full polyline from its parts.
    /// </summary>
    /// <param name="start">The source anchor.</param>
    /// <param name="route">The interior bend points.</param>
    /// <param name="end">The target anchor.</param>
    /// <returns>A new list: start, bends, end.</returns>
    private static List<Point> Polyline(Point start, IReadOnlyList<Point> route, Point end)
    {
        var points = new List<Point>(route.Count + 2) { start };
        points.AddRange(route);
        points.Add(end);
        return points;
    }

    /// <summary>
    ///     Determines whether every segment is horizontal or vertical and has a length.
    /// </summary>
    /// <param name="points">The full polyline.</param>
    /// <returns><see langword="true"/> when the polyline is a valid orthogonal route.</returns>
    private static bool IsEditable(List<Point> points)
    {
        if (points.Count < 2)
        {
            return false;
        }

        for (var i = 0; i + 1 < points.Count; i++)
        {
            var horizontal = Math.Abs(points[i].Y - points[i + 1].Y) < Epsilon;
            var vertical = Math.Abs(points[i].X - points[i + 1].X) < Epsilon;
            if (horizontal == vertical)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether a segment runs horizontally.
    /// </summary>
    /// <param name="a">One end.</param>
    /// <param name="b">The other end.</param>
    /// <returns><see langword="true"/> when both ends have the same Y.</returns>
    private static bool IsHorizontal(Point a, Point b) => Math.Abs(a.Y - b.Y) < Epsilon;

    /// <summary>
    ///     Moves one or more segments perpendicular to themselves in one go and returns the cleaned interior.
    /// </summary>
    /// <param name="original">The full polyline; not modified.</param>
    /// <param name="moves">The segments to move and their new coordinates.</param>
    /// <param name="stubLength">The stub length kept at a port when a jog is inserted.</param>
    /// <returns>The new interior bend points, or null when the route cannot be edited that way.</returns>
    private static IReadOnlyList<Point>? ApplyMoves(List<Point> original, (int Segment, double Coordinate)[] moves, double stubLength)
    {
        if (!IsEditable(original))
        {
            return null;
        }

        var last = original.Count - 1;
        var points = original.ToList();

        // Pass 1: shift the movable end points of every moved segment onto the new line
        var jogs = new List<(int Segment, double Coordinate)>();
        foreach (var (segment, requested) in moves)
        {
            var horizontal = IsHorizontal(original[segment], original[segment + 1]);

            // The neighbors must be perpendicular, otherwise stretching them would make them diagonal
            if ((segment > 0 && IsHorizontal(original[segment - 1], original[segment]) == horizontal)
                || (segment + 1 < last && IsHorizontal(original[segment + 1], original[segment + 2]) == horizontal))
            {
                return null;
            }

            var coordinate = KeepStubsClear(original, segment, horizontal, requested, stubLength);
            if (segment > 0)
            {
                points[segment] = WithCoordinate(points[segment], horizontal, coordinate);
            }

            if (segment + 1 < last)
            {
                points[segment + 1] = WithCoordinate(points[segment + 1], horizontal, coordinate);
            }

            // A port cannot move, so a segment that touches one needs a jog if its line really moved
            if ((segment == 0 || segment + 1 == last) && Math.Abs(coordinate - Coordinate(original[segment == 0 ? 0 : last], horizontal)) > Epsilon)
            {
                jogs.Add((segment, coordinate));
            }
        }

        // Pass 2: insert the jogs next to the ports, now that the neighbors have their final positions
        var result = new List<Point>(points.Count + 4);
        var before = new List<Point>();
        var after = new List<Point>();
        foreach (var (segment, coordinate) in jogs)
        {
            var horizontal = IsHorizontal(original[segment], original[segment + 1]);
            if (!AddJog(points, segment, horizontal, coordinate, stubLength, before, after))
            {
                return null;
            }
        }

        result.Add(points[0]);
        result.AddRange(before);
        for (var i = 1; i < last; i++)
        {
            result.Add(points[i]);
        }

        result.AddRange(after);
        result.Add(points[last]);
        return RouteCleaner.Clean(result[0], result.GetRange(1, result.Count - 2), result[^1]);
    }

    /// <summary>
    ///     Keeps a vertical segment next to a port stub at least one stub length away from the port.
    /// </summary>
    /// <remarks>
    ///     The first stub always leaves to the right and the last one always arrives from the left, so
    ///     a vertical segment right behind a stub must stay on the free side of the port.
    /// </remarks>
    /// <param name="points">The original full polyline.</param>
    /// <param name="segment">The segment being moved.</param>
    /// <param name="horizontal">Whether the segment is horizontal.</param>
    /// <param name="coordinate">The requested coordinate.</param>
    /// <param name="stubLength">The stub length.</param>
    /// <returns>The coordinate to use.</returns>
    private static double KeepStubsClear(List<Point> points, int segment, bool horizontal, double coordinate, double stubLength)
    {
        if (horizontal || stubLength <= 0)
        {
            return coordinate;
        }

        var last = points.Count - 1;
        if (segment == 1 && IsHorizontal(points[0], points[1]))
        {
            coordinate = Math.Max(coordinate, points[0].X + stubLength);
        }

        if (segment == last - 2 && IsHorizontal(points[last - 1], points[last]) && last - 2 >= 0)
        {
            coordinate = Math.Min(coordinate, points[last].X - stubLength);
        }

        return coordinate;
    }

    /// <summary>
    ///     Computes the points of a jog beside a port and files them before or after the interior points.
    /// </summary>
    /// <param name="points">The polyline with the moved neighbors already in place.</param>
    /// <param name="segment">The moved segment; the first, the last or the only one.</param>
    /// <param name="horizontal">Whether the segment is horizontal.</param>
    /// <param name="coordinate">The new coordinate of the segment's line.</param>
    /// <param name="stubLength">The stub length.</param>
    /// <param name="before">Receives the points to insert right after the first point.</param>
    /// <param name="after">Receives the points to insert right before the last point.</param>
    /// <returns><see langword="false"/> when there is no room for the jog.</returns>
    private static bool AddJog(
        List<Point> points,
        int segment,
        bool horizontal,
        double coordinate,
        double stubLength,
        List<Point> before,
        List<Point> after)
    {
        var last = points.Count - 1;
        var first = points[0];
        var final = points[last];
        var firstAlong = Along(first, horizontal);
        var finalAlong = Along(final, horizontal);

        if (last == 1)
        {
            // A wire without bends: two steps, one beside each port, joined by the moved middle piece
            var direction = finalAlong >= firstAlong ? 1 : -1;
            var step = Math.Min(StubStep(stubLength), Math.Abs(finalAlong - firstAlong) / 4);
            if (step <= Epsilon)
            {
                return false;
            }

            AddSourceJog(before, first, horizontal, coordinate, firstAlong + (direction * step));
            AddTargetJog(after, final, horizontal, coordinate, finalAlong - (direction * step));
            return true;
        }

        if (segment == 0)
        {
            // The jog sits at most one stub away from the source port, on the side the wire leaves to
            var gap = Along(points[1], horizontal) - firstAlong;
            var direction = horizontal ? 1 : Math.Sign(gap);
            var step = Math.Min(StubStep(stubLength), Math.Abs(gap) / 2);
            if (gap * direction <= Epsilon || step <= Epsilon)
            {
                return false;
            }

            AddSourceJog(before, first, horizontal, coordinate, firstAlong + (direction * step));
            return true;
        }

        // The last segment: the jog sits at most one stub away from the target port, on the side the wire arrives from
        var distance = finalAlong - Along(points[last - 1], horizontal);
        var approach = horizontal ? 1 : Math.Sign(distance);
        var jog = Math.Min(StubStep(stubLength), Math.Abs(distance) / 2);
        if (distance * approach <= Epsilon || jog <= Epsilon)
        {
            return false;
        }

        AddTargetJog(after, final, horizontal, coordinate, finalAlong - (approach * jog));
        return true;
    }

    /// <summary>
    ///     Gets the longest allowed jog step for a stub length; a stub length of zero or less means no limit.
    /// </summary>
    /// <param name="stubLength">The stub length.</param>
    /// <returns>The longest allowed step.</returns>
    private static double StubStep(double stubLength) => stubLength > 0 ? stubLength : double.MaxValue;

    /// <summary>
    ///     Adds the two points of a jog beside the source port.
    /// </summary>
    /// <param name="before">The list that receives the points.</param>
    /// <param name="port">The source port's point.</param>
    /// <param name="horizontal">Whether the segment is horizontal.</param>
    /// <param name="coordinate">The new coordinate of the segment's line.</param>
    /// <param name="along">The position along the segment where the jog sits.</param>
    private static void AddSourceJog(List<Point> before, Point port, bool horizontal, double coordinate, double along)
    {
        before.Add(Make(horizontal, along, Coordinate(port, horizontal)));
        before.Add(Make(horizontal, along, coordinate));
    }

    /// <summary>
    ///     Adds the two points of a jog beside the target port.
    /// </summary>
    /// <param name="after">The list that receives the points.</param>
    /// <param name="port">The target port's point.</param>
    /// <param name="horizontal">Whether the segment is horizontal.</param>
    /// <param name="coordinate">The new coordinate of the segment's line.</param>
    /// <param name="along">The position along the segment where the jog sits.</param>
    private static void AddTargetJog(List<Point> after, Point port, bool horizontal, double coordinate, double along)
    {
        after.Add(Make(horizontal, along, coordinate));
        after.Add(Make(horizontal, along, Coordinate(port, horizontal)));
    }

    /// <summary>
    ///     Gets the coordinate a segment's line is defined by: Y for horizontal, X for vertical.
    /// </summary>
    /// <param name="point">A point on the line.</param>
    /// <param name="horizontal">Whether the segment is horizontal.</param>
    /// <returns>The line coordinate.</returns>
    private static double Coordinate(Point point, bool horizontal) => horizontal ? point.Y : point.X;

    /// <summary>
    ///     Gets the coordinate along a segment: X for horizontal, Y for vertical.
    /// </summary>
    /// <param name="point">A point.</param>
    /// <param name="horizontal">Whether the segment is horizontal.</param>
    /// <returns>The coordinate along the segment.</returns>
    private static double Along(Point point, bool horizontal) => horizontal ? point.X : point.Y;

    /// <summary>
    ///     Replaces the line coordinate of a point.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="horizontal">Whether the segment is horizontal.</param>
    /// <param name="coordinate">The new line coordinate.</param>
    /// <returns>The moved point.</returns>
    private static Point WithCoordinate(Point point, bool horizontal, double coordinate) =>
        horizontal ? point with { Y = coordinate } : point with { X = coordinate };

    /// <summary>
    ///     Creates a point from its along and across coordinates.
    /// </summary>
    /// <param name="horizontal">Whether the segment is horizontal.</param>
    /// <param name="along">The coordinate along the segment.</param>
    /// <param name="across">The coordinate across the segment.</param>
    /// <returns>The point.</returns>
    private static Point Make(bool horizontal, double along, double across) =>
        horizontal ? new Point(along, across) : new Point(across, along);
}
