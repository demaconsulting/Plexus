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
    ///     Gets the segments added so far, tagged with the current net of their wire.
    /// </summary>
    public IReadOnlyList<RouteSegment> Segments => [.. _segments.Select(s => s with { Net = Find(s.Net) })];

    /// <summary>
    ///     Gets the net identity of a wire's source port, assigning a new one the first time it is seen.
    /// </summary>
    /// <param name="source">The output port a wire starts at.</param>
    /// <param name="target">The input port the wire ends at, when known; an input that allows fan-in joins the wire to the net of its other wires.</param>
    /// <returns>The net identity.</returns>
    public int NetOf(Port source, Port? target = null)
    {
        var net = Find(Identity(source));
        if (target is { AllowsMultipleWires: true })
        {
            var other = Find(Identity(target));
            if (other != net)
            {
                _parents[other] = net;
            }
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
            _segments.Add(new RouteSegment(previous, point, net));
            previous = point;
        }

        _segments.Add(new RouteSegment(previous, end, net));
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
