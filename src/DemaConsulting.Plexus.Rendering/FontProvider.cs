using System.Collections.Concurrent;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.Plexus.Rendering;

/// <summary>
///     Resolves theme font families to CanvasNet fonts, shared by the text measurer and the painter.
/// </summary>
/// <remarks>
///     Resolution prefers an installed system font of the requested family (so output matches what
///     the host's own UI would show) and falls back to the bundled Liberation Sans, which is always
///     available. Both measuring and painting must use the very same font for layout to match the
///     pixels, which is why this lookup is centralized. Results are cached for the process
///     lifetime; the class is thread-safe.
/// </remarks>
internal static class FontProvider
{
    /// <summary>
    ///     Fonts already resolved, keyed by family name and weight.
    /// </summary>
    private static readonly ConcurrentDictionary<(string Family, bool Bold), TrueTypeFont> Cache = new();

    /// <summary>
    ///     Gets the font for a family and weight.
    /// </summary>
    /// <param name="family">The font family name (case-insensitive).</param>
    /// <param name="bold">True for the bold face.</param>
    /// <returns>A loaded font; never null.</returns>
    public static TrueTypeFont Get(string family, bool bold) =>
        Cache.GetOrAdd((family.ToUpperInvariant(), bold), static (key, state) => Load(state.Family, state.Bold), (Family: family, Bold: bold));

    /// <summary>
    ///     Loads a font from the system catalog, or the bundled fallback when none is usable.
    /// </summary>
    /// <param name="family">The requested family.</param>
    /// <param name="bold">True for the bold face.</param>
    /// <returns>The loaded font.</returns>
    private static TrueTypeFont Load(string family, bool bold)
    {
        // A system match can still fail to parse (unsupported CFF flavours, corrupt files), and
        // that must never stop rendering, so any such failure falls through to the bundled font
        var match = SystemFontCatalog.FindBestMatch(family, bold, false, false, false);
        if (match is { } info)
        {
            try
            {
                return TrueTypeFont.Load(info.FilePath, info.FaceIndex);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                // Intentionally ignored: the bundled fallback below is always available
            }
        }

        return SystemFontCatalog.LoadBundledFallback(false, false, bold, false);
    }
}
