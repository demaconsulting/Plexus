namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Breaks annotation text into lines that fit a width.
/// </summary>
/// <remarks>
///     Explicit line breaks are kept; each paragraph is wrapped greedily at word boundaries and a
///     word wider than the line is split by character. Works with any <see cref="ITextMeasurer"/>.
/// </remarks>
internal static class TextWrapper
{
    /// <summary>
    ///     Wraps text to a maximum line width.
    /// </summary>
    /// <param name="text">The text, possibly with line breaks.</param>
    /// <param name="maxWidth">The maximum line width.</param>
    /// <param name="style">The text style used for measuring.</param>
    /// <param name="measurer">The text measurer.</param>
    /// <returns>The lines, at least one (possibly empty).</returns>
    public static List<string> Wrap(string text, double maxWidth, TextStyle style, ITextMeasurer measurer)
    {
        var lines = new List<string>();
        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        foreach (var paragraph in paragraphs)
        {
            WrapParagraph(paragraph, maxWidth, style, measurer, lines);
        }

        return lines;
    }

    /// <summary>
    ///     Wraps one paragraph and appends its lines.
    /// </summary>
    /// <param name="paragraph">The paragraph (no line breaks).</param>
    /// <param name="maxWidth">The maximum line width.</param>
    /// <param name="style">The text style used for measuring.</param>
    /// <param name="measurer">The text measurer.</param>
    /// <param name="lines">The list receiving the lines.</param>
    private static void WrapParagraph(string paragraph, double maxWidth, TextStyle style, ITextMeasurer measurer, List<string> lines)
    {
        var current = string.Empty;
        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // Try to extend the current line with the word
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (measurer.Measure(candidate, style).Width <= maxWidth)
            {
                current = candidate;
                continue;
            }

            // Otherwise start a new line, splitting words that are wider than a whole line
            if (current.Length > 0)
            {
                lines.Add(current);
            }

            current = SplitLongWord(word, maxWidth, style, measurer, lines);
        }

        lines.Add(current);
    }

    /// <summary>
    ///     Emits full lines from a word too wide for one line and returns the remainder.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <param name="maxWidth">The maximum line width.</param>
    /// <param name="style">The text style used for measuring.</param>
    /// <param name="measurer">The text measurer.</param>
    /// <param name="lines">The list receiving completed lines.</param>
    /// <returns>The part of the word that did not fill a whole line.</returns>
    private static string SplitLongWord(string word, double maxWidth, TextStyle style, ITextMeasurer measurer, List<string> lines)
    {
        var current = string.Empty;
        foreach (var c in word)
        {
            var candidate = current + c;
            if (current.Length > 0 && measurer.Measure(candidate, style).Width > maxWidth)
            {
                lines.Add(current);
                current = c.ToString();
            }
            else
            {
                current = candidate;
            }
        }

        return current;
    }
}
