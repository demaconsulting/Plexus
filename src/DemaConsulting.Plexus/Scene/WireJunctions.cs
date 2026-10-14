namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Finds the points where wires of one net branch apart, which are marked with a junction dot.
/// </summary>
/// <remarks>
///     A point is a junction when, counting every wire of the net, the wire segments that meet there
///     leave it in at least three different directions: a "T" where one wire ends or bends against a
///     trunk another wire runs along, or a "+" where two wires branch both ways. Two wires that
///     simply run together and turn together leave in only two directions and so make no dot.
///     Only horizontal and vertical segments take part; the directions of a diagonal segment are
///     classified by its dominant axis.
/// </remarks>
internal static class WireJunctions
{
    /// <summary>
    ///     Tolerance below which two coordinates are the same point.
    /// </summary>
    private const double Epsilon = 1e-3;

    /// <summary>
    ///     Direction bit: toward positive X.
    /// </summary>
    private const int Right = 1;

    /// <summary>
    ///     Direction bit: toward negative X.
    /// </summary>
    private const int Left = 2;

    /// <summary>
    ///     Direction bit: toward positive Y.
    /// </summary>
    private const int Down = 4;

    /// <summary>
    ///     Direction bit: toward negative Y.
    /// </summary>
    private const int Up = 8;

    /// <summary>
    ///     Finds the junctions of a set of wires that belong to one net.
    /// </summary>
    /// <param name="wires">The displayed polylines (end points included) of the wires of the net.</param>
    /// <returns>Each junction point with the index of the first wire that has a point there, in a deterministic order.</returns>
    public static List<(Point Point, int Owner)> Find(IReadOnlyList<IReadOnlyList<Point>> wires)
    {
        // Every vertex of every wire is a candidate; remember which wire named it first and the directions its neighbors lie in
        var candidates = new List<(Point Point, int Owner, int Directions)>();
        for (var w = 0; w < wires.Count; w++)
        {
            var points = wires[w];
            for (var i = 0; i < points.Count; i++)
            {
                var directions = 0;
                if (i > 0)
                {
                    directions |= DirectionTo(points[i], points[i - 1]);
                }

                if (i + 1 < points.Count)
                {
                    directions |= DirectionTo(points[i], points[i + 1]);
                }

                Merge(candidates, points[i], w, directions);
            }
        }

        // A candidate that lies inside another wire's segment also has that segment's two directions
        for (var c = 0; c < candidates.Count; c++)
        {
            foreach (var points in wires)
            {
                for (var i = 0; i + 1 < points.Count; i++)
                {
                    if (StrictlyInside(candidates[c].Point, points[i], points[i + 1]))
                    {
                        var along = DirectionTo(points[i], points[i + 1]);
                        var current = candidates[c];
                        candidates[c] = (current.Point, current.Owner, current.Directions | along | Opposite(along));
                    }
                }
            }
        }

        var junctions = new List<(Point, int)>();
        foreach (var (point, owner, directions) in candidates)
        {
            if (BitCount(directions) >= 3)
            {
                junctions.Add((point, owner));
            }
        }

        return junctions;
    }

    /// <summary>
    ///     Adds directions to the candidate at a point, creating the candidate when the point is new.
    /// </summary>
    /// <param name="candidates">The candidates so far.</param>
    /// <param name="point">The point.</param>
    /// <param name="owner">The wire naming the point.</param>
    /// <param name="directions">The directions the wire leaves the point in.</param>
    private static void Merge(List<(Point Point, int Owner, int Directions)> candidates, Point point, int owner, int directions)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (Math.Abs(candidates[i].Point.X - point.X) < Epsilon && Math.Abs(candidates[i].Point.Y - point.Y) < Epsilon)
            {
                var current = candidates[i];
                candidates[i] = (current.Point, current.Owner, current.Directions | directions);
                return;
            }
        }

        candidates.Add((point, owner, directions));
    }

    /// <summary>
    ///     Classifies the direction from one point toward another by its dominant axis.
    /// </summary>
    /// <param name="from">The origin.</param>
    /// <param name="to">The neighbor.</param>
    /// <returns>The direction bit, or zero when the points coincide.</returns>
    private static int DirectionTo(Point from, Point to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (Math.Abs(dx) < Epsilon && Math.Abs(dy) < Epsilon)
        {
            return 0;
        }

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx > 0 ? Right : Left;
        }

        return dy > 0 ? Down : Up;
    }

    /// <summary>
    ///     Gets the direction bit opposite to another.
    /// </summary>
    /// <param name="direction">A direction bit.</param>
    /// <returns>The opposite direction bit.</returns>
    private static int Opposite(int direction) => direction switch
    {
        Right => Left,
        Left => Right,
        Down => Up,
        Up => Down,
        _ => 0,
    };

    /// <summary>
    ///     Determines whether a point lies on a horizontal or vertical segment, strictly between its ends.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="a">One end of the segment.</param>
    /// <param name="b">The other end of the segment.</param>
    /// <returns><see langword="true"/> when the point is inside the segment and not at either end.</returns>
    private static bool StrictlyInside(Point point, Point a, Point b)
    {
        if (Math.Abs(a.Y - b.Y) < Epsilon && Math.Abs(point.Y - a.Y) < Epsilon)
        {
            return point.X > Math.Min(a.X, b.X) + Epsilon && point.X < Math.Max(a.X, b.X) - Epsilon;
        }

        return Math.Abs(a.X - b.X) < Epsilon
            && Math.Abs(point.X - a.X) < Epsilon
            && point.Y > Math.Min(a.Y, b.Y) + Epsilon
            && point.Y < Math.Max(a.Y, b.Y) - Epsilon;
    }

    /// <summary>
    ///     Counts the set bits of a direction mask.
    /// </summary>
    /// <param name="mask">The mask.</param>
    /// <returns>The number of directions in the mask.</returns>
    private static int BitCount(int mask) => System.Numerics.BitOperations.PopCount((uint)mask);
}
