namespace DemaConsulting.Plexus.Routing;

/// <summary>
///     The axis-aligned segments of other nets that a search should keep clear of, with the penalties for ignoring that.
/// </summary>
/// <remarks>
///     Built once per routed wire from <see cref="RouteRequest.ExistingSegments"/>: segments of the
///     wire's own net are dropped (they may be shared like a bus trunk), diagonal and zero-length
///     segments are ignored because only orthogonal wires have a meaningful "running along" relation.
///     The lists are plain data; <see cref="OrthogonalSearch"/> turns the ones inside its window into
///     per-grid-line lookups.
/// </remarks>
internal sealed class ForeignSegments
{
    /// <summary>
    ///     Tolerance below which two coordinates are the same.
    /// </summary>
    private const double Epsilon = 1e-6;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ForeignSegments"/> class.
    /// </summary>
    /// <param name="horizontal">The horizontal segments as (y, low x, high x).</param>
    /// <param name="vertical">The vertical segments as (x, low y, high y).</param>
    /// <param name="overlapPenalty">The extra cost per unit of length run along a foreign segment.</param>
    /// <param name="crossingPenalty">The extra cost of crossing a foreign segment.</param>
    private ForeignSegments(
        List<(double Fixed, double Low, double High)> horizontal,
        List<(double Fixed, double Low, double High)> vertical,
        double overlapPenalty,
        double crossingPenalty)
    {
        Horizontal = horizontal;
        Vertical = vertical;
        OverlapPenalty = overlapPenalty;
        CrossingPenalty = crossingPenalty;
    }

    /// <summary>
    ///     Gets the horizontal segments as (y, low x, high x).
    /// </summary>
    public IReadOnlyList<(double Fixed, double Low, double High)> Horizontal { get; }

    /// <summary>
    ///     Gets the vertical segments as (x, low y, high y).
    /// </summary>
    public IReadOnlyList<(double Fixed, double Low, double High)> Vertical { get; }

    /// <summary>
    ///     Gets the extra cost per unit of length run collinear with a foreign segment.
    /// </summary>
    public double OverlapPenalty { get; }

    /// <summary>
    ///     Gets the extra cost of crossing a foreign segment at a right angle.
    /// </summary>
    public double CrossingPenalty { get; }

    /// <summary>
    ///     Collects the segments of other nets from a request.
    /// </summary>
    /// <param name="segments">The existing segments of the request.</param>
    /// <param name="net">The net of the wire being routed, or negative when unknown.</param>
    /// <param name="overlapPenalty">The extra cost per unit of length run along a foreign segment.</param>
    /// <param name="crossingPenalty">The extra cost of crossing a foreign segment.</param>
    /// <returns>The foreign segments, or null when there is nothing to keep clear of.</returns>
    public static ForeignSegments? Create(IReadOnlyList<RouteSegment> segments, int net, double overlapPenalty, double crossingPenalty)
    {
        if (segments.Count == 0 || (overlapPenalty <= 0 && crossingPenalty <= 0))
        {
            return null;
        }

        var horizontal = new List<(double, double, double)>();
        var vertical = new List<(double, double, double)>();
        foreach (var segment in segments)
        {
            // Wires of the same net may share a trunk, so they are not obstacles
            if (net >= 0 && segment.Net == net)
            {
                continue;
            }

            var dx = Math.Abs(segment.End.X - segment.Start.X);
            var dy = Math.Abs(segment.End.Y - segment.Start.Y);
            if (dy < Epsilon && dx >= Epsilon)
            {
                horizontal.Add((segment.Start.Y, Math.Min(segment.Start.X, segment.End.X), Math.Max(segment.Start.X, segment.End.X)));
            }
            else if (dx < Epsilon && dy >= Epsilon)
            {
                vertical.Add((segment.Start.X, Math.Min(segment.Start.Y, segment.End.Y), Math.Max(segment.Start.Y, segment.End.Y)));
            }
        }

        return horizontal.Count + vertical.Count == 0
            ? null
            : new ForeignSegments(horizontal, vertical, overlapPenalty, crossingPenalty);
    }
}
