using System.Globalization;
using AvaloniaColor = Avalonia.Media.Color;
using DrawingColor = System.Drawing.Color;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Reads, formats and parses the color types the grid's color editor supports: <c>Avalonia.Media.Color</c> and
///     <c>System.Drawing.Color</c> (the type WinForms users know).
/// </summary>
internal static class ColorSupport
{
    /// <summary>
    ///     Gets the Avalonia color of a property value.
    /// </summary>
    /// <param name="value">The value (an Avalonia or System.Drawing color, or <see langword="null"/>).</param>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true"/> when the value is a color.</returns>
    internal static bool TryGetColor(object? value, out AvaloniaColor color)
    {
        switch (value)
        {
            case AvaloniaColor avalonia:
                color = avalonia;
                return true;
            case DrawingColor drawing:
                color = AvaloniaColor.FromArgb(drawing.A, drawing.R, drawing.G, drawing.B);
                return true;
            default:
                color = default;
                return false;
        }
    }

    /// <summary>
    ///     Formats a color value as <c>#RRGGBB</c> (or <c>#AARRGGBB</c> when translucent).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text; empty when the value is not a color.</returns>
    internal static string Format(object? value)
    {
        if (!TryGetColor(value, out var color))
        {
            return string.Empty;
        }

        return color.A == 255
            ? string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}")
            : string.Create(CultureInfo.InvariantCulture, $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    /// <summary>
    ///     Parses color text into a value of a color type.
    /// </summary>
    /// <param name="text">The text (<c>#RGB</c>, <c>#RRGGBB</c>, <c>#AARRGGBB</c> or a color name).</param>
    /// <param name="targetType">The color type to produce.</param>
    /// <param name="value">The color value.</param>
    /// <returns><see langword="true"/> when the text is a color.</returns>
    internal static bool TryParse(string text, Type targetType, out object? value)
    {
        value = null;
        if (!AvaloniaColor.TryParse(text.Trim(), out var color))
        {
            return false;
        }

        value = targetType == typeof(DrawingColor) ? DrawingColor.FromArgb(color.A, color.R, color.G, color.B) : color;
        return true;
    }
}
