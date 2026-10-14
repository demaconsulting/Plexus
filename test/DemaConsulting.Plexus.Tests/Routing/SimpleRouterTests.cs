using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="SimpleRouter"/>: elbow shape, loop for backward targets, snapping and independence from obstacles.
/// </summary>
public class SimpleRouterTests
{
    /// <summary>
    ///     Validates that a target ahead of the source gets a single vertical run at the grid-snapped midpoint of the stubs.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_TargetAhead_ProducesSnappedElbow()
    {
        // Arrange: the stubs end at x = 115 and x = 385, so the midpoint 250 snaps to 255
        var request = new RouteRequest { Start = new Point(100, 50), End = new Point(400, 200) };

        // Act
        var route = new SimpleRouter().Route(request);

        // Assert
        Assert.Equal(new[] { new Point(255, 50), new Point(255, 200) }, route);
    }

    /// <summary>
    ///     Validates that ports on the same row, with the target ahead, are joined by a straight wire.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_SameRowAhead_ReturnsNoBendPoints()
    {
        // Arrange
        var request = new RouteRequest { Start = new Point(0, 50), End = new Point(200, 50) };

        // Act
        var route = new SimpleRouter().Route(request);

        // Assert
        Assert.Empty(route);
    }

    /// <summary>
    ///     Validates that a target behind the source gets a loop that leaves through the start stub and enters through the end stub.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_TargetBehind_LoopsThroughBothStubs()
    {
        // Arrange: the stubs end at x = 315 (leaving right) and x = 85 (entering from the left); the channel is at y = 120
        var request = new RouteRequest { Start = new Point(300, 50), End = new Point(100, 200) };

        // Act
        var route = new SimpleRouter().Route(request);

        // Assert
        var expected = new[] { new Point(315, 50), new Point(315, 120), new Point(85, 120), new Point(85, 200) };
        Assert.Equal(expected, route);
        Assert.True(RouteAssertions.IsOrthogonal(RouteAssertions.Polyline(request, route)));
    }

    /// <summary>
    ///     Validates that a backward target on the same row is routed through a channel below the row instead of straight through the nodes.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_TargetBehindOnSameRow_UsesChannelBelowTheRow()
    {
        // Arrange
        var request = new RouteRequest { Start = new Point(300, 50), End = new Point(100, 50) };

        // Act
        var route = new SimpleRouter().Route(request);

        // Assert
        Assert.Equal(new[] { new Point(315, 50), new Point(315, 80), new Point(85, 80), new Point(85, 50) }, route);
    }

    /// <summary>
    ///     Validates that disabling grid snapping places the vertical run exactly at the midpoint.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_GridSizeZero_DoesNotSnap()
    {
        // Arrange
        var request = new RouteRequest { Start = new Point(100, 50), End = new Point(400, 200), GridSize = 0 };

        // Act
        var route = new SimpleRouter().Route(request);

        // Assert
        Assert.Equal(new[] { new Point(250, 50), new Point(250, 200) }, route);
    }

    /// <summary>
    ///     Validates that snapping never moves the vertical run outside the two stubs.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_SnappingWouldLeaveStubRange_KeepsUnsnappedMidpoint()
    {
        // Arrange: the stubs end at x = 115 and x = 118; the midpoint 116.5 would snap to 120, beyond the end stub
        var request = new RouteRequest { Start = new Point(100, 50), End = new Point(133, 200) };

        // Act
        var route = new SimpleRouter().Route(request);

        // Assert
        Assert.Equal(new[] { new Point(116.5, 50), new Point(116.5, 200) }, route);
    }

    /// <summary>
    ///     Validates that obstacles have no influence on the route.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_Obstacles_AreIgnored()
    {
        // Arrange
        var free = new RouteRequest { Start = new Point(100, 50), End = new Point(400, 200) };
        var blocked = new RouteRequest
        {
            Start = free.Start,
            End = free.End,
            Obstacles = [new Rect(200, 0, 120, 300), new Rect(0, 0, 500, 40)],
        };

        // Act / Assert
        Assert.Equal(new SimpleRouter().Route(free), new SimpleRouter().Route(blocked));
    }

    /// <summary>
    ///     Validates that routing the same request repeatedly, with separate instances, yields identical points.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_SameRequest_IsDeterministic()
    {
        // Arrange
        var request = new RouteRequest { Start = new Point(300, 50), End = new Point(100, 200) };

        // Act
        var first = new SimpleRouter().Route(request);
        var second = new SimpleRouter().Route(request);

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Validates that a null request is rejected.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new SimpleRouter().Route(null!));
    }
}
