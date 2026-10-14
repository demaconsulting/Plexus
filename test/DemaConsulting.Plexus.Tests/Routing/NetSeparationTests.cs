using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for net separation in <see cref="OrthogonalRouter"/>: wires of different nets keep off each other's segments, wires of one net may share a trunk.
/// </summary>
public class NetSeparationTests
{
    /// <summary>
    ///     Tolerance for coordinate comparisons.
    /// </summary>
    private const double Epsilon = 1e-6;

    /// <summary>
    ///     Validates that, without other wires around, two nets would indeed share the same trunk (so the separation tests measure something).
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_WithoutExistingSegments_TwoNetsShareTrunk()
    {
        // Arrange: two wires with the same middle channel
        var router = new OrthogonalRouter();
        var first = Request(new Point(0, 0), new Point(300, 150), 0, []);
        var second = Request(new Point(0, 30), new Point(300, 120), 1, []);

        // Act
        var a = Segments(first, router.Route(first), 0);
        var b = Segments(second, router.Route(second), 1);

        // Assert
        Assert.True(OverlapLength(a, b) > 0);
    }

    /// <summary>
    ///     Validates that a wire of another net takes its own track instead of running along the first wire.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_ExistingOtherNet_DoesNotRunAlongIt()
    {
        // Arrange: the first wire is routed, then the second keeps clear of it
        var router = new OrthogonalRouter();
        var first = Request(new Point(0, 0), new Point(300, 150), 0, []);
        var a = Segments(first, router.Route(first), 0);
        var second = Request(new Point(0, 30), new Point(300, 120), 1, a);

        // Act
        var route = router.Route(second);
        var b = Segments(second, route, 1);

        // Assert: still a valid orthogonal wire between its ports, with no shared stretch
        Assert.Multiple(
            () => Assert.True(RouteAssertions.IsOrthogonal(RouteAssertions.Polyline(second, route))),
            () => Assert.Equal(0, OverlapLength(a, b), 6));
    }

    /// <summary>
    ///     Validates that wires of the same net are free to share a trunk.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_ExistingSameNet_MayShareTrunk()
    {
        // Arrange: the same two wires, but both belong to net 0
        var router = new OrthogonalRouter();
        var first = Request(new Point(0, 0), new Point(300, 150), 0, []);
        var a = Segments(first, router.Route(first), 0);
        var second = Request(new Point(0, 0), new Point(300, 120), 0, a);

        // Act
        var b = Segments(second, router.Route(second), 0);

        // Assert: the branch runs along the trunk of the first wire
        Assert.True(OverlapLength(a, b) > 0);
    }

    /// <summary>
    ///     Validates that crossing another net is allowed when there is no other way: a wire must cross a long wire that blocks its way.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_MustCrossOtherNet_CrossesWithoutOverlap()
    {
        // Arrange: a long horizontal wire from far left to far right at y=100 and a wire that has to get from above to below it
        var router = new OrthogonalRouter();
        List<RouteSegment> wall = [new(new Point(-500, 100), new Point(1500, 100), 0)];
        var request = Request(new Point(0, 0), new Point(300, 200), 1, wall);

        // Act
        var route = router.Route(request);
        var segments = Segments(request, route, 1);

        // Assert: it crossed y=100 (start above, end below), without running along the wall
        Assert.Multiple(
            () => Assert.True(RouteAssertions.IsOrthogonal(RouteAssertions.Polyline(request, route))),
            () => Assert.Equal(0, OverlapLength(wall, segments), 6));
    }

    /// <summary>
    ///     Validates that routing with other nets around is deterministic.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_WithExistingSegments_IsDeterministic()
    {
        // Arrange
        var router = new OrthogonalRouter();
        var first = Request(new Point(0, 0), new Point(300, 150), 0, []);
        var a = Segments(first, router.Route(first), 0);
        var second = Request(new Point(0, 30), new Point(300, 120), 1, a);

        // Act
        var one = router.Route(second);
        var two = router.Route(second);

        // Assert
        Assert.Equal(one, two);
    }

    /// <summary>
    ///     Validates that overlap avoidance can be switched off with a zero penalty.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_ZeroOverlapPenalty_IgnoresOtherNets()
    {
        // Arrange
        var router = new OrthogonalRouter { OverlapPenalty = 0, CrossingPenalty = 0 };
        var first = Request(new Point(0, 0), new Point(300, 150), 0, []);
        var a = Segments(first, router.Route(first), 0);
        var second = Request(new Point(0, 30), new Point(300, 120), 1, a);

        // Act
        var b = Segments(second, router.Route(second), 1);

        // Assert
        Assert.True(OverlapLength(a, b) > 0);
    }

    /// <summary>
    ///     Validates that many parallel nets routed one after another are orthogonal and none of them runs along another.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_ManyParallelNets_KeepSeparateTracks()
    {
        // Arrange: twenty nets from a column of outputs to a reversed column of inputs, routed one after the other
        const int nets = 20;
        var router = new OrthogonalRouter();
        var existing = new List<RouteSegment>();
        var all = new List<List<RouteSegment>>();
        var polylines = new List<List<Point>>();
        router.Route(Request(new Point(0, 0), new Point(300, 150), 0, []));

        // Act
        for (var i = 0; i < nets; i++)
        {
            var request = Request(new Point(0, i * 30), new Point(300, (nets - 1 - i) * 30), i, existing);
            var route = router.Route(request);
            existing.AddRange(Segments(request, route, i).Select(s => new RouteSegment(s.Start, s.End, i)));
            all.Add(Segments(request, route, i));
            polylines.Add(RouteAssertions.Polyline(request, route));
        }

        // Assert: orthogonal, and no pair of nets runs along each other
        for (var i = 0; i < nets; i++)
        {
            Assert.True(RouteAssertions.IsOrthogonal(polylines[i]), $"Net {i} is not orthogonal");
            for (var j = i + 1; j < nets; j++)
            {
                Assert.True(OverlapLength(all[i], all[j]) < Epsilon, $"Nets {i} and {j} overlap");
            }
        }
    }

    /// <summary>
    ///     Validates that a run lining up with another net's run, ending a port pitch from where it begins, is moved to its own track, while a run far away stays on the middle channel.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_RunLiningUpWithOtherNet_MovesToItsOwnTrack()
    {
        // Arrange: the first wire's vertical run is at x=150 up to y=150; the second wire would run at x=150 from y=165 (one port pitch further)
        var router = new OrthogonalRouter();
        var first = Request(new Point(0, 0), new Point(300, 150), 0, []);
        var a = Segments(first, router.Route(first), 0);
        var close = Request(new Point(0, 400), new Point(300, 165), 1, a);
        var far = Request(new Point(0, 400), new Point(300, 255), 1, a);

        // Act
        var closeRoute = router.Route(close);
        var farRoute = router.Route(far);

        // Assert: the close one avoids x=150, the far one (gap of 105) keeps it
        Assert.Multiple(
            () => Assert.DoesNotContain(closeRoute, p => Math.Abs(p.X - 150) <= 4),
            () => Assert.Contains(farRoute, p => Math.Abs(p.X - 150) <= 4));
    }

    /// <summary>
    ///     Validates that wires of the same net may line up, as they may share a trunk.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_RunLiningUpWithSameNet_KeepsMiddleChannel()
    {
        // Arrange
        var router = new OrthogonalRouter();
        var first = Request(new Point(0, 0), new Point(300, 150), 0, []);
        var a = Segments(first, router.Route(first), 0);
        var second = Request(new Point(0, 400), new Point(300, 165), 0, a);

        // Act
        var route = router.Route(second);

        // Assert
        Assert.Contains(route, p => Math.Abs(p.X - 150) <= 4);
    }

    /// <summary>
    ///     Creates a request without obstacles.
    /// </summary>
    /// <param name="start">The start anchor.</param>
    /// <param name="end">The end anchor.</param>
    /// <param name="net">The net of the wire.</param>
    /// <param name="existing">The existing segments.</param>
    /// <returns>The request.</returns>
    private static RouteRequest Request(Point start, Point end, int net, IReadOnlyList<RouteSegment> existing) =>
        new() { Start = start, End = end, Net = net, ExistingSegments = existing };

    /// <summary>
    ///     Converts a routed wire into its segments.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="route">The route.</param>
    /// <param name="net">The net of the wire.</param>
    /// <returns>The segments as (start, end) pairs.</returns>
    private static List<RouteSegment> Segments(RouteRequest request, IReadOnlyList<Point> route, int net)
    {
        var polyline = RouteAssertions.Polyline(request, route);
        var result = new List<RouteSegment>();
        for (var i = 0; i + 1 < polyline.Count; i++)
        {
            result.Add(new RouteSegment(polyline[i], polyline[i + 1], net));
        }

        return result;
    }

    /// <summary>
    ///     Measures how much of two sets of segments runs on top of each other.
    /// </summary>
    /// <param name="a">The first set.</param>
    /// <param name="b">The second set.</param>
    /// <returns>The total collinear overlap length.</returns>
    private static double OverlapLength(IReadOnlyList<RouteSegment> a, IReadOnlyList<RouteSegment> b)
    {
        var total = 0.0;
        foreach (var (a1, a2, _) in a)
        {
            foreach (var (b1, b2, _) in b)
            {
                if (Math.Abs(a1.X - a2.X) < Epsilon && Math.Abs(b1.X - b2.X) < Epsilon && Math.Abs(a1.X - b1.X) < Epsilon)
                {
                    total += Math.Max(0, Math.Min(Math.Max(a1.Y, a2.Y), Math.Max(b1.Y, b2.Y)) - Math.Max(Math.Min(a1.Y, a2.Y), Math.Min(b1.Y, b2.Y)));
                }
                else if (Math.Abs(a1.Y - a2.Y) < Epsilon && Math.Abs(b1.Y - b2.Y) < Epsilon && Math.Abs(a1.Y - b1.Y) < Epsilon)
                {
                    total += Math.Max(0, Math.Min(Math.Max(a1.X, a2.X), Math.Max(b1.X, b2.X)) - Math.Max(Math.Min(a1.X, a2.X), Math.Min(b1.X, b2.X)));
                }
            }
        }

        return total;
    }
}
