namespace DemaConsulting.Plexus.Rendering.Tests;

/// <summary>
///     Tests for <see cref="IconCache"/>.
/// </summary>
public class IconCacheTests
{
    /// <summary>
    ///     Creates an SVG icon that has a key of its own, so tests cannot share cache entries by accident.
    /// </summary>
    /// <param name="color">The fill color of the icon's square.</param>
    /// <returns>The icon.</returns>
    private static IconSource UniqueIcon(string color) =>
        IconSource.FromSvg($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 8 8\"><rect width=\"8\" height=\"8\" fill=\"{color}\"/><!-- {Guid.NewGuid()} --></svg>");

    /// <summary>
    ///     Validates that the same icon at the same pixel size is rasterized once and shared, and another size gets its own surface.
    /// </summary>
    [Fact]
    public void IconCache_Get_SameKeyAndSize_ReturnsSharedSurface()
    {
        // Arrange: a private cache and an icon unique to this test
        var cache = new IconCache(IconCache.DefaultCapacity);
        var icon = UniqueIcon("#00FF00");

        // Act
        var first = cache.Get(icon, 16, 16);
        var second = cache.Get(icon, 16, 16);
        var otherSize = cache.Get(icon, 20, 20);

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
        var cache = new IconCache(IconCache.DefaultCapacity);
        var icon = IconSource.FromSvg($"definitely not svg {Guid.NewGuid()}");

        // Act / Assert: null both times, and the failure occupies one cache entry
        Assert.Null(cache.Get(icon, 10, 10));
        Assert.Null(cache.Get(icon, 10, 10));
        Assert.Equal(1, cache.Count);
    }

    /// <summary>
    ///     Validates that the cache never holds more rasterizations than its capacity when many distinct icon sizes are requested.
    /// </summary>
    [Fact]
    public void IconCache_Get_MoreSizesThanCapacity_NeverHoldsMoreThanCapacity()
    {
        // Arrange: a cache of four entries and one icon requested at twelve distinct sizes
        var cache = new IconCache(4);
        var icon = UniqueIcon("#0000FF");

        // Act / Assert: after every request the count is within the capacity
        for (var size = 1; size <= 12; size++)
        {
            Assert.NotNull(cache.Get(icon, size, size));
            Assert.InRange(cache.Count, 1, 4);
        }
    }

    /// <summary>
    ///     Validates that the cache does not grow without bound under the default capacity.
    /// </summary>
    [Fact]
    public void IconCache_Get_ManyDistinctIcons_KeepsCacheBounded()
    {
        // Arrange: far more distinct icons than the default capacity
        var cache = new IconCache(IconCache.DefaultCapacity);
        const int requests = 700;

        // Act
        for (var index = 0; index < requests; index++)
        {
            cache.Get(UniqueIcon("#0000FF"), 2, 2);
            Assert.True(cache.Count <= IconCache.DefaultCapacity, $"The cache holds {cache.Count} entries.");
        }

        // Assert: the cache was emptied along the way instead of holding every request
        Assert.True(cache.Count < requests);
    }

    /// <summary>
    ///     Validates that a request for a new size that finds the cache full discards the earlier rasterizations first.
    /// </summary>
    [Fact]
    public void IconCache_Get_FullCache_DiscardsEarlierRasterizations()
    {
        // Arrange: a cache of four entries, filled with four sizes of one icon
        var cache = new IconCache(4);
        var icon = UniqueIcon("#FF8800");
        var first = cache.Get(icon, 1, 1);
        for (var size = 2; size <= 4; size++)
        {
            cache.Get(icon, size, size);
        }

        Assert.Equal(4, cache.Count);

        // Act: a fifth size finds the cache full; then the first size is requested again
        cache.Get(icon, 5, 5);
        var countAfterEviction = cache.Count;
        var firstAgain = cache.Get(icon, 1, 1);

        // Assert: only the new entry remained after the discard, and the first size was rasterized anew
        Assert.Equal(1, countAfterEviction);
        Assert.NotNull(first);
        Assert.NotSame(first, firstAgain);
    }

    /// <summary>
    ///     Validates that requesting an icon size that is already cached does not discard anything when the cache is full.
    /// </summary>
    [Fact]
    public void IconCache_Get_FullCacheCachedSize_KeepsEntries()
    {
        // Arrange: a full cache of two entries
        var cache = new IconCache(2);
        var icon = UniqueIcon("#123456");
        var first = cache.Get(icon, 1, 1);
        cache.Get(icon, 2, 2);

        // Act
        var again = cache.Get(icon, 1, 1);

        // Assert
        Assert.Same(first, again);
        Assert.Equal(2, cache.Count);
    }

    /// <summary>
    ///     Validates the capacity of the shared cache and that a cache needs a capacity of at least one.
    /// </summary>
    [Fact]
    public void IconCache_Shared_HasCapacityOf512()
    {
        // Act / Assert
        Assert.Equal(512, IconCache.DefaultCapacity);
        Assert.Equal(512, IconCache.Shared.Capacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => new IconCache(0));
    }

    /// <summary>
    ///     Validates that concurrent requests for the same icon and size all receive the same shared surface.
    /// </summary>
    [Fact]
    public void IconCache_Get_ConcurrentRequests_ReturnTheSameSurface()
    {
        // Arrange: a private cache and an icon unique to this test
        var cache = new IconCache(IconCache.DefaultCapacity);
        var icon = UniqueIcon("#FF00FF");
        var results = new object?[32];

        // Act
        Parallel.For(0, results.Length, index => results[index] = cache.Get(icon, 12, 12));

        // Assert
        Assert.NotNull(results[0]);
        Assert.All(results, result => Assert.Same(results[0], result));
    }
}
