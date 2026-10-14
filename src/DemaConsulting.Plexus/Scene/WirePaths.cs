namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Draws wire polylines as rounded orthogonal paths.
/// </summary>
/// <remarks>
///     Every interior corner is replaced by a cubic curve approximating a circular arc, so the
///     result stays a plain list of <see cref="PathCommand"/>s that every painter understands.
///     The radius shrinks at corners whose neighboring segments are short so curves never overlap.
/// </remarks>
internal static class WirePaths
{
    /// <summary>
    ///     Tolerance below which lengths and cross products count as zero.
    /// </summary>
    private const double Epsilon = 1e-9;

    /// <summary>
    ///     Control point distance of a cubic Bezier approximating a quarter circle, relative to the radius.
    /// </summary>
    private const double ArcKappa = 0.5522847498;

    /// <summary>
    ///     How many times the step height the S-curve of a small jog spans along the wire.
    /// </summary>
    private const double JogSpanFactor = 4;

    /// <summary>
    ///     Tolerance when matching a point to a junction point or a crossing to a horizontal line.
    /// </summary>
    private const double HopTolerance = 1e-3;

    /// <summary>
    ///     The straight run a hop keeps clear on each side of itself, from the ends of its line.
    /// </summary>
    private const double HopMargin = 1;

    /// <summary>
    ///     Number of straight segments a corner curve is sampled with when a wire is dashed.
    /// </summary>
    private const int CurveSegments = 8;

    /// <summary>
    ///     The most dashes one wire may be split into; finer patterns leave the wire solid.
    /// </summary>
    private const int MaxDashes = 5000;

    /// <summary>
    ///     Converts a polyline into path commands with rounded corners.
    /// </summary>
    /// <remarks>
    ///     When the wire ends in an arrowhead the caller passes how much straight run the arrowhead
    ///     needs: the last corner's radius shrinks so the curve ends at least <paramref name="endClearance"/>
    ///     before the tip, which keeps the arrowhead on a straight final segment, and the stroke itself
    ///     stops <paramref name="endTrim"/> short of the tip so its flat end hides inside the arrowhead.
    ///     Trimming the finished path, rather than the polyline before rounding, keeps the corner radius
    ///     the same as everywhere else on the wire.
    /// </remarks>
    /// <param name="points">The polyline points including both end points.</param>
    /// <param name="radius">The maximum corner radius.</param>
    /// <param name="endClearance">The straight run required between the last corner's curve and the tip.</param>
    /// <param name="endTrim">How far the stroke stops short of the tip; at most the last segment length is used.</param>
    /// <param name="smoothSmallJogs">
    ///     True to draw a step shorter than twice the radius between two parallel runs as one gentle S-curve
    ///     (see <see cref="TryAddJog"/>) instead of two tight corners that would read as a kink.
    /// </param>
    /// <param name="sharpCorners">
    ///     Points whose corner is drawn square (radius zero) and never merged into a small-jog S-curve; these are
    ///     the junction points of the wire's net, where a crisp "T" shows the branch.
    /// </param>
    /// <param name="hops">
    ///     Crossings where a horizontal run hops over a wire of another net (see <see cref="WireCrossings"/>);
    ///     a hop is only drawn where it fits inside a straight piece of the path, clear of corner curves and of
    ///     the <paramref name="endClearance"/> before the tip.
    /// </param>
    /// <returns>The path commands; empty when there are no points.</returns>
    public static List<PathCommand> Rounded(
        IReadOnlyList<Point> points,
        double radius,
        double endClearance = 0,
        double endTrim = 0,
        bool smoothSmallJogs = false,
        IReadOnlyList<Point>? sharpCorners = null,
        IReadOnlyList<WireHop>? hops = null)
    {
        // Repeated points have no direction and would make corner maths degenerate
        var distinct = new List<Point>(points.Count);
        foreach (var point in points)
        {
            if (distinct.Count == 0 || Distance(distinct[^1], point) > Epsilon)
            {
                distinct.Add(point);
            }
        }

        var commands = new List<PathCommand>(distinct.Count + 4);
        if (distinct.Count == 0)
        {
            return commands;
        }

        commands.Add(PathCommand.MoveTo(distinct[0]));
        var skip = false;
        for (var i = 1; i < distinct.Count - 1; i++)
        {
            // The second corner of a jog was already drawn with the first
            if (skip)
            {
                skip = false;
                continue;
            }

            var sharp = IsSharp(sharpCorners, distinct[i]);

            // A short step between two parallel runs becomes one S-curve that consumes both of its corners;
            // a junction corner stays square so the T reads crisply
            if (smoothSmallJogs
                && !sharp
                && !IsSharp(sharpCorners, distinct[i + 1])
                && TryAddJog(commands, distinct, i, radius, endClearance))
            {
                skip = true;
                continue;
            }

            // The last corner leaves room for the arrowhead on the final segment
            var limit = i == distinct.Count - 2
                ? Math.Min(radius, Math.Max(0, Distance(distinct[i], distinct[i + 1]) - endClearance))
                : radius;

            // A neighbor that is a square corner uses none of the shared segment, so this corner may use all of it
            AddCorner(
                commands,
                distinct[i - 1],
                distinct[i],
                distinct[i + 1],
                sharp ? 0 : limit,
                i > 1 && IsSharp(sharpCorners, distinct[i - 1]),
                i < distinct.Count - 2 && IsSharp(sharpCorners, distinct[i + 1]));
        }

        if (distinct.Count > 1)
        {
            var tip = distinct[^1];
            var previous = distinct[^2];
            var length = Distance(previous, tip);
            var cut = Math.Min(Math.Max(0, endTrim), length * 0.75);
            commands.Add(PathCommand.LineTo(cut <= Epsilon ? tip : Interpolate(tip, previous, cut / length)));

            if (hops is { Count: > 0 })
            {
                // Hops stay clear of the arrowhead: on the last piece they may not reach past the clearance before the tip
                var lastEnd = endClearance > Epsilon && length > Epsilon
                    ? Interpolate(tip, previous, Math.Min(1, endClearance / length))
                    : tip;
                return InsertHops(commands, hops, lastEnd);
            }
        }

        return commands;
    }

    /// <summary>
    ///     Measures the last segment of a polyline that has a length.
    /// </summary>
    /// <param name="points">The polyline points including both end points.</param>
    /// <returns>The length of the last non-degenerate segment; zero when there is none.</returns>
    public static double LastSegmentLength(IReadOnlyList<Point> points)
    {
        for (var i = points.Count - 2; i >= 0; i--)
        {
            // Points equal to the tip add nothing; the first point that differs from it starts the final segment
            var distance = Distance(points[i], points[^1]);
            if (distance > Epsilon)
            {
                return distance;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Splits a path of lines and curves into dashes, one open figure per dash.
    /// </summary>
    /// <remarks>
    ///     Curves are first flattened into short lines, then the path is walked from its start
    ///     alternating between the "on" and "off" lengths of the pattern. A dash that runs around a
    ///     corner stays one figure, so corners are not left open. A pattern that would produce an
    ///     unreasonable number of dashes leaves the path solid.
    /// </remarks>
    /// <param name="commands">The path commands of a single open figure.</param>
    /// <param name="pattern">The alternating on and off lengths; must have an even, non-zero count of positive lengths.</param>
    /// <returns>The dash figures as path commands; the original commands when the pattern is empty or too fine.</returns>
    public static List<PathCommand> Dashed(IReadOnlyList<PathCommand> commands, IReadOnlyList<double> pattern)
    {
        var line = Flatten(commands);
        var total = 0.0;
        for (var i = 1; i < line.Count; i++)
        {
            total += Distance(line[i - 1], line[i]);
        }

        var period = pattern.Sum();
        if (pattern.Count == 0 || line.Count < 2 || total / period * (pattern.Count / 2) > MaxDashes)
        {
            return [.. commands];
        }

        var result = new List<PathCommand>();
        var entry = 0;
        var remaining = pattern[0];
        var figureOpen = false;
        for (var i = 1; i < line.Count; i++)
        {
            var from = line[i - 1];
            var to = line[i];
            var length = Distance(from, to);
            var position = 0.0;
            while (position < length - Epsilon)
            {
                var step = Math.Min(remaining, length - position);
                var on = entry % 2 == 0;
                if (on)
                {
                    if (!figureOpen)
                    {
                        result.Add(PathCommand.MoveTo(Interpolate(from, to, position / length)));
                        figureOpen = true;
                    }

                    result.Add(PathCommand.LineTo(Interpolate(from, to, (position + step) / length)));
                }

                position += step;
                remaining -= step;
                if (remaining <= Epsilon)
                {
                    // The current pattern entry is used up: an "on" entry ends its dash, then the next entry starts
                    entry = (entry + 1) % pattern.Count;
                    remaining = pattern[entry];
                    figureOpen = false;
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Creates the path of a small filled arrowhead whose tip is the last point of a polyline.
    /// </summary>
    /// <param name="points">The polyline points including both end points.</param>
    /// <param name="length">The arrowhead length along the wire.</param>
    /// <param name="width">The arrowhead width across the wire.</param>
    /// <returns>The closed triangle path, or null when the polyline has no usable last segment.</returns>
    public static List<PathCommand>? Arrowhead(IReadOnlyList<Point> points, double length, double width)
    {
        // The heading comes from the last segment that has a length
        var tip = points.Count > 0 ? points[^1] : default;
        for (var i = points.Count - 2; i >= 0; i--)
        {
            var distance = Distance(points[i], tip);
            if (distance <= Epsilon)
            {
                continue;
            }

            var ux = (tip.X - points[i].X) / distance;
            var uy = (tip.Y - points[i].Y) / distance;
            var baseX = tip.X - (ux * length);
            var baseY = tip.Y - (uy * length);
            var half = width / 2;
            return
            [
                PathCommand.MoveTo(tip),
                PathCommand.LineTo(new Point(baseX - (uy * half), baseY + (ux * half))),
                PathCommand.LineTo(new Point(baseX + (uy * half), baseY - (ux * half))),
                PathCommand.Close(),
            ];
        }

        return null;
    }

    /// <summary>
    ///     Shortens a polyline at its end by a distance, dropping points that fall within it.
    /// </summary>
    /// <param name="points">The polyline points including both end points.</param>
    /// <param name="distance">How far to cut back, measured along the polyline.</param>
    /// <returns>The shortened polyline; the original points when it is not longer than the cut.</returns>
    public static IReadOnlyList<Point> TrimEnd(IReadOnlyList<Point> points, double distance)
    {
        var result = new List<Point>(points);
        var remaining = distance;
        while (result.Count >= 2)
        {
            var end = result[^1];
            var previous = result[^2];
            var length = Distance(previous, end);
            if (length > remaining + Epsilon)
            {
                var t = remaining / length;
                result[^1] = new Point(end.X + ((previous.X - end.X) * t), end.Y + ((previous.Y - end.Y) * t));
                return result;
            }

            // The whole last segment is inside the cut: drop it unless nothing would be left
            if (result.Count == 2)
            {
                return points;
            }

            remaining -= length;
            result.RemoveAt(result.Count - 1);
        }

        return points;
    }

    /// <summary>
    ///     Draws the two corners at <paramref name="i"/> and <paramref name="i"/> + 1 as one S-curve when they form a small jog.
    /// </summary>
    /// <remarks>
    ///     A jog is a step (the segment between the two corners) shorter than twice the corner radius that
    ///     joins two runs heading the same way, so two rounded corners would leave no straight piece between
    ///     them and read as a kink. The S-curve is one cubic centered on the step that spans about four times
    ///     the step's length along the runs, limited to the straight length the runs offer (half of a run that
    ///     is shared with another corner, the whole run at the start of the wire, and the run minus the
    ///     arrowhead clearance at the end). It is not used when the limited span would be shorter than the
    ///     step, because the curve would then be steeper than the corners it replaces.
    /// </remarks>
    /// <param name="commands">The command list to append to.</param>
    /// <param name="points">The distinct polyline points.</param>
    /// <param name="i">The index of the corner that starts the step; its neighbors must exist.</param>
    /// <param name="radius">The corner radius.</param>
    /// <param name="endClearance">The straight run required before the tip.</param>
    /// <returns>True when the S-curve was added; false when the corners must be drawn normally.</returns>
    private static bool TryAddJog(List<PathCommand> commands, List<Point> points, int i, double radius, double endClearance)
    {
        if (i + 2 >= points.Count)
        {
            return false;
        }

        var a = points[i - 1];
        var b = points[i];
        var c = points[i + 1];
        var d = points[i + 2];
        var before = Distance(a, b);
        var step = Distance(b, c);
        var after = Distance(c, d);
        if (step >= 2 * radius)
        {
            return false;
        }

        // The runs must be parallel and head the same way, and the step must be perpendicular to them
        var runX = (b.X - a.X) / before;
        var runY = (b.Y - a.Y) / before;
        var stepX = (c.X - b.X) / step;
        var stepY = (c.Y - b.Y) / step;
        var outX = (d.X - c.X) / after;
        var outY = (d.Y - c.Y) / after;
        var straight = Math.Abs((runX * stepX) + (runY * stepY)) < Epsilon
            && Math.Abs((runX * outY) - (runY * outX)) < Epsilon
            && (runX * outX) + (runY * outY) > 0;
        if (!straight)
        {
            return false;
        }

        var room = Math.Min(
            i - 1 == 0 ? before : before / 2,
            i + 2 == points.Count - 1 ? Math.Max(0, after - endClearance) : after / 2);
        var half = Math.Min(JogSpanFactor * step / 2, room);
        if (half * 2 < step)
        {
            return false;
        }

        // The corners themselves are the control points, which gives horizontal tangents at both ends of the S
        commands.Add(PathCommand.LineTo(new Point(b.X - (runX * half), b.Y - (runY * half))));
        commands.Add(PathCommand.CubicTo(b, c, new Point(c.X + (runX * half), c.Y + (runY * half))));
        return true;
    }

    /// <summary>
    ///     Adds the commands for one corner: a line up to the start of the curve, then the curve itself.
    /// </summary>
    /// <param name="commands">The command list to append to.</param>
    /// <param name="previous">The point before the corner.</param>
    /// <param name="corner">The corner point.</param>
    /// <param name="next">The point after the corner.</param>
    /// <param name="radius">The maximum corner radius; zero draws a square corner.</param>
    /// <param name="previousSquare">True when the point before is itself a square corner, so the whole incoming segment may be used.</param>
    /// <param name="nextSquare">True when the point after is itself a square corner, so the whole outgoing segment may be used.</param>
    private static void AddCorner(
        List<PathCommand> commands,
        Point previous,
        Point corner,
        Point next,
        double radius,
        bool previousSquare = false,
        bool nextSquare = false)
    {
        var incoming = Distance(previous, corner);
        var outgoing = Distance(corner, next);
        var inX = (corner.X - previous.X) / incoming;
        var inY = (corner.Y - previous.Y) / incoming;
        var outX = (next.X - corner.X) / outgoing;
        var outY = (next.Y - corner.Y) / outgoing;

        // A straight run needs no rounding; otherwise never use more than half of either neighbor segment
        // (the whole segment when the neighbor is square and uses none of it)
        var cross = (inX * outY) - (inY * outX);
        var r = Math.Min(
            radius,
            Math.Min(previousSquare ? incoming : incoming / 2, nextSquare ? outgoing : outgoing / 2));
        if (Math.Abs(cross) < Epsilon || r <= Epsilon)
        {
            commands.Add(PathCommand.LineTo(corner));
            return;
        }

        var start = new Point(corner.X - (inX * r), corner.Y - (inY * r));
        var end = new Point(corner.X + (outX * r), corner.Y + (outY * r));
        commands.Add(PathCommand.LineTo(start));
        commands.Add(PathCommand.CubicTo(
            new Point(start.X + (inX * r * ArcKappa), start.Y + (inY * r * ArcKappa)),
            new Point(end.X - (outX * r * ArcKappa), end.Y - (outY * r * ArcKappa)),
            end));
    }

    /// <summary>
    ///     Determines whether a point is one of the square-corner points.
    /// </summary>
    /// <param name="sharpCorners">The square-corner points, or null for none.</param>
    /// <param name="point">The point to look for.</param>
    /// <returns><see langword="true"/> when the point is listed.</returns>
    private static bool IsSharp(IReadOnlyList<Point>? sharpCorners, Point point) =>
        sharpCorners is not null
        && sharpCorners.Any(sharp => Math.Abs(sharp.X - point.X) < HopTolerance && Math.Abs(sharp.Y - point.Y) < HopTolerance);

    /// <summary>
    ///     Replaces the stretches of horizontal lines that cross another net with semicircular hops.
    /// </summary>
    /// <remarks>
    ///     Each horizontal line is treated on its own. The hops that lie on it are put in travel order and
    ///     those whose spans overlap (crossings closer than the sum of their radii) merge into one wider hop,
    ///     with a flat top between two quarter circles of the largest radius in the group. A hop group is
    ///     drawn only when its whole span, plus a small margin, fits inside the line; otherwise the group is
    ///     skipped and the wires simply cross, which also covers crossings too near a corner curve or the
    ///     arrowhead. Hops rise toward negative Y (up on screen).
    /// </remarks>
    /// <param name="commands">The path commands.</param>
    /// <param name="hops">The crossings.</param>
    /// <param name="lastEnd">How far along the last command hops may reach.</param>
    /// <returns>The path commands with hops.</returns>
    private static List<PathCommand> InsertHops(List<PathCommand> commands, IReadOnlyList<WireHop> hops, Point lastEnd)
    {
        var result = new List<PathCommand>(commands.Count + (hops.Count * 3));
        var current = default(Point);
        for (var k = 0; k < commands.Count; k++)
        {
            var command = commands[k];
            if (command.Kind == PathCommandKind.LineTo
                && Math.Abs(command.End.Y - current.Y) < HopTolerance
                && Math.Abs(command.End.X - current.X) > Epsilon)
            {
                var limit = k == commands.Count - 1 ? lastEnd.X : command.End.X;
                AddHoppingLine(result, current, command.End, limit, hops);
            }
            else
            {
                result.Add(command);
            }

            current = command.End;
        }

        return result;
    }

    /// <summary>
    ///     Adds a horizontal line with the hops that fit on it.
    /// </summary>
    /// <param name="result">The command list to append to.</param>
    /// <param name="from">The start of the line.</param>
    /// <param name="to">The end of the line.</param>
    /// <param name="limit">The X coordinate hops may not pass; between the start and the end of the line.</param>
    /// <param name="hops">All crossings of the wire.</param>
    private static void AddHoppingLine(List<PathCommand> result, Point from, Point to, double limit, IReadOnlyList<WireHop> hops)
    {
        var sign = to.X > from.X ? 1 : -1;
        var tLimit = sign * limit;

        // Work in "travel" coordinates t = sign * x, which grow along the direction the wire runs
        var onLine = hops
            .Where(h => Math.Abs(h.Y - from.Y) < HopTolerance && (sign * h.X) > (sign * from.X) && (sign * h.X) < tLimit)
            .OrderBy(h => sign * h.X)
            .ToList();
        var room = (sign * from.X) + HopMargin;
        var index = 0;
        while (index < onLine.Count)
        {
            // Gather the crossings whose spans overlap into one group
            var radius = onLine[index].Radius;
            var start = (sign * onLine[index].X) - radius;
            var end = (sign * onLine[index].X) + radius;
            index++;
            while (index < onLine.Count && (sign * onLine[index].X) - onLine[index].Radius < end)
            {
                radius = Math.Max(radius, onLine[index].Radius);
                end = Math.Max(end, (sign * onLine[index].X) + onLine[index].Radius);
                index++;
            }

            // A group that does not fit between the previous hop (or the line start) and the limit is skipped
            if (start < room || end > tLimit - HopMargin)
            {
                continue;
            }

            var a = sign * start;
            var b = sign * end;
            var top = from.Y - radius;
            var k = ArcKappa * radius;
            result.Add(PathCommand.LineTo(new Point(a, from.Y)));
            result.Add(PathCommand.CubicTo(
                new Point(a, from.Y - k),
                new Point(a + (sign * (radius - k)), top),
                new Point(a + (sign * radius), top)));
            if (end - start > (2 * radius) + Epsilon)
            {
                result.Add(PathCommand.LineTo(new Point(b - (sign * radius), top)));
            }

            result.Add(PathCommand.CubicTo(
                new Point(b - (sign * (radius - k)), top),
                new Point(b, from.Y - k),
                new Point(b, from.Y)));
            room = end;
        }

        result.Add(PathCommand.LineTo(to));
    }


    /// <param name="commands">The path commands.</param>
    /// <returns>The polyline points in drawing order.</returns>
    private static List<Point> Flatten(IReadOnlyList<PathCommand> commands)
    {
        var points = new List<Point>(commands.Count);
        foreach (var command in commands)
        {
            switch (command.Kind)
            {
                case PathCommandKind.MoveTo or PathCommandKind.LineTo:
                    points.Add(command.End);
                    break;
                case PathCommandKind.CubicTo when points.Count > 0:
                    var start = points[^1];
                    for (var step = 1; step <= CurveSegments; step++)
                    {
                        var t = (double)step / CurveSegments;
                        var u = 1 - t;
                        points.Add(new Point(
                            (u * u * u * start.X) + (3 * u * u * t * command.Control1.X) + (3 * u * t * t * command.Control2.X) + (t * t * t * command.End.X),
                            (u * u * u * start.Y) + (3 * u * u * t * command.Control1.Y) + (3 * u * t * t * command.Control2.Y) + (t * t * t * command.End.Y)));
                    }

                    break;
                default:
                    // Close never occurs in wire paths; any other command contributes no points
                    break;
            }
        }

        return points;
    }

    /// <summary>
    ///     Interpolates linearly between two points.
    /// </summary>
    /// <param name="a">The point at zero.</param>
    /// <param name="b">The point at one.</param>
    /// <param name="t">The fraction from <paramref name="a"/> to <paramref name="b"/>.</param>
    /// <returns>The interpolated point.</returns>
    private static Point Interpolate(Point a, Point b, double t) =>
        new(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));

    /// <summary>
    ///     Computes the distance between two points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The Euclidean distance.</returns>
    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
