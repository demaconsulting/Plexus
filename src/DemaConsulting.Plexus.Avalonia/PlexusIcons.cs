using System.Collections.Concurrent;
using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Turns Plexus <see cref="IconSource"/> values into Avalonia images, for hosts that show node-type icons themselves (palettes, trees, menus).
/// </summary>
/// <remarks>
///     The editor paints node icons through the same cache, so an icon used both on the canvas and in a
///     host palette is parsed once. The cache is process-wide, keyed by <see cref="IconSource.Key"/>,
///     and thread-safe. It is bounded: when it holds more than 2048 icons it is cleared, so hosts that
///     load transient icons do not grow memory without limit; a cleared icon is simply loaded again on
///     its next use.
/// </remarks>
/// <example>
///     Show a node type's icon in your own control, for example a menu item or a tree node.
///     <code>
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia;
///
///     var type = types.Get("demo.Source");
///     if (type.Icon is { } icon &amp;&amp; PlexusIcons.GetImage(icon) is { } source)   // null when the SVG could not be loaded
///     {
///         var image = new Image { Source = source, Width = 16, Height = 16 };
///         var row = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Children = { image, new TextBlock { Text = type.DisplayName } } };
///     }
///     </code>
/// </example>
public static class PlexusIcons
{
    /// <summary>
    ///     The number of cached icons after which the cache is cleared; large enough for big tool palettes.
    /// </summary>
    internal const int MaxEntries = 2048;

    /// <summary>
    ///     Cached images by icon key; a null entry records an icon that failed to load so it is not retried.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SvgImage?> Cache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the number of icons currently cached.
    /// </summary>
    internal static int Count => Cache.Count;

    /// <summary>
    ///     Gets an Avalonia image for an icon, loading and caching it on first use.
    /// </summary>
    /// <remarks>
    ///     Icon SVG is host-supplied, so a malformed document never throws here: the result is
    ///     <see langword="null"/> and the failure is remembered. The returned image is shared, so do
    ///     not modify it.
    /// </remarks>
    /// <example>
    ///     <code><![CDATA[
    ///     using Avalonia.Controls;
    ///     using DemaConsulting.Plexus;
    ///     using DemaConsulting.Plexus.Avalonia;
    ///
    ///     static class IconSample
    ///     {
    ///         // Returns an Image showing the node type's icon, or null when the type has none or its SVG cannot be loaded
    ///         public static Image? IconFor(NodeType nodeType) =>
    ///             nodeType.Icon is { } icon && PlexusIcons.GetImage(icon) is { } source
    ///                 ? new Image { Source = source, Width = 16, Height = 16 }
    ///                 : null;
    ///     }
    ///     ]]></code>
    /// </example>
    /// <param name="icon">The icon.</param>
    /// <returns>The image, or <see langword="null"/> when the SVG could not be loaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="icon"/> is null.</exception>
    public static IImage? GetImage(IconSource icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        return GetSvgImage(icon);
    }

    /// <summary>
    ///     Gets the cached SVG image for an icon, loading it on first use.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The image, or null when the SVG could not be loaded.</returns>
    internal static SvgImage? GetSvgImage(IconSource icon)
    {
        // Bound the memory held by transient icons; losing the cache only costs a re-load
        if (Cache.Count > MaxEntries)
        {
            Cache.Clear();
        }

        return Cache.GetOrAdd(icon.Key, static (_, source) => Load(source), icon);
    }

    /// <summary>
    ///     Parses an icon's SVG text.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The image, or null when the SVG could not be loaded.</returns>
    private static SvgImage? Load(IconSource icon)
    {
        try
        {
            var source = SvgSource.LoadFromSvg(icon.SvgText);
            return source is null ? null : new SvgImage { Source = source };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Deliberately swallowed: a broken host icon must not take the UI down; null means "no image"
            return null;
        }
    }
}
