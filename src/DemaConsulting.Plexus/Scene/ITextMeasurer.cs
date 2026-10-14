namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Measures how large a piece of text is when drawn with a given style.
/// </summary>
/// <remarks>
///     The scene builder needs text sizes to place labels, wrap annotation text and size boxes.
///     Painters may supply an exact measurer backed by their font engine; the headless default is
///     <see cref="ApproximateTextMeasurer"/>. Port anchors never depend on measured text, so
///     measurement differences only shift labels by a pixel or two. Implementations must be
///     deterministic for a given text and style.
/// </remarks>
public interface ITextMeasurer
{
    /// <summary>
    ///     Measures a single line of text.
    /// </summary>
    /// <param name="text">The text; may be empty.</param>
    /// <param name="style">The style the text is drawn with.</param>
    /// <returns>The width and height of the text in diagram units.</returns>
    Size Measure(string text, TextStyle style);
}
