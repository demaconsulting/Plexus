using System.Collections.Concurrent;
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
///     a broken icon is not re-parsed for every node. The cache is bounded: when it grows past
///     <see cref="MaxEntries"/> it is simply cleared, because hosts that animate zoom levels
///     would otherwise accumulate one surface per zoom step.
/// </remarks>
internal static class IconCache
{
    /// <summary>
    ///     The number of cached rasterizations after which the cache is cleared.
    /// </summary>
    private const int MaxEntries = 512;

    /// <summary>
    ///     Rasterized icons keyed by icon key and pixel size; null marks an icon that failed to load.
    /// </summary>
    private static readonly ConcurrentDictionary<(string Key, int Width, int Height), Surface?> Cache = new();

    /// <summary>
    ///     Gets the number of rasterizations currently cached.
    /// </summary>
    public static int Count => Cache.Count;

    /// <summary>
    ///     Gets the rasterized icon for a pixel size, rasterizing it on the first request.
    /// </summary>
    /// <param name="icon">The icon to rasterize.</param>
    /// <param name="width">The width in pixels; at least 1.</param>
    /// <param name="height">The height in pixels; at least 1.</param>
    /// <returns>The shared surface (straight alpha, transparent background), or null when the SVG cannot be rasterized.</returns>
    public static Surface? Get(IconSource icon, int width, int height)
    {
        // Bound the memory held by stale sizes; losing the cache only costs a re-rasterization
        if (Cache.Count > MaxEntries)
        {
            Cache.Clear();
        }

        return Cache.GetOrAdd((icon.Key, width, height), static (_, state) => Rasterize(state.Icon, state.Width, state.Height), (Icon: icon, Width: width, Height: height));
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
