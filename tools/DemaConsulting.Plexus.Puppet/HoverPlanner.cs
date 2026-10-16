using System.Globalization;
using System.Text.Json.Nodes;

namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     The options of the hover commands: how long the cursor rests on the target and where to save a screen copy.
/// </summary>
/// <param name="WaitMs">How long the cursor rests on the target before the command returns (or takes its shot), in milliseconds.</param>
/// <param name="ShotPath">Where to save a screen copy taken while the cursor rests on the target, or <see langword="null"/> for none.</param>
internal sealed record HoverOptions(int WaitMs, string? ShotPath)
{
    /// <summary>
    ///     The default rest, in milliseconds: comfortably longer than Avalonia's default tooltip delay (<c>ToolTip.ShowDelay</c>,
    ///     400 ms), so the tooltip is open when the command returns or takes its shot.
    /// </summary>
    internal const int DefaultWaitMs = 700;

    /// <summary>
    ///     Reads the <c>--wait &lt;ms&gt;</c> and <c>--shot &lt;path.png&gt;</c> options of a hover command.
    /// </summary>
    /// <param name="parsed">The parsed command.</param>
    /// <returns>The options; <see cref="DefaultWaitMs"/> when <c>--wait</c> is absent.</returns>
    /// <exception cref="PuppetException">Thrown with a usage error when <c>--wait</c> is not a non-negative integer or <c>--shot</c> has no path.</exception>
    internal static HoverOptions Parse(ParsedCommand parsed)
    {
        ArgumentNullException.ThrowIfNull(parsed);

        // The wait must be a whole, non-negative number of milliseconds
        var wait = DefaultWaitMs;
        if (parsed.HasOption("wait"))
        {
            var text = parsed.GetOption("wait");
            if (text is null || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out wait))
            {
                throw PuppetException.Usage($"--wait must be a non-negative number of milliseconds, not '{text}'.");
            }
        }

        // A shot needs somewhere to go
        var shot = parsed.GetOption("shot");
        if (parsed.HasOption("shot") && string.IsNullOrWhiteSpace(shot))
        {
            throw PuppetException.Usage("--shot needs a path, for example --shot tip.png.");
        }

        return new HoverOptions(wait, shot);
    }
}

/// <summary>
///     A rectangle in diagram units: the bounds of a node, an annotation or a group the cursor should not park on.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
internal readonly record struct DiagramBox(double X, double Y, double Width, double Height)
{
    /// <summary>
    ///     Returns a copy grown by the same amount on every side.
    /// </summary>
    /// <param name="by">The margin, in diagram units.</param>
    /// <returns>The grown box.</returns>
    internal DiagramBox Inflate(double by) => new(X - by, Y - by, Width + (2 * by), Height + (2 * by));

    /// <summary>
    ///     Tests whether a point lies inside the box.
    /// </summary>
    /// <param name="x">The diagram x coordinate.</param>
    /// <param name="y">The diagram y coordinate.</param>
    /// <returns><see langword="true"/> when the point is inside (edges included).</returns>
    internal bool Contains(double x, double y) => x >= X && x <= X + Width && y >= Y && y <= Y + Height;
}

/// <summary>
///     The pure planning behind the hover commands: where to park the cursor first, where on a wire to rest, which wire
///     joins two ports, and how a port reference names its side.
/// </summary>
/// <remarks>
///     A tooltip only opens fresh when the pointer arrives at its target from somewhere else: the editor keeps the tooltip
///     of the element it already describes and suppresses the one of an element that was just pressed. Parking the cursor
///     on empty canvas first resets both, so the hover commands always show the tooltip a user would see. Everything here
///     works in diagram units and is unit-tested; the commands convert to screen pixels with the demo's <c>geometry</c>
///     command.
/// </remarks>
internal static class HoverPlanner
{
    /// <summary>
    ///     The distances from the target at which park points are tried, nearest first, in diagram units.
    /// </summary>
    private static readonly double[] ParkDistances = [40, 80, 160, 240];

    /// <summary>
    ///     The directions in which park points are tried, as unit steps: up, down, left, right, then the diagonals.
    /// </summary>
    private static readonly (double X, double Y)[] ParkDirections =
    [
        (0, -1), (0, 1), (-1, 0), (1, 0), (-1, -1), (1, -1), (-1, 1), (1, 1),
    ];

    /// <summary>
    ///     Lists the places to try parking the cursor before hovering a target: rings of increasing distance in eight
    ///     directions around it.
    /// </summary>
    /// <param name="x">The diagram x coordinate of the target.</param>
    /// <param name="y">The diagram y coordinate of the target.</param>
    /// <returns>The candidates, best (nearest) first.</returns>
    internal static IReadOnlyList<(double X, double Y)> ParkCandidates(double x, double y) =>
        [.. ParkDistances.SelectMany(d => ParkDirections.Select(dir => (x + (dir.X * d), y + (dir.Y * d))))];

    /// <summary>
    ///     Lists the park candidates that are on empty canvas: outside every obstacle and away from every wire.
    /// </summary>
    /// <param name="x">The diagram x coordinate of the target.</param>
    /// <param name="y">The diagram y coordinate of the target.</param>
    /// <param name="obstacles">The boxes to avoid (already inflated for name labels and ports).</param>
    /// <param name="wires">The wire polylines to avoid, in diagram units.</param>
    /// <param name="clearance">The least distance from a wire, in diagram units.</param>
    /// <returns>The free candidates, best first.</returns>
    internal static IReadOnlyList<(double X, double Y)> FreeParkPoints(
        double x,
        double y,
        IReadOnlyList<DiagramBox> obstacles,
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> wires,
        double clearance = 12)
    {
        ArgumentNullException.ThrowIfNull(obstacles);
        ArgumentNullException.ThrowIfNull(wires);
        return [.. ParkCandidates(x, y).Where(c =>
            !obstacles.Any(box => box.Contains(c.X, c.Y))
            && !wires.Any(route => DistanceToPolyline(c, route) < clearance))];
    }

    /// <summary>
    ///     Chooses where to park the cursor before hovering a target: the nearest candidate on empty canvas.
    /// </summary>
    /// <param name="x">The diagram x coordinate of the target.</param>
    /// <param name="y">The diagram y coordinate of the target.</param>
    /// <param name="obstacles">The boxes to avoid (already inflated for name labels and ports).</param>
    /// <param name="wires">The wire polylines to avoid, in diagram units.</param>
    /// <param name="clearance">The least distance from a wire, in diagram units.</param>
    /// <returns>The park point, or <see langword="null"/> when every candidate is taken.</returns>
    internal static (double X, double Y)? ChooseParkPoint(
        double x,
        double y,
        IReadOnlyList<DiagramBox> obstacles,
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> wires,
        double clearance = 12) =>
        FreeParkPoints(x, y, obstacles, wires, clearance) is { Count: > 0 } free ? free[0] : null;

    /// <summary>
    ///     Picks where to rest on a wire: the midpoint of its longest drawn segment, the easiest part to hit.
    /// </summary>
    /// <param name="wireGeometry">The result of <c>geometry</c> for a wire (its <c>points</c>).</param>
    /// <returns>The point, in both coordinate systems; visible only when both ends of the segment are.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the geometry has fewer than two points.</exception>
    internal static TargetPoint WirePoint(JsonNode wireGeometry)
    {
        ArgumentNullException.ThrowIfNull(wireGeometry);
        var points = (wireGeometry["points"] as JsonArray)?.Select(GeometryReader.ReadPoint).ToList() ?? [];
        if (points.Count < 2)
        {
            throw new InvalidOperationException("the wire has no drawn segment");
        }

        // The longest segment in diagram units (zoom does not change which one it is)
        var best = Enumerable.Range(0, points.Count - 1)
            .MaxBy(i => Math.Pow(points[i + 1].DiagramX - points[i].DiagramX, 2) + Math.Pow(points[i + 1].DiagramY - points[i].DiagramY, 2));
        var (a, b) = (points[best], points[best + 1]);
        return new TargetPoint(
            (a.DiagramX + b.DiagramX) / 2,
            (a.DiagramY + b.DiagramY) / 2,
            new ScreenPoint((a.Screen.X + b.Screen.X) / 2, (a.Screen.Y + b.Screen.Y) / 2),
            a.Visible && b.Visible);
    }

    /// <summary>
    ///     Finds the wire that joins two ports in a <c>list-wires</c> result.
    /// </summary>
    /// <param name="wires">The result of <c>list-wires</c> (objects with <c>id</c>, <c>source</c> and <c>target</c> as <c>Node.port</c>).</param>
    /// <param name="source">The source port as <c>Node.port</c>; an <c>@in</c> or <c>@out</c> suffix is ignored.</param>
    /// <param name="target">The target port as <c>Node.port</c>; an <c>@in</c> or <c>@out</c> suffix is ignored.</param>
    /// <returns>The id of the first matching wire, or <see langword="null"/> when none joins them.</returns>
    internal static string? FindWireId(JsonNode? wires, string source, string target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var from = SplitPortReference(source).Path;
        var to = SplitPortReference(target).Path;
        return (wires as JsonArray)?
            .FirstOrDefault(w => string.Equals(w?["source"]?.GetValue<string>(), from, StringComparison.Ordinal)
                && string.Equals(w?["target"]?.GetValue<string>(), to, StringComparison.Ordinal))?["id"]?.GetValue<string>();
    }

    /// <summary>
    ///     Splits a port reference into its path and its optional side.
    /// </summary>
    /// <param name="reference"><c>Node.port</c>, optionally followed by <c>@in</c> or <c>@out</c>.</param>
    /// <returns>The <c>Node.port</c> path and the side after the last <c>@</c>, or <see langword="null"/> when none is given.</returns>
    internal static (string Path, string? Side) SplitPortReference(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var at = reference.LastIndexOf('@');
        return at < 0 ? (reference, null) : (reference[..at], reference[(at + 1)..]);
    }

    /// <summary>
    ///     Computes the distance from a point to a polyline.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="route">The polyline; a single point counts as a point, an empty one is infinitely far.</param>
    /// <returns>The shortest distance to any segment.</returns>
    private static double DistanceToPolyline((double X, double Y) point, IReadOnlyList<(double X, double Y)> route)
    {
        if (route.Count == 0)
        {
            return double.PositiveInfinity;
        }

        if (route.Count == 1)
        {
            return Math.Sqrt(Math.Pow(point.X - route[0].X, 2) + Math.Pow(point.Y - route[0].Y, 2));
        }

        return Enumerable.Range(0, route.Count - 1).Min(i => DistanceToSegment(point, route[i], route[i + 1]));
    }

    /// <summary>
    ///     Computes the distance from a point to a line segment.
    /// </summary>
    /// <param name="p">The point.</param>
    /// <param name="a">One end of the segment.</param>
    /// <param name="b">The other end.</param>
    /// <returns>The shortest distance.</returns>
    private static double DistanceToSegment((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        // Project the point onto the segment, clamped to its ends
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        var t = lengthSquared > 0 ? Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared, 0, 1) : 0;
        var cx = a.X + (t * dx);
        var cy = a.Y + (t * dy);
        return Math.Sqrt(Math.Pow(p.X - cx, 2) + Math.Pow(p.Y - cy, 2));
    }
}
