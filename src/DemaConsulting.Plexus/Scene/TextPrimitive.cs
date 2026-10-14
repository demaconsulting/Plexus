namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     A single line of text drawn inside a rectangle.
/// </summary>
/// <remarks>
///     The text is aligned horizontally according to <see cref="Style"/> and centered vertically
///     in <see cref="Rect"/>. Painters do not wrap or clip; the scene builder has already
///     broken long text into one primitive per line.
/// </remarks>
public sealed class TextPrimitive : ScenePrimitive
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TextPrimitive"/> class.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="rect">The layout rectangle.</param>
    /// <param name="style">The text style.</param>
    /// <param name="element">Optional; defaults to null. The owning model element. May be null.</param>
    public TextPrimitive(string text, Rect rect, TextStyle style, DiagramElement? element = null)
        : base(rect, element)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);
        Text = text;
        Rect = rect;
        Style = style;
    }

    /// <summary>
    ///     Gets the text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the layout rectangle.
    /// </summary>
    public Rect Rect { get; }

    /// <summary>
    ///     Gets the text style.
    /// </summary>
    public TextStyle Style { get; }
}
