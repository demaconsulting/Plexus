using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Creates Avalonia <see cref="FormattedText"/> from painter-agnostic <see cref="TextStyle"/> values.
/// </summary>
/// <remarks>
///     Measuring and painting must format text identically, otherwise the boxes the scene lays out
///     would not match the glyphs that are drawn. Both therefore go through this one factory.
///     Typefaces are cached because they are created for every primitive of every frame.
/// </remarks>
internal static class TextFormats
{
    /// <summary>
    ///     Typefaces already created, by family name and weight.
    /// </summary>
    private static readonly Dictionary<(string Family, bool Bold), Typeface> Typefaces = [];

    /// <summary>
    ///     Creates formatted text for a string and style.
    /// </summary>
    /// <remarks>
    ///     Empty text is formatted as a single space, because Avalonia reports no line height for an
    ///     empty string while the scene still needs one to size the (empty) rectangle.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="style">The style: family, size, weight and color.</param>
    /// <returns>The formatted text, without alignment or wrapping.</returns>
    public static FormattedText Create(string text, TextStyle style)
    {
        // The foreground brush is baked in at construction; measuring ignores it but it is cheap
        return new FormattedText(
            text.Length == 0 ? " " : text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            GetTypeface(style),
            style.FontSize,
            new ImmutableSolidColorBrush(ToAvaloniaColor(style.Color)));
    }

    /// <summary>
    ///     Gets the typeface for a style, creating and caching it on first use.
    /// </summary>
    /// <param name="style">The text style.</param>
    /// <returns>The typeface.</returns>
    private static Typeface GetTypeface(TextStyle style)
    {
        // Typefaces are only ever used on the UI thread, so a plain dictionary suffices
        var key = (style.FontFamily, style.Bold);
        if (!Typefaces.TryGetValue(key, out var typeface))
        {
            typeface = new Typeface(new FontFamily(style.FontFamily), FontStyle.Normal, style.Bold ? FontWeight.Bold : FontWeight.Normal);
            Typefaces[key] = typeface;
        }

        return typeface;
    }

    /// <summary>
    ///     Converts a Plexus color to an Avalonia color.
    /// </summary>
    /// <param name="color">The Plexus color.</param>
    /// <returns>The Avalonia color.</returns>
    private static Color ToAvaloniaColor(PlexusColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);
}
