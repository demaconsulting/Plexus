namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     A font-free text measurer that estimates sizes from per-character width classes.
/// </summary>
/// <remarks>
///     It works with no painter and no installed fonts, so scenes can be built, hit tested and
///     tested headlessly. Estimates are tuned for proportional sans-serif faces such as Segoe UI
///     and are accurate enough "to fool the eye"; use a painter-backed measurer when exact
///     metrics matter. Stateless and thread-safe.
/// </remarks>
public sealed class ApproximateTextMeasurer : ITextMeasurer
{
    /// <summary>
    ///     Average glyph width relative to the font size for ordinary lower-case letters and digits.
    /// </summary>
    private const double NormalWidth = 0.53;

    /// <summary>
    ///     Glyph width relative to the font size for narrow glyphs such as <c>i</c>, <c>l</c> and punctuation.
    /// </summary>
    private const double NarrowWidth = 0.28;

    /// <summary>
    ///     Glyph width relative to the font size for wide glyphs such as <c>m</c>, <c>w</c> and capitals.
    /// </summary>
    private const double WideWidth = 0.72;

    /// <summary>
    ///     Line height relative to the font size.
    /// </summary>
    private const double LineHeightFactor = 1.25;

    /// <summary>
    ///     Extra width of bold text relative to regular text.
    /// </summary>
    private const double BoldFactor = 1.08;

    /// <summary>
    ///     Characters that are drawn narrow.
    /// </summary>
    private const string NarrowCharacters = " ijlt1!|.,:;'`()[]{}/\\-fI";

    /// <summary>
    ///     Characters that are drawn wide (in addition to every upper-case letter).
    /// </summary>
    private const string WideCharacters = "mwMW@%";

    /// <summary>
    ///     Initializes a new instance of the <see cref="ApproximateTextMeasurer"/> class.
    /// </summary>
    public ApproximateTextMeasurer()
    {
        // Nothing to initialize: the measurer is stateless
    }

    /// <summary>
    ///     Estimates the size of a single line of text from per-character width classes: narrow, normal and wide characters times the font size (bold is 8 percent wider), and a line height of 1.25 times the font size.
    /// </summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="style">The style the text is drawn with; only the size and weight matter.</param>
    /// <returns>The estimated width and height in diagram units.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="style"/> is null.</exception>
    public Size Measure(string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);

        // Sum the width class of every character, then scale by the font size and weight
        var units = 0.0;
        foreach (var c in text)
        {
            units += WidthOf(c);
        }

        var width = units * style.FontSize * (style.Bold ? BoldFactor : 1);
        return new Size(width, style.FontSize * LineHeightFactor);
    }

    /// <summary>
    ///     Gets the estimated width of one character in font-size units.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>The width relative to the font size.</returns>
    private static double WidthOf(char c)
    {
        if (NarrowCharacters.Contains(c, StringComparison.Ordinal))
        {
            return NarrowWidth;
        }

        return WideCharacters.Contains(c, StringComparison.Ordinal) || char.IsUpper(c) ? WideWidth : NormalWidth;
    }
}
