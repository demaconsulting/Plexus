using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.Plexus.Rendering;

/// <summary>
///     Rasterizes node icons once and reuses the pixels for every later paint of the same icon at
///     the same pixel size.
/// </summary>
/// <remarks>
///     Rasterizing SVG is by far the most expensive step of painting a node, and diagrams repeat
///     the same few icons many times, so the cache is keyed by <see cref="IconSource.Key"/> and
///     the target pixel size. Cached surfaces are never mutated after creation, which makes
///     sharing them between renders and threads safe. A failed rasterization is cached as null so
///     a broken icon is not re-parsed for every node. The cache is bounded by its
///     <see cref="Capacity"/> (<see cref="DefaultCapacity"/> for <see cref="Shared"/>): a request for an
///     uncached icon size that finds the cache full first discards every cached rasterization and then
///     adds the new one, because hosts that animate zoom levels would otherwise accumulate one surface
///     per zoom step. The lookup, eviction and insertion run under one lock (rasterizing does not), so the cache never holds
///     more than <see cref="Capacity"/> entries, also when several renders request icons at once. The painter uses the
///     <see cref="Shared"/> instance; tests create private instances with a small capacity.
/// </remarks>
internal sealed class IconCache
{
    /// <summary>
    ///     The number of rasterizations the shared cache holds before it is cleared.
    /// </summary>
    public const int DefaultCapacity = 512;

    /// <summary>
    ///     Rasterized icons keyed by icon key and pixel size; null marks an icon that failed to load. Guarded by <see cref="_gate"/>.
    /// </summary>
    private readonly Dictionary<(string Key, int Width, int Height), Surface?> _cache = [];

    /// <summary>
    ///     Serializes the lookup, eviction and insertion so the capacity also holds under concurrent renders; rasterizing runs
    ///     outside it.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="IconCache"/> class.
    /// </summary>
    /// <param name="capacity">The number of rasterizations held before the cache is cleared; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is less than 1.</exception>
    internal IconCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    /// <summary>
    ///     Gets the cache shared by every render in the process, with a capacity of <see cref="DefaultCapacity"/>.
    /// </summary>
    public static IconCache Shared { get; } = new(DefaultCapacity);

    /// <summary>
    ///     Gets the number of rasterizations the cache holds before it is cleared.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    ///     Gets the number of rasterizations currently cached.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>
    ///     Gets the rasterized icon for a pixel size, rasterizing it on the first request.
    /// </summary>
    /// <param name="icon">The icon to rasterize.</param>
    /// <param name="width">The width in pixels; at least 1.</param>
    /// <param name="height">The height in pixels; at least 1.</param>
    /// <returns>The shared surface (straight alpha, transparent background), or null when the SVG cannot be rasterized.</returns>
    public Surface? Get(IconSource icon, int width, int height)
    {
        var key = (icon.Key, width, height);
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        // Rasterizing is slow, so it runs outside the lock; two threads missing the same key at once may both rasterize
        var surface = Rasterize(icon, width, height);
        lock (_gate)
        {
            // A racing thread that stored the key first wins, so every caller shares one surface
            if (_cache.TryGetValue(key, out var winner))
            {
                return winner;
            }

            // Bound the memory held by stale sizes: a new size that finds the cache full empties it first.
            // Losing the cache only costs a re-rasterization
            if (_cache.Count >= Capacity)
            {
                _cache.Clear();
            }

            _cache.Add(key, surface);
            return surface;
        }
    }

    /// <summary>
    ///     Rasterizes an icon's SVG.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The surface, or null when the SVG is malformed or uses unsupported features.</returns>
    private static Surface? Rasterize(IconSource icon, int width, int height)
    {
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(icon.SvgText));
            return SvgCodec.Load(stream, width, height, null);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            // A bad icon must not prevent the rest of the diagram from rendering
            return null;
        }
    }
}
