namespace DemaConsulting.Plexus.Rendering.Tests;

/// <summary>
///     Tests for <see cref="IconCache"/>.
/// </summary>
public class IconCacheTests
{
    /// <summary>
    ///     Validates that the same icon at the same pixel size is rasterized once and shared.
    /// </summary>
    [Fact]
    public void IconCache_Get_SameKeyAndSize_ReturnsSharedSurface()
    {
        // Arrange: an icon unique to this test so parallel tests cannot interfere
        var icon = IconSource.FromSvg($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 8 8\"><rect width=\"8\" height=\"8\" fill=\"#00FF00\"/><!-- {Guid.NewGuid()} --></svg>");

        // Act
        var first = IconCache.Get(icon, 16, 16);
        var second = IconCache.Get(icon, 16, 16);
        var otherSize = IconCache.Get(icon, 20, 20);

        // Assert
        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.NotSame(first, otherSize);
        Assert.Equal((20, 20), (otherSize!.Width, otherSize.Height));
    }

    /// <summary>
    ///     Validates that a malformed SVG yields null (and is remembered as such).
    /// </summary>
    [Fact]
    public void IconCache_Get_MalformedSvg_ReturnsNull()
    {
        // Arrange
        var icon = IconSource.FromSvg($"definitely not svg {Guid.NewGuid()}");

        // Act / Assert
        Assert.Null(IconCache.Get(icon, 10, 10));
        Assert.Null(IconCache.Get(icon, 10, 10));
    }

    /// <summary>
    ///     Validates that the cache does not grow without bound when many distinct icon sizes are requested.
    /// </summary>
    [Fact]
    public void IconCache_Get_ManyDistinctIcons_KeepsCacheBounded()
    {
        // Arrange: far more distinct icons than the cache is allowed to hold
        const int requests = 700;

        // Act
        for (var index = 0; index < requests; index++)
        {
            IconCache.Get(IconSource.FromSvg($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 4 4\"><rect width=\"4\" height=\"4\" fill=\"#0000FF\"/><!-- {Guid.NewGuid()} --></svg>"), 2, 2);
        }

        // Assert: the cache was cleared along the way instead of holding every request
        Assert.True(IconCache.Count < requests, $"The cache holds {IconCache.Count} entries.");
    }

    /// <summary>
    ///     Validates that concurrent requests for the same icon and size all receive the same shared surface.
    /// </summary>
    [Fact]
    public void IconCache_Get_ConcurrentRequests_ReturnTheSameSurface()
    {
        // Arrange: an icon unique to this test
        var icon = IconSource.FromSvg($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 8 8\"><rect width=\"8\" height=\"8\" fill=\"#FF00FF\"/><!-- {Guid.NewGuid()} --></svg>");
        var results = new object?[32];

        // Act
        Parallel.For(0, results.Length, index => results[index] = IconCache.Get(icon, 12, 12));

        // Assert
        Assert.NotNull(results[0]);
        Assert.All(results, result => Assert.Same(results[0], result));
    }
}
