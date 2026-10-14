using DemaConsulting.Plexus.Routing;

namespace DemaConsulting.Plexus;

/// <summary>
///     The segments of the wires already routed on a page, grouped into nets, for handing to routers.
/// </summary>
/// <remarks>
///     A net is every wire that starts at the same output port, extended through inputs that allow
///     fan-in: wires that end at the same such input join the nets of their sources, because they
///     merge there and may share the final approach. The traffic assigns each port a small integer so
///     <see cref="RouteRequest.Net"/> and <see cref="RouteSegment.Net"/> can be compared cheaply.
///     Callers add wires in a deterministic order (page order) as they are routed, so the result of
///     re-routing a whole page never depends on stale routes.
/// </remarks>
internal sealed class WireTraffic
{
    /// <summary>
    ///     The identity first assigned to each port seen so far.
    /// </summary>
    private readonly Dictionary<Port, int> _nets = [];

    /// <summary>
    ///     For each identity, the identity it was merged into, or itself when it is a net representative.
    /// </summary>
    private readonly List<int> _parents = [];

    /// <summary>
    ///     The segments added so far, tagged with their identity at the time of adding.
    /// </summary>
    private readonly List<RouteSegment> _segments = [];

    /// <summary>
    ///     The segments with their current net, in the order they were added; replaced (never changed) when nets merge.
    /// </summary>
    private List<RouteSegment> _resolved = [];

    /// <summary>
    ///     Whether <see cref="_resolved"/> carries nets that a merge has since replaced.
    /// </summary>
    private bool _resolvedStale;

    /// <summary>
    ///     Gets the segments added so far, tagged with the current net of their wire.
    /// </summary>
    /// <remarks>
    ///     The result is a snapshot: segments added later do not appear in it. Reading it is cheap, so
    ///     routing many wires one after another does not copy the traffic for every wire.
    /// </remarks>
    public IReadOnlyList<RouteSegment> Segments
    {
        get
        {
            if (_resolvedStale)
            {
                _resolved = [.. _segments.Select(s => s with { Net = Find(s.Net) })];
                _resolvedStale = false;
            }

            return new SegmentSnapshot(_resolved, _resolved.Count);
        }
    }

    /// <summary>
    ///     Determines whether routing a wire between two ports would merge two nets of this traffic.
    /// </summary>
    /// <param name="source">The output port the wire starts at.</param>
    /// <param name="target">The input port the wire ends at.</param>
    /// <returns><see langword="true"/> when the target allows fan-in and already belongs to a different net.</returns>
    /// <remarks>A target seen for the first time joins the source's net without merging anything.</remarks>
    public bool WouldMerge(Port source, Port target) =>
        target.AllowsMultipleWires && _nets.TryGetValue(target, out var id) && Find(id) != Find(Identity(source));

    /// <summary>
    ///     Gets the net identity of a wire's source port, assigning a new one the first time it is seen.
    /// </summary>
    /// <param name="source">The output port a wire starts at.</param>
    /// <param name="target">The input port the wire ends at, when known; an input that allows fan-in joins the wire to the net of its other wires.</param>
    /// <returns>The net identity.</returns>
    public int NetOf(Port source, Port? target = null)
    {
        var net = Find(Identity(source));
        if (target is not { AllowsMultipleWires: true })
        {
            return net;
        }

        // A fan-in input seen for the first time simply joins this net; one that already has a net merges it into this
        // one, which re-tags the segments of that net
        if (!_nets.TryGetValue(target, out var id))
        {
            _nets[target] = net;
            return net;
        }

        var other = Find(id);
        if (other != net)
        {
            _parents[other] = net;
            _resolvedStale = true;
        }

        return net;
    }

    /// <summary>
    ///     Adds the segments of a routed wire.
    /// </summary>
    /// <param name="source">The wire's source port, which names its net.</param>
    /// <param name="target">The wire's target port, which joins the net of its other wires when it allows fan-in.</param>
    /// <param name="start">The source anchor.</param>
    /// <param name="route">The interior bend points.</param>
    /// <param name="end">The target anchor.</param>
    public void Add(Port source, Port target, Point start, IReadOnlyList<Point> route, Point end)
    {
        var net = NetOf(source, target);
        var previous = start;
        foreach (var point in route)
        {
            Append(new RouteSegment(previous, point, net));
            previous = point;
        }

        Append(new RouteSegment(previous, end, net));
    }

    /// <summary>
    ///     Records a segment, and adds it to the resolved list while that list is current.
    /// </summary>
    /// <param name="segment">The segment, tagged with its current net.</param>
    private void Append(RouteSegment segment)
    {
        _segments.Add(segment);
        if (!_resolvedStale)
        {
            _resolved.Add(segment);
        }
    }

    /// <summary>
    ///     Gets the identity of a port, assigning a new one the first time it is seen.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <returns>The identity.</returns>
    private int Identity(Port port)
    {
        if (!_nets.TryGetValue(port, out var id))
        {
            id = _parents.Count;
            _parents.Add(id);
            _nets[port] = id;
        }

        return id;
    }

    /// <summary>
    ///     Finds the representative identity of the net an identity belongs to.
    /// </summary>
    /// <param name="id">The identity.</param>
    /// <returns>The representative identity.</returns>
    private int Find(int id)
    {
        while (_parents[id] != id)
        {
            _parents[id] = _parents[_parents[id]];
            id = _parents[id];
        }

        return id;
    }
}

/// <summary>
///     A read-only view of the first segments of a list that only ever grows.
/// </summary>
/// <param name="segments">The list; items below <paramref name="count"/> never change.</param>
/// <param name="count">The number of segments the view shows.</param>
internal sealed class SegmentSnapshot(List<RouteSegment> segments, int count) : IReadOnlyList<RouteSegment>
{
    /// <inheritdoc/>
    public int Count => count;

    /// <inheritdoc/>
    public RouteSegment this[int index] =>
        (uint)index < (uint)count ? segments[index] : throw new ArgumentOutOfRangeException(nameof(index));

    /// <inheritdoc/>
    public IEnumerator<RouteSegment> GetEnumerator()
    {
        for (var i = 0; i < count; i++)
        {
            yield return segments[i];
        }
    }

    /// <inheritdoc/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
