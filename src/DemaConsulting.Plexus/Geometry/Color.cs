using System.Globalization;

namespace DemaConsulting.Plexus.Geometry;

/// <summary>
///     An immutable 32-bit RGBA color.
/// </summary>
/// <remarks>
///     Colors are stored as four bytes so they round-trip exactly through JSON snapshots and
///     map directly onto any painter's color type. The textual form is <c>#RRGGBB</c> for
///     opaque colors and <c>#AARRGGBB</c> otherwise, matching common UI conventions.
///     <para>
///         Channel order warning: the constructor takes <c>(R, G, B, A)</c> with alpha LAST and optional (255 is opaque), whereas
///         <see cref="FromArgb"/> and the <c>#AARRGGBB</c> text put alpha FIRST. <c>new Color(255, 0, 0, 128)</c> is half-transparent red;
///     <c>Color.FromArgb(128, 255, 0, 0)</c> is the same color. Derive a variant with
///         <c>color with { A = 128 }</c>. When in doubt use <see cref="Parse"/> or <see cref="FromArgb"/>.
///     </para>
/// </remarks>
/// <example>
///     Four ways to write the same half-transparent red.
///     <code>
///     using DemaConsulting.Plexus.Geometry;
///
///     var a = new Color(255, 0, 0, 128);                    // (R, G, B, A): alpha last
///     var b = Color.FromArgb(128, 255, 0, 0);               // (A, R, G, B): alpha first
///     var c = Color.Parse("#80FF0000");                     // #AARRGGBB: alpha first
///     var d = Color.Red with { A = 128 };                   // derive from a named color
///     Console.WriteLine(a == b &amp;&amp; b == c &amp;&amp; c == d);       // True
///     Console.WriteLine(Color.FromRgb(0x33, 0x66, 0x99));  // #336699 (opaque colors print without alpha)
///     </code>
/// </example>
/// <param name="R">Red channel (0-255).</param>
/// <param name="G">Green channel (0-255).</param>
/// <param name="B">Blue channel (0-255).</param>
/// <param name="A">Alpha channel (0 transparent - 255 opaque). It comes LAST, unlike <see cref="Color.FromArgb"/>.</param>
public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    /// <summary>
    ///     Gets fully transparent black.
    /// </summary>
    public static Color Transparent => new(0, 0, 0, 0);

    /// <summary>
    ///     Gets opaque black.
    /// </summary>
    public static Color Black => new(0, 0, 0);

    /// <summary>
    ///     Gets opaque white.
    /// </summary>
    public static Color White => new(255, 255, 255);

    /// <summary>
    ///     Gets opaque mid gray.
    /// </summary>
    public static Color Gray => new(128, 128, 128);

    /// <summary>
    ///     Gets opaque red.
    /// </summary>
    public static Color Red => new(255, 0, 0);

    /// <summary>
    ///     Gets opaque green.
    /// </summary>
    public static Color Green => new(0, 128, 0);

    /// <summary>
    ///     Gets opaque blue.
    /// </summary>
    public static Color Blue => new(0, 0, 255);

    /// <summary>
    ///     Gets opaque orange.
    /// </summary>
    public static Color Orange => new(255, 165, 0);

    /// <summary>
    ///     Creates an opaque color from red, green and blue channels.
    /// </summary>
    /// <param name="r">Red channel.</param>
    /// <param name="g">Green channel.</param>
    /// <param name="b">Blue channel.</param>
    /// <returns>The opaque color.</returns>
    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b);

    /// <summary>
    ///     Creates a color from alpha, red, green and blue channels (alpha FIRST, unlike the constructor, which takes alpha last).
    /// </summary>
    /// <param name="a">Alpha channel.</param>
    /// <param name="r">Red channel.</param>
    /// <param name="g">Green channel.</param>
    /// <param name="b">Blue channel.</param>
    /// <returns>The color.</returns>
    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(r, g, b, a);

    /// <summary>
    ///     Parses a <c>#RRGGBB</c> or <c>#AARRGGBB</c> hexadecimal color string.
    /// </summary>
    /// <param name="text">The text to parse; the leading <c>#</c> is required.</param>
    /// <returns>The parsed color.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the text is not a valid color.</exception>
    public static Color Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out var color)
            ? color
            : throw new FormatException($"'{text}' is not a valid color; expected #RRGGBB or #AARRGGBB.");
    }

    /// <summary>
    ///     Attempts to parse a <c>#RRGGBB</c> or <c>#AARRGGBB</c> hexadecimal color string.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="color">Receives the parsed color, or the default value on failure.</param>
    /// <returns><see langword="true"/> when parsing succeeded.</returns>
    public static bool TryParse(string? text, out Color color)
    {
        color = default;

        // Only the two documented shapes are valid: '#' followed by exactly 6 or 8 hex digits
        if (text is null || text.Length is not (7 or 9) || text[0] != '#')
        {
            return false;
        }

        // AllowHexSpecifier rejects signs and whitespace that a plain parse would accept
        if (!uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        // Six digits means opaque; eight digits carry alpha in the high byte
        var alpha = text.Length == 9 ? (byte)(value >> 24) : (byte)255;
        color = new Color((byte)(value >> 16), (byte)(value >> 8), (byte)value, alpha);
        return true;
    }

    /// <summary>
    ///     Formats the color as <c>#RRGGBB</c> when opaque, otherwise <c>#AARRGGBB</c>.
    /// </summary>
    /// <returns>The hexadecimal color text.</returns>
    public override string ToString() =>
        A == 255
            ? string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}")
            : string.Create(CultureInfo.InvariantCulture, $"#{A:X2}{R:X2}{G:X2}{B:X2}");
}
