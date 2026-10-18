namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Unit tests for the uniform-grid spatial index and <see cref="DiagramScene.Query"/>.
/// </summary>
public class SpatialIndexTests
{
    /// <summary>
    ///     Validates that a query returns only items whose bounds intersect the region, in insertion order.
    /// </summary>
    [Fact]
    public void SpatialIndex_Query_ReturnsIntersectingItemsInInsertionOrder()
    {
        // Arrange
        var index = new SpatialIndex<string>(50);
        index.Add("a", new Rect(0, 0, 10, 10));
        index.Add("far", new Rect(500, 500, 10, 10));
        index.Add("b", new Rect(20, 20, 10, 10));
        index.Add("outside", new Rect(0, 100, 10, 10));

        // Act
        var result = index.Query(new Rect(-5, -5, 40, 40));

        // Assert
        Assert.Equal(["a", "b"], result);
        Assert.Equal(4, index.Count);
    }

    /// <summary>
    ///     Validates that an item spanning many cells is returned once.
    /// </summary>
    [Fact]
    public void SpatialIndex_Query_ItemSpanningCells_IsReturnedOnce()
    {
        // Arrange: 100 x 100 at cell size 20 touches 36 cells (bucketed, not "large")
        var index = new SpatialIndex<string>(20);
        index.Add("wide", new Rect(5, 5, 100, 100));

        // Act
        var result = index.Query(new Rect(0, 0, 200, 200));

        // Assert
        Assert.Equal(["wide"], result);
    }

    /// <summary>
    ///     Validates that very large items are still found by small queries and by far queries only when they intersect.
    /// </summary>
    [Fact]
    public void SpatialIndex_Query_LargeItem_IsFoundWhenIntersecting()
    {
        // Arrange: far more cells than the bucketing limit
        var index = new SpatialIndex<string>(10);
        index.Add("huge", new Rect(0, 0, 100_000, 100_000));
        index.Add("small", new Rect(50, 50, 5, 5));

        // Act
        var inside = index.Query(new Rect(52, 52, 1, 1));
        var outside = index.Query(new Rect(200_000, 200_000, 5, 5));

        // Assert
        Assert.Equal(["huge", "small"], inside);
        Assert.Empty(outside);
    }

    /// <summary>
    ///     Validates that a query much larger than the populated area still returns everything.
    /// </summary>
    [Fact]
    public void SpatialIndex_Query_HugeRegion_ReturnsAllIntersecting()
    {
        // Arrange
        var index = new SpatialIndex<int>(10);
        for (var i = 0; i < 20; i++)
        {
            index.Add(i, new Rect(i * 30.0, 0, 5, 5));
        }

        // Act
        var result = index.Query(new Rect(-1_000_000, -1_000_000, 2_000_000, 2_000_000));

        // Assert
        Assert.Equal(Enumerable.Range(0, 20), result);
    }

    /// <summary>
    ///     Validates that negative coordinates are indexed correctly.
    /// </summary>
    [Fact]
    public void SpatialIndex_Query_NegativeCoordinates_AreFound()
    {
        // Arrange
        var index = new SpatialIndex<string>(50);
        index.Add("neg", new Rect(-120, -80, 20, 20));

        // Act / Assert
        Assert.Equal(["neg"], index.Query(new Rect(-115, -75, 2, 2)));
        Assert.Empty(index.Query(new Rect(0, 0, 10, 10)));
    }

    /// <summary>
    ///     Validates that a non-positive cell size is rejected.
    /// </summary>
    [Fact]
    public void SpatialIndex_Constructor_NonPositiveCellSize_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialIndex<int>(0));
    }

    /// <summary>
    ///     Validates that DiagramScene.Query agrees with a brute-force intersection test and preserves drawing order.
    /// </summary>
    [Fact]
    public void Scene_Query_MatchesBruteForceAndKeepsDrawingOrder()
    {
        // Arrange: a spread of nodes and wires, queried over several regions
        var (_, page) = SceneTestModel.CreatePage();
        Node? previous = null;
        for (var i = 0; i < 12; i++)
        {
            var node = page.AddNode("acme.Gain", i % 4 * 250.0, Math.Floor(i / 4.0) * 250.0, null);
            if (previous is not null)
            {
                page.Connect(previous.Port("out"), node.Port("in"));
            }

            previous = node;
        }

        var scene = SceneBuilder.Build(page);
        Rect[] regions =
        [
            new Rect(0, 0, 100, 100),
            new Rect(200, 200, 400, 300),
            new Rect(-50, -50, 2000, 2000),
            new Rect(5000, 5000, 10, 10),
        ];

        // Act / Assert
        foreach (var region in regions)
        {
            var expected = scene.Primitives.Where(p => p.Bounds.Intersects(region)).ToList();
            var actual = scene.Query(region);
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    ///     Validates that a small query only returns nearby primitives, not the whole scene.
    /// </summary>
    [Fact]
    public void Scene_Query_SmallRegion_ReturnsOnlyNearbyPrimitives()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var near = page.AddNode("acme.Producer", 0, 0, "Near");
        var far = page.AddNode("acme.Consumer", 3000, 3000, "Far");
        var scene = SceneTestModel.BuildNoGrid(page);

        // Act
        var result = scene.Query(near.Bounds);

        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, p => Assert.Same(near, p.Element));
        Assert.DoesNotContain(result, p => ReferenceEquals(p.Element, far));
    }
}
