namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     A piece of text together with the rectangle it is laid out in.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Bounds">The layout rectangle in diagram coordinates.</param>
public readonly record struct LayoutText(string Text, Rect Bounds);
