using System.Diagnostics.CodeAnalysis;

namespace DemaConsulting.Plexus.Routing;

/// <summary>
///     One orthogonal path search: a sparse visibility grid over a set of obstacles explored with A*.
/// </summary>
/// <remarks>
///     The grid is "sparse" because its lines are only the coordinates that can matter - obstacle
///     edges, the two port stub points, the middle of the two stub points and the borders of the
///     search window - instead of every grid step. Grid cells are never materialized: whether a
///     step between two neighboring grid lines is free is answered lazily from the obstacles
///     covering that column or row, so the cost of a search follows the part of the diagram it
///     actually explores. A search state is a grid intersection plus the direction of travel, which
///     lets the cost function charge a penalty for every bend. Obstacles are open regions: a path
///     may run along an obstacle edge but never through its interior. The search is fully
///     deterministic (ties are broken by insertion order). Instances are single-use and not
///     thread-safe; <see cref="OrthogonalRouter"/> creates one per attempt.
/// </remarks>
internal sealed class OrthogonalSearch
{
    /// <summary>
    ///     Tolerance below which two coordinates are the same grid line.
    /// </summary>
    private const double Epsilon = 1e-6;

    /// <summary>
    ///     Weight of the tie-break term that prefers bends near the middle of the two end points.
    /// </summary>
    /// <remarks>
    ///     Tiny on purpose: it only chooses between otherwise equal routes (for example a Z shape
    ///     whose vertical run is centered over an L shape hugging the source node).
    /// </remarks>
    private const double CenterBias = 1e-3;

    /// <summary>
    ///     How far apart two parallel runs of different nets may be and still count as running on top of each other.
    /// </summary>
    /// <remarks>
    ///     Wires are a few units thick, so a run one or two units beside another net's run still reads as
    ///     one line; the overlap penalty applies to every grid line within this distance of a foreign segment.
    ///     Crossings are unaffected.
    /// </remarks>
    private const double NearCollinearTolerance = 4;

    /// <summary>
    ///     How far a foreign segment is stretched at both ends when looking for runs that line up with it.
    /// </summary>
    /// <remarks>
    ///     Two runs of different nets on the same track that stop and start a port pitch apart read as
    ///     one wire with a kink, so the overlap penalty also applies a little beyond the segment's ends.
    /// </remarks>
    private const double NearCollinearReach = 20;

    /// <summary>
    ///     How many parallel tracks on each side of the middle channel are offered when other nets are around.
    /// </summary>
    private const int ChannelAlternatives = 3;

    /// <summary>
    ///     The track spacing used for offsets when the request disables the grid.
    /// </summary>
    private const double FallbackTrackStep = 10;

    /// <summary>
    ///     Horizontal step of each direction index: right, left, down, up.
    /// </summary>
    private static readonly int[] StepX = [1, -1, 0, 0];

    /// <summary>
    ///     Vertical step of each direction index: right, left, down, up.
    /// </summary>
    private static readonly int[] StepY = [0, 0, 1, -1];

    /// <summary>
    ///     The sorted, de-duplicated x coordinates of the vertical grid lines.
    /// </summary>
    private readonly double[] _xs;

    /// <summary>
    ///     The sorted, de-duplicated y coordinates of the horizontal grid lines.
    /// </summary>
    private readonly double[] _ys;

    /// <summary>
    ///     The obstacles that matter for this search (those inside the window, adjusted to leave the end points free).
    /// </summary>
    private readonly Rect[] _boxes;

    /// <summary>
    ///     Lazily built lists of the obstacles covering each column between two vertical grid lines.
    /// </summary>
    private readonly int[]?[] _columns;

    /// <summary>
    ///     Lazily built lists of the obstacles covering each row between two horizontal grid lines.
    /// </summary>
    private readonly int[]?[] _rows;

    /// <summary>
    ///     The point the path starts from (the end of the start stub).
    /// </summary>
    private readonly Point _from;

    /// <summary>
    ///     The point the path must reach (the beginning of the end stub).
    /// </summary>
    private readonly Point _to;

    /// <summary>
    ///     The direction index the wire initially travels in.
    /// </summary>
    private readonly int _startDirection;

    /// <summary>
    ///     The direction index the wire must be traveling in when it reaches <see cref="_to"/>.
    /// </summary>
    private readonly int _arrivalDirection;

    /// <summary>
    ///     The cost charged for each bend.
    /// </summary>
    private readonly double _bendPenalty;

    /// <summary>
    ///     The segments of other nets to keep clear of, or null when the search ignores other wires.
    /// </summary>
    private readonly ForeignSegments? _foreign;

    /// <summary>
    ///     The horizontal foreign segments as (low x, high x), by the index of the grid row they lie on.
    /// </summary>
    private readonly Dictionary<int, List<(double Low, double High)>> _rowSegments = [];

    /// <summary>
    ///     The vertical foreign segments as (low y, high y), by the index of the grid column they lie on.
    /// </summary>
    private readonly Dictionary<int, List<(double Low, double High)>> _columnSegments = [];

    /// <summary>
    ///     The horizontal foreign segments by every grid row close enough to look like the same track (see <see cref="NearCollinearTolerance"/>).
    /// </summary>
    private readonly Dictionary<int, List<(double Low, double High)>> _rowAlong = [];

    /// <summary>
    ///     The vertical foreign segments by every grid column close enough to look like the same track (see <see cref="NearCollinearTolerance"/>).
    /// </summary>
    private readonly Dictionary<int, List<(double Low, double High)>> _columnAlong = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="OrthogonalSearch"/> class.
    /// </summary>
    /// <param name="obstacles">All obstacle boxes of the request.</param>
    /// <param name="from">Where the path starts (end of the start stub).</param>
    /// <param name="to">Where the path ends (beginning of the end stub).</param>
    /// <param name="startDirection">Direction index the wire initially travels in.</param>
    /// <param name="arrivalDirection">Direction index the wire must be traveling in on arrival.</param>
    /// <param name="gridSize">Grid size used to place the middle channels; zero or negative disables snapping.</param>
    /// <param name="bendPenalty">Cost of one bend.</param>
    /// <param name="window">The region the search is confined to.</param>
    /// <param name="foreign">The segments of other nets to keep clear of, or null.</param>
    private OrthogonalSearch(
        IReadOnlyList<Rect> obstacles,
        Point from,
        Point to,
        int startDirection,
        int arrivalDirection,
        double gridSize,
        double bendPenalty,
        Rect window,
        ForeignSegments? foreign)
    {
        _from = from;
        _to = to;
        _startDirection = startDirection;
        _arrivalDirection = arrivalDirection;
        _bendPenalty = bendPenalty;
        _foreign = foreign;
        _boxes = SelectObstacles(obstacles, window, from, to);

        // Collect the interesting coordinates: window borders, both end points, the channel
        // between them and every obstacle edge inside the window
        var xs = new List<double> { window.Left, window.Right, from.X, to.X };
        var ys = new List<double> { window.Top, window.Bottom, from.Y, to.Y };
        var alternatives = foreign is null ? 0 : ChannelAlternatives;
        AddChannel(xs, from.X, to.X, gridSize, alternatives);
        AddChannel(ys, from.Y, to.Y, gridSize, alternatives);
        foreach (var box in _boxes)
        {
            AddWithin(xs, box.Left, window.Left, window.Right);
            AddWithin(xs, box.Right, window.Left, window.Right);
            AddWithin(ys, box.Top, window.Top, window.Bottom);
            AddWithin(ys, box.Bottom, window.Top, window.Bottom);
        }

        // Lines beside the foreign segments give the search free tracks to move onto
        if (foreign is not null)
        {
            AddForeignLines(xs, ys, foreign, window, gridSize > 0 ? gridSize : FallbackTrackStep);
        }

        _xs = Compress(xs);
        _ys = Compress(ys);
        _columns = new int[]?[Math.Max(0, _xs.Length - 1)];
        _rows = new int[]?[Math.Max(0, _ys.Length - 1)];
        if (foreign is not null)
        {
            IndexForeignSegments(foreign, window);
        }
    }

    /// <summary>
    ///     Searches for the cheapest orthogonal path between two points.
    /// </summary>
    /// <param name="obstacles">All obstacle boxes of the request.</param>
    /// <param name="from">Where the path starts (end of the start stub).</param>
    /// <param name="to">Where the path ends (beginning of the end stub).</param>
    /// <param name="startDirection">Direction index (0 right, 1 left, 2 down, 3 up) the wire initially travels in.</param>
    /// <param name="arrivalDirection">Direction index the wire must be traveling in on arrival.</param>
    /// <param name="gridSize">Grid size used to place the middle channels; zero or negative disables snapping.</param>
    /// <param name="bendPenalty">Cost of one bend.</param>
    /// <param name="padding">How far beyond the end points (and, when unbounded, beyond all obstacles) the search may stray.</param>
    /// <param name="bounded">
    ///     When true the search is confined to the end points plus <paramref name="padding"/>, which
    ///     keeps it cheap for short wires; when false every obstacle is within reach.
    /// </param>
    /// <param name="foreign">The segments of other nets the path should not run along, or null.</param>
    /// <returns>The grid points from <paramref name="from"/> to <paramref name="to"/>, or null when no path exists.</returns>
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Internal entry point that mirrors the request fields; a parameter object would only add allocations to a hot path.")]
    internal static List<Point>? Find(
        IReadOnlyList<Rect> obstacles,
        Point from,
        Point to,
        int startDirection,
        int arrivalDirection,
        double gridSize,
        double bendPenalty,
        double padding,
        bool bounded,
        ForeignSegments? foreign = null)
    {
        // Confine the search to the end points (cheap) or to everything that exists (complete)
        var extent = Rect.FromPoints(from, to);
        if (!bounded)
        {
            foreach (var obstacle in obstacles)
            {
                extent = extent.Union(obstacle);
            }
        }

        var window = extent.Inflate(Math.Max(padding, 1));
        var search = new OrthogonalSearch(obstacles, from, to, startDirection, arrivalDirection, gridSize, bendPenalty, window, foreign);
        return search.Run();
    }

    /// <summary>
    ///     Picks the obstacles relevant to a search and frees the two end points if an obstacle covers them.
    /// </summary>
    /// <remarks>
    ///     Stub points normally lie on the edge of their own node's margin, but nodes placed closer
    ///     together than twice the margin make one node's margin cover another's stub point. Such an
    ///     obstacle is shrunk just enough to leave the point on its edge, which keeps the real node
    ///     box protected while letting the wire get out.
    /// </remarks>
    /// <param name="obstacles">All obstacle boxes.</param>
    /// <param name="window">The search window.</param>
    /// <param name="from">The first end point.</param>
    /// <param name="to">The second end point.</param>
    /// <returns>The obstacles to search around.</returns>
    private static Rect[] SelectObstacles(IReadOnlyList<Rect> obstacles, Rect window, Point from, Point to)
    {
        var result = new List<Rect>();
        foreach (var obstacle in obstacles)
        {
            // Ignore degenerate boxes and boxes entirely outside the window
            if (obstacle.IsEmpty || !Overlaps(obstacle, window))
            {
                continue;
            }

            var box = ExcludePoint(ExcludePoint(obstacle, from), to);
            if (!box.IsEmpty)
            {
                result.Add(box);
            }
        }

        return [.. result];
    }

    /// <summary>
    ///     Shrinks a box uniformly until a point that was strictly inside it lies on its edge.
    /// </summary>
    /// <param name="box">The box.</param>
    /// <param name="point">The point that must not be strictly inside.</param>
    /// <returns>The original box when the point is not strictly inside, otherwise the shrunken box.</returns>
    private static Rect ExcludePoint(Rect box, Point point)
    {
        if (!StrictlyInside(box, point))
        {
            return box;
        }

        var depth = Math.Min(
            Math.Min(point.X - box.Left, box.Right - point.X),
            Math.Min(point.Y - box.Top, box.Bottom - point.Y));
        return box.Inflate(-depth);
    }

    /// <summary>
    ///     Determines whether a point lies strictly inside a box (not on its edge).
    /// </summary>
    /// <param name="box">The box.</param>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true"/> when the point is inside by more than the tolerance.</returns>
    private static bool StrictlyInside(Rect box, Point point) =>
        point.X > box.Left + Epsilon && point.X < box.Right - Epsilon
        && point.Y > box.Top + Epsilon && point.Y < box.Bottom - Epsilon;

    /// <summary>
    ///     Determines whether the interiors of two boxes overlap.
    /// </summary>
    /// <param name="a">The first box.</param>
    /// <param name="b">The second box.</param>
    /// <returns><see langword="true"/> when the boxes share interior area.</returns>
    private static bool Overlaps(Rect a, Rect b) =>
        a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;

    /// <summary>
    ///     Adds the middle channel between two coordinates, snapped to the grid when that keeps it between them.
    /// </summary>
    /// <param name="lines">The line coordinates collected so far.</param>
    /// <param name="a">One coordinate.</param>
    /// <param name="b">The other coordinate.</param>
    /// <param name="gridSize">The grid size; zero or negative disables snapping.</param>
    /// <param name="alternatives">How many extra channels to add on each side of the middle one, one grid step apart.</param>
    private static void AddChannel(List<double> lines, double a, double b, double gridSize, int alternatives)
    {
        var low = Math.Min(a, b);
        var high = Math.Max(a, b);
        if (high - low < Epsilon)
        {
            return;
        }

        var middle = (low + high) / 2;
        if (gridSize > 0)
        {
            var snapped = Math.Round(middle / gridSize, MidpointRounding.AwayFromZero) * gridSize;
            if (snapped > low + Epsilon && snapped < high - Epsilon)
            {
                middle = snapped;
            }
        }

        lines.Add(middle);

        // Parallel tracks beside the middle one, so nets that must not overlap can sit side by side
        var step = gridSize > 0 ? gridSize : FallbackTrackStep;
        for (var i = 1; i <= alternatives; i++)
        {
            foreach (var candidate in new[] { middle - (i * step), middle + (i * step) }.Where(c => c > low + Epsilon && c < high - Epsilon))
            {
                lines.Add(candidate);
            }
        }
    }

    /// <summary>
    ///     Adds grid lines on and beside every foreign segment that reaches into the window.
    /// </summary>
    /// <param name="xs">The vertical line coordinates collected so far.</param>
    /// <param name="ys">The horizontal line coordinates collected so far.</param>
    /// <param name="foreign">The foreign segments.</param>
    /// <param name="window">The search window.</param>
    /// <param name="step">The distance between neighboring tracks.</param>
    private static void AddForeignLines(List<double> xs, List<double> ys, ForeignSegments foreign, Rect window, double step)
    {
        foreach (var (y, low, high) in foreign.Horizontal)
        {
            if (y >= window.Top && y <= window.Bottom && high > window.Left && low < window.Right)
            {
                for (var i = -2; i <= 2; i++)
                {
                    AddWithin(ys, y + (i * step), window.Top, window.Bottom);
                }
            }
        }

        foreach (var (x, low, high) in foreign.Vertical)
        {
            if (x >= window.Left && x <= window.Right && high > window.Top && low < window.Bottom)
            {
                for (var i = -2; i <= 2; i++)
                {
                    AddWithin(xs, x + (i * step), window.Left, window.Right);
                }
            }
        }
    }

    /// <summary>
    ///     Files the foreign segments of the window under the index of the grid line they lie on.
    /// </summary>
    /// <param name="foreign">The foreign segments.</param>
    /// <param name="window">The search window.</param>
    private void IndexForeignSegments(ForeignSegments foreign, Rect window)
    {
        foreach (var (y, low, high) in foreign.Horizontal)
        {
            if (y >= window.Top && y <= window.Bottom && high > window.Left && low < window.Right)
            {
                var line = NearestIndex(_ys, y);
                AddSegment(_rowSegments, line, low, high);
                AddAlong(_rowAlong, _ys, line, y, low, high);
            }
        }

        foreach (var (x, low, high) in foreign.Vertical)
        {
            if (x >= window.Left && x <= window.Right && high > window.Top && low < window.Bottom)
            {
                var line = NearestIndex(_xs, x);
                AddSegment(_columnSegments, line, low, high);
                AddAlong(_columnAlong, _xs, line, x, low, high);
            }
        }
    }

    /// <summary>
    ///     Files a foreign segment under every grid line close enough to it to look like the same track.
    /// </summary>
    /// <param name="index">The along-lists by grid line index.</param>
    /// <param name="lines">The sorted grid lines.</param>
    /// <param name="nearest">The index of the grid line closest to the segment.</param>
    /// <param name="position">The coordinate of the segment across its direction.</param>
    /// <param name="low">The low end of the interval.</param>
    /// <param name="high">The high end of the interval.</param>
    private static void AddAlong(Dictionary<int, List<(double Low, double High)>> index, double[] lines, int nearest, double position, double low, double high)
    {
        // A run that ends right where the other begins reads as one line with a kink, so the segment is stretched at both ends
        var from = low - NearCollinearReach;
        var to = high + NearCollinearReach;
        for (var i = nearest; i >= 0 && Math.Abs(lines[i] - position) <= NearCollinearTolerance; i--)
        {
            AddSegment(index, i, from, to);
        }

        for (var i = nearest + 1; i < lines.Length && Math.Abs(lines[i] - position) <= NearCollinearTolerance; i++)
        {
            AddSegment(index, i, from, to);
        }
    }

    /// <summary>
    ///     Adds one interval to the list kept for a grid line.
    /// </summary>
    /// <param name="index">The segment lists by grid line index.</param>
    /// <param name="line">The grid line index.</param>
    /// <param name="low">The low end of the interval.</param>
    /// <param name="high">The high end of the interval.</param>
    private static void AddSegment(Dictionary<int, List<(double Low, double High)>> index, int line, double low, double high)
    {
        if (!index.TryGetValue(line, out var list))
        {
            list = [];
            index[line] = list;
        }

        list.Add((low, high));
    }

    /// <summary>
    ///     Computes the extra cost of one step caused by foreign segments: running along one, or crossing one.
    /// </summary>
    /// <param name="ix">Column index the step starts at.</param>
    /// <param name="iy">Row index the step starts at.</param>
    /// <param name="nx">Column index the step ends at.</param>
    /// <param name="ny">Row index the step ends at.</param>
    /// <param name="direction">The direction index of the step.</param>
    /// <returns>The extra cost; zero when no other net is involved.</returns>
    private double ForeignCost(int ix, int iy, int nx, int ny, int direction)
    {
        if (_foreign is null)
        {
            return 0;
        }

        var cost = 0.0;
        if (direction < 2)
        {
            // Horizontal step: penalize every unit that runs along a foreign horizontal segment on this row
            if (_rowAlong.TryGetValue(iy, out var along))
            {
                var a = Math.Min(_xs[ix], _xs[nx]);
                var b = Math.Max(_xs[ix], _xs[nx]);
                foreach (var (low, high) in along)
                {
                    var overlap = Math.Min(b, high) - Math.Max(a, low);
                    cost += overlap > Epsilon ? overlap * _foreign.OverlapPenalty : 0;
                }
            }

            // ... and reaching a point strictly inside a foreign vertical segment means crossing it
            if (_columnSegments.TryGetValue(nx, out var across))
            {
                cost += CrossingCost(across, _ys[ny]);
            }

            return cost;
        }

        if (_columnAlong.TryGetValue(ix, out var alongColumn))
        {
            var a = Math.Min(_ys[iy], _ys[ny]);
            var b = Math.Max(_ys[iy], _ys[ny]);
            foreach (var (low, high) in alongColumn)
            {
                var overlap = Math.Min(b, high) - Math.Max(a, low);
                cost += overlap > Epsilon ? overlap * _foreign.OverlapPenalty : 0;
            }
        }

        if (_rowSegments.TryGetValue(ny, out var acrossRow))
        {
            cost += CrossingCost(acrossRow, _xs[nx]);
        }

        return cost;
    }

    /// <summary>
    ///     Computes the crossing cost of a point against the foreign segments of one grid line.
    /// </summary>
    /// <param name="segments">The foreign segments lying on the grid line the point is on.</param>
    /// <param name="position">The coordinate of the point along the grid line.</param>
    /// <returns>The crossing penalty for every segment that strictly contains the point.</returns>
    private double CrossingCost(List<(double Low, double High)> segments, double position)
    {
        var cost = 0.0;
        foreach (var (low, high) in segments)
        {
            if (position > low + Epsilon && position < high - Epsilon)
            {
                cost += _foreign!.CrossingPenalty;
            }
        }

        return cost;
    }

    /// <summary>
    ///     Adds a coordinate when it lies within a range.
    /// </summary>
    /// <param name="lines">The line coordinates collected so far.</param>
    /// <param name="value">The candidate coordinate.</param>
    /// <param name="low">Lower limit.</param>
    /// <param name="high">Upper limit.</param>
    private static void AddWithin(List<double> lines, double value, double low, double high)
    {
        if (value >= low && value <= high)
        {
            lines.Add(value);
        }
    }

    /// <summary>
    ///     Sorts coordinates and merges those closer together than the tolerance.
    /// </summary>
    /// <param name="values">The coordinates.</param>
    /// <returns>The sorted distinct coordinates.</returns>
    private static double[] Compress(List<double> values)
    {
        values.Sort();
        var result = new List<double>(values.Count);
        foreach (var value in values)
        {
            if (result.Count == 0 || value - result[^1] > Epsilon)
            {
                result.Add(value);
            }
        }

        return [.. result];
    }

    /// <summary>
    ///     Finds the index of the grid line closest to a coordinate.
    /// </summary>
    /// <param name="lines">The sorted grid lines.</param>
    /// <param name="value">The coordinate.</param>
    /// <returns>The index of the closest line.</returns>
    private static int NearestIndex(double[] lines, double value)
    {
        var index = Array.BinarySearch(lines, value);
        if (index >= 0)
        {
            return index;
        }

        // BinarySearch returns the complement of the next larger element; pick the closer neighbor
        var upper = ~index;
        if (upper >= lines.Length)
        {
            return lines.Length - 1;
        }

        return upper > 0 && value - lines[upper - 1] <= lines[upper] - value ? upper - 1 : upper;
    }

    /// <summary>
    ///     Gets the direction index opposite to another.
    /// </summary>
    /// <param name="direction">A direction index.</param>
    /// <returns>The reverse direction index.</returns>
    private static int Opposite(int direction) => direction ^ 1;

    /// <summary>
    ///     Runs the A* search.
    /// </summary>
    /// <returns>The grid points of the cheapest path, or null when none exists.</returns>
    private List<Point>? Run()
    {
        var ny = _ys.Length;
        var startX = NearestIndex(_xs, _from.X);
        var startY = NearestIndex(_ys, _from.Y);
        var search = new SearchState(ny, NearestIndex(_xs, _to.X), NearestIndex(_ys, _to.Y), (_from.X + _to.X) / 2, (_from.Y + _to.Y) / 2);

        var startKey = EncodeState(startX, startY, _startDirection, ny);
        search.Best[startKey] = 0;
        search.Parent[startKey] = -1;
        var startEstimate = Heuristic(startX, startY, search.EndX, search.EndY);
        search.Open.Enqueue((startKey, 0), (startEstimate, startEstimate, search.Sequence++));

        while (search.Open.TryDequeue(out var current, out _))
        {
            // Skip entries superseded by a cheaper route to the same state
            if (current.Cost > search.Best[current.Key] + Epsilon)
            {
                continue;
            }

            var (ix, iy, direction) = DecodeState(current.Key, ny);
            if (ix == search.EndX && iy == search.EndY)
            {
                return Reconstruct(current.Key, search.Parent, ny);
            }

            for (var next = 0; next < 4; next++)
            {
                // Never reverse: a U-turn would double back over the wire itself
                if (next != Opposite(direction))
                {
                    Relax(search, current, (ix, iy), direction, next);
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     Tries to extend the path from a state one grid step in a direction, queueing the new state when it is cheaper than any known.
    /// </summary>
    /// <param name="search">The search state.</param>
    /// <param name="current">The state being expanded and its cost.</param>
    /// <param name="position">The grid column and row of the state.</param>
    /// <param name="direction">The direction the wire arrived in.</param>
    /// <param name="next">The direction of the step.</param>
    private void Relax(SearchState search, (int Key, double Cost) current, (int X, int Y) position, int direction, int next)
    {
        var (ix, iy) = position;
        var nx = ix + StepX[next];
        var nyIndex = iy + StepY[next];
        if (nx < 0 || nx >= _xs.Length || nyIndex < 0 || nyIndex >= search.Ny || IsBlocked(ix, iy, next))
        {
            return;
        }

        // The wire must reach the end stub traveling the way the stub expects
        var reachesEnd = nx == search.EndX && nyIndex == search.EndY;
        if (reachesEnd && next == Opposite(_arrivalDirection))
        {
            return;
        }

        var cost = current.Cost + Math.Abs(_xs[nx] - _xs[ix]) + Math.Abs(_ys[nyIndex] - _ys[iy]) + ForeignCost(ix, iy, nx, nyIndex, next);
        if (next != direction)
        {
            cost += _bendPenalty + (CenterBias * (Math.Abs(_xs[ix] - search.CenterX) + Math.Abs(_ys[iy] - search.CenterY)));
        }

        var key = EncodeState(nx, nyIndex, next, search.Ny);
        if (search.Best.TryGetValue(key, out var known) && known <= cost + Epsilon)
        {
            return;
        }

        search.Best[key] = cost;
        search.Parent[key] = current.Key;

        // Arriving at the end in the wrong direction costs one more bend into the stub; that bend
        // sits at the end point and takes part in the center tie-break like every other bend
        var remaining = Heuristic(nx, nyIndex, search.EndX, search.EndY);
        var arrivalBend = reachesEnd && next != _arrivalDirection
            ? _bendPenalty + (CenterBias * (Math.Abs(_xs[nx] - search.CenterX) + Math.Abs(_ys[nyIndex] - search.CenterY)))
            : 0;
        var total = cost + remaining + arrivalBend;
        search.Open.Enqueue((key, cost), (total, remaining, search.Sequence++));
    }

    /// <summary>
    ///     The mutable state of one A* run.
    /// </summary>
    private sealed class SearchState
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SearchState"/> class.
        /// </summary>
        /// <param name="ny">The number of grid rows.</param>
        /// <param name="endX">The goal column index.</param>
        /// <param name="endY">The goal row index.</param>
        /// <param name="centerX">The X of the center between start and end.</param>
        /// <param name="centerY">The Y of the center between start and end.</param>
        public SearchState(int ny, int endX, int endY, double centerX, double centerY)
        {
            Ny = ny;
            EndX = endX;
            EndY = endY;
            CenterX = centerX;
            CenterY = centerY;
        }

        /// <summary>
        ///     Gets the number of grid rows.
        /// </summary>
        public int Ny { get; }

        /// <summary>
        ///     Gets the goal column index.
        /// </summary>
        public int EndX { get; }

        /// <summary>
        ///     Gets the goal row index.
        /// </summary>
        public int EndY { get; }

        /// <summary>
        ///     Gets the X of the center between start and end.
        /// </summary>
        public double CenterX { get; }

        /// <summary>
        ///     Gets the Y of the center between start and end.
        /// </summary>
        public double CenterY { get; }

        /// <summary>
        ///     Gets the cheapest known cost per state.
        /// </summary>
        public Dictionary<int, double> Best { get; } = [];

        /// <summary>
        ///     Gets the predecessor state per state, for path reconstruction.
        /// </summary>
        public Dictionary<int, int> Parent { get; } = [];

        /// <summary>
        ///     Gets the open states ordered by estimated total cost, remaining estimate and insertion sequence.
        /// </summary>
        public PriorityQueue<(int Key, double Cost), (double Total, double Remaining, int Sequence)> Open { get; } = new();

        /// <summary>
        ///     Gets or sets the next insertion sequence number, which keeps equal priorities in insertion order.
        /// </summary>
        public int Sequence { get; set; }
    }

    /// <summary>
    ///     Computes the admissible estimate of the remaining cost: the Manhattan distance to the goal.
    /// </summary>
    /// <param name="ix">Column index.</param>
    /// <param name="iy">Row index.</param>
    /// <param name="endX">Goal column index.</param>
    /// <param name="endY">Goal row index.</param>
    /// <returns>The Manhattan distance.</returns>
    private double Heuristic(int ix, int iy, int endX, int endY) =>
        Math.Abs(_xs[ix] - _xs[endX]) + Math.Abs(_ys[iy] - _ys[endY]);

    /// <summary>
    ///     Packs a search state into one integer key.
    /// </summary>
    /// <param name="ix">Column index.</param>
    /// <param name="iy">Row index.</param>
    /// <param name="direction">Travel direction index.</param>
    /// <param name="rows">Number of rows in the grid.</param>
    /// <returns>The key.</returns>
    private static int EncodeState(int ix, int iy, int direction, int rows) => (((ix * rows) + iy) * 4) + direction;

    /// <summary>
    ///     Unpacks a search state key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="rows">Number of rows in the grid.</param>
    /// <returns>The column index, row index and direction index.</returns>
    private static (int Ix, int Iy, int Direction) DecodeState(int key, int rows)
    {
        var direction = key & 3;
        var cell = key >> 2;
        return (cell / rows, cell % rows, direction);
    }

    /// <summary>
    ///     Rebuilds the path by following parent links from the goal state back to the start.
    /// </summary>
    /// <param name="goal">The goal state key.</param>
    /// <param name="parent">The predecessor of each state.</param>
    /// <param name="rows">Number of rows in the grid.</param>
    /// <returns>The grid points from start to goal.</returns>
    private List<Point> Reconstruct(int goal, Dictionary<int, int> parent, int rows)
    {
        var points = new List<Point>();
        for (var key = goal; key >= 0; key = parent[key])
        {
            var (ix, iy, _) = DecodeState(key, rows);
            points.Add(new Point(_xs[ix], _ys[iy]));
        }

        points.Reverse();
        return points;
    }

    /// <summary>
    ///     Determines whether a one-step move from a grid intersection to its neighbor crosses an obstacle interior.
    /// </summary>
    /// <param name="ix">Column index of the intersection.</param>
    /// <param name="iy">Row index of the intersection.</param>
    /// <param name="direction">The direction of the step.</param>
    /// <returns><see langword="true"/> when an obstacle blocks the step.</returns>
    private bool IsBlocked(int ix, int iy, int direction)
    {
        if (direction < 2)
        {
            // Horizontal step: it lies in the column between two vertical lines, at the row's y
            var column = direction == 0 ? ix : ix - 1;
            var y = _ys[iy];

            // Plain loops (not LINQ): this runs in the search's inner loop and must not allocate
            foreach (var index in ColumnObstacles(column))
            {
                var box = _boxes[index];
                if (y > box.Top + Epsilon && y < box.Bottom - Epsilon)
                {
                    return true;
                }
            }

            return false;
        }

        // Vertical step: it lies in the row between two horizontal lines, at the column's x
        var row = direction == 2 ? iy : iy - 1;
        var x = _xs[ix];
        foreach (var index in RowObstacles(row))
        {
            var box = _boxes[index];
            if (x > box.Left + Epsilon && x < box.Right - Epsilon)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets the obstacles whose horizontal extent covers a whole column between two vertical grid lines.
    /// </summary>
    /// <remarks>
    ///     Every obstacle edge inside the window is a grid line, so an obstacle either covers a
    ///     column completely or not at all.
    /// </remarks>
    /// <param name="column">The column index.</param>
    /// <returns>Indexes into the obstacle array.</returns>
    private int[] ColumnObstacles(int column)
    {
        if (_columns[column] is { } cached)
        {
            return cached;
        }

        var low = _xs[column];
        var high = _xs[column + 1];
        var list = new List<int>();
        for (var i = 0; i < _boxes.Length; i++)
        {
            if (_boxes[i].Left <= low + Epsilon && _boxes[i].Right >= high - Epsilon)
            {
                list.Add(i);
            }
        }

        return _columns[column] = [.. list];
    }

    /// <summary>
    ///     Gets the obstacles whose vertical extent covers a whole row between two horizontal grid lines.
    /// </summary>
    /// <param name="row">The row index.</param>
    /// <returns>Indexes into the obstacle array.</returns>
    private int[] RowObstacles(int row)
    {
        if (_rows[row] is { } cached)
        {
            return cached;
        }

        var low = _ys[row];
        var high = _ys[row + 1];
        var list = new List<int>();
        for (var i = 0; i < _boxes.Length; i++)
        {
            if (_boxes[i].Top <= low + Epsilon && _boxes[i].Bottom >= high - Epsilon)
            {
                list.Add(i);
            }
        }

        return _rows[row] = [.. list];
    }
}
