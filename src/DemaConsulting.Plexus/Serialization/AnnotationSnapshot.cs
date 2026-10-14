namespace DemaConsulting.Plexus;

/// <summary>
///     The persisted state of one <see cref="Annotation"/>.
/// </summary>
public sealed record AnnotationSnapshot
{
    /// <summary>
    ///     Gets the element id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     Gets the note text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    ///     Gets the bounding box.
    /// </summary>
    public required RectSnapshot Bounds { get; init; }

    /// <summary>
    ///     Gets the fill color as <c>"#AARRGGBB"</c>, or null for the theme's annotation fill.
    /// </summary>
    public string? Fill { get; init; }
}
