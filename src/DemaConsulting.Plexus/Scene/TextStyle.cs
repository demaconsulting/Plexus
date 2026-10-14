namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     How a piece of text is drawn: font family, size, weight, color and horizontal alignment.
/// </summary>
/// <remarks>
///     Painters map <see cref="FontFamily"/> to an installed font and fall back to a default
///     sans-serif face when it is missing. <see cref="FontSize"/> is in diagram units (one unit is
///     one pixel at zoom 1). Text is always drawn on a single line, vertically centered in the
///     rectangle of its primitive.
/// </remarks>
/// <param name="FontFamily">The font family name.</param>
/// <param name="FontSize">The font size in diagram units.</param>
/// <param name="Color">The text color.</param>
/// <param name="Bold">Optional; defaults to false. Whether the text is bold.</param>
/// <param name="Alignment">Optional; defaults to <see cref="TextAlignment.Left"/>. The horizontal alignment inside the primitive's rectangle.</param>
public sealed record TextStyle(
    string FontFamily,
    double FontSize,
    Color Color,
    bool Bold = false,
    TextAlignment Alignment = TextAlignment.Left);
