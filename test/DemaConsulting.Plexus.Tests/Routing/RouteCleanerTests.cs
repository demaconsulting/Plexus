using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="RouteCleaner"/>: merging collinear bends and removing zero-length segments without changing the drawn shape.
/// </summary>
public class RouteCleanerTests
{
    /// <summary>
    ///     Validates that a bend on a straight run between its neighbors is removed.
    /// </summary>
    [Fact]
    public void RouteCleaner_Clean_CollinearBend_IsMerged()
    {
        // Arrange: (0,0) (30,0) (60,0) (60,40) (100,40): the bend at (30,0) does not bend
        var route = new[] { new Point(30, 0), new Point(60, 0), new Point(60, 40) };

        // Act
        var cleaned = RouteCleaner.Clean(new Point(0, 0), route, new Point(100, 40));

        // Assert: only the two real corners remain
        Assert.Equal([new Point(60, 0), new Point(60, 40)], cleaned);
    }

    /// <summary>
    ///     Validates that a chain of collinear bends collapses completely and that removal cascades.
    /// </summary>
    [Fact]
    public void RouteCleaner_Clean_ChainOfCollinearBends_CollapsesToStraightLine()
    {
        // Arrange: every bend lies on the line from start to end
        var route = new[] { new Point(10, 5), new Point(20, 5), new Point(35, 5) };

        // Act
        var cleaned = RouteCleaner.Clean(new Point(0, 5), route, new Point(100, 5));

        // Assert
        Assert.Empty(cleaned);
    }

    /// <summary>
    ///     Validates that repeated points and bends equal to an end point (zero-length segments) are removed.
    /// </summary>
    [Fact]
    public void RouteCleaner_Clean_ZeroLengthSegments_AreRemoved()
    {
        // Arrange: a duplicated bend, a bend on the start anchor and a bend on the end anchor
        var route = new[] { new Point(0, 0), new Point(50, 0), new Point(50, 0), new Point(50, 80), new Point(100, 80) };

        // Act
        var cleaned = RouteCleaner.Clean(new Point(0, 0), route, new Point(100, 80));

        // Assert: the genuine corners only
        Assert.Equal([new Point(50, 0), new Point(50, 80)], cleaned);
    }

    /// <summary>
    ///     Validates that a genuine reversal (the wire doubles back) is kept, because removing it would change the drawn shape.
    /// </summary>
    [Fact]
    public void RouteCleaner_Clean_Reversal_IsKept()
    {
        // Arrange: out to x=100 and back to x=40 along the same line
        var route = new[] { new Point(100, 0) };

        // Act
        var cleaned = RouteCleaner.Clean(new Point(0, 0), route, new Point(40, 0));

        // Assert
        Assert.Equal([new Point(100, 0)], cleaned);
    }

    /// <summary>
    ///     Validates that cleaning is idempotent and leaves an already clean route untouched.
    /// </summary>
    [Fact]
    public void RouteCleaner_Clean_AlreadyCleanRoute_IsUnchangedAndStable()
    {
        // Arrange
        Point[] route = [new Point(15, 0), new Point(15, 60), new Point(90, 60)];

        // Act
        var once = RouteCleaner.Clean(new Point(0, 0), route, new Point(90, 100));
        var twice = RouteCleaner.Clean(new Point(0, 0), once, new Point(90, 100));

        // Assert
        Assert.Equal(route, once);
        Assert.Equal(once, twice);
    }

    /// <summary>
    ///     Validates that a null route is rejected.
    /// </summary>
    [Fact]
    public void RouteCleaner_Clean_NullRoute_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => RouteCleaner.Clean(default, null!, default));
    }
}
