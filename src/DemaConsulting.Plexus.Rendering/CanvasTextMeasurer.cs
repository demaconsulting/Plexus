using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Rendering;

/// <summary>
///     Measures text with the same CanvasNet fonts the <see cref="PlexusRenderer"/> paints with, so
///     layout computed during scene building matches the rendered pixels.
/// </summary>
/// <remarks>
///     Fonts are discovered on the host system by the style's font family and fall back to the
///     bundled Liberation Sans, so measuring works on any machine (results then differ slightly
///     between machines with and without the family installed). The height of a line is the
///     font's ascent plus descent at the requested size, capped at 1.25 times the size. Stateless and thread-safe.
///     Pass it as <c>SceneOptions.TextMeasurer</c> when a scene (and its hit test) must agree with the pixels of <see cref="PlexusRenderer"/>;
///     the scene builder's default is the font-free approximate measurer, whose name and label areas can differ by a few pixels.
/// </remarks>
/// <example>
///     Hit test with the same text geometry the PNG renderer uses.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Rendering;
///     using DemaConsulting.Plexus.Scene;
///
///     var diagram = new Diagram(new NodeTypeRegistry());
///     var page = diagram.Pages.Add("Main");
///     var scene = SceneBuilder.Build(page, new SceneOptions { TextMeasurer = new CanvasTextMeasurer() });
///     var hit = scene.HitTest(new Point(10, 10));
///     Console.WriteLine(hit.Kind);                        // Canvas: the page is empty
///     </code>
/// </example>
public sealed class CanvasTextMeasurer : ITextMeasurer
{
    /// <summary>
    ///     The largest line height, as a multiple of the font size.
    /// </summary>
    private const double MaximumLineHeightFactor = 1.25;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CanvasTextMeasurer"/> class.
    /// </summary>
    public CanvasTextMeasurer()
    {
        // Nothing to initialize: the measurer is stateless
    }

    /// <summary>
    ///     Measures a single line of text.
    /// </summary>
    /// <remarks>
    ///     An empty string is valid and measures zero width (the height follows the style). A font size that is zero,
    ///     negative, NaN or infinite measures as <see cref="Size.Empty"/>.
    /// </remarks>
    /// <param name="text">The text; may be empty but not null.</param>
    /// <param name="style">The style the text is drawn with. Must not be null.</param>
    /// <returns>The width and height of the text in diagram units.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="style"/> is null.</exception>
    public Size Measure(string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);

        // Nothing to measure for degenerate sizes; the font engine rejects non-positive sizes
        if (style.FontSize <= 0 || !double.IsFinite(style.FontSize))
        {
            return Size.Empty;
        }

        // The line box is the font's ascent plus descent, capped so stacked lines (summaries,
        // annotations) keep a compact rhythm even for fonts with generous line gaps
        var font = FontProvider.Get(style.FontFamily, style.Bold);
        var metrics = TextRenderer.MeasureText(text, font, (float)style.FontSize);
        return new Size(metrics.Width, Math.Min(metrics.Ascent + metrics.Descent, style.FontSize * MaximumLineHeightFactor));
    }
}
