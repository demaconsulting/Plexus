namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     An SVG icon scaled to fit a rectangle.
/// </summary>
/// <remarks>
///     <para>
///         Painters render <see cref="Icon"/> into <see cref="Rect"/>, preserving the aspect ratio and
///         caching by <see cref="IconSource.Key"/>.
///     </para>
///     <para>
///         When <see cref="Tint"/> is set the icon is painted as a silhouette: every pixel the icon
///         would paint takes the tint's color, with the icon's own alpha multiplied by the tint's alpha
///         as coverage. <see cref="Opacity"/> then scales the whole result. Both painters follow
///         exactly these rules, so a tinted icon looks the same in the PNG and in the editor.
///     </para>
///     <para>
///         Inherited from <see cref="ScenePrimitive"/>: <see cref="ScenePrimitive.Bounds"/> and <see cref="ScenePrimitive.Element"/> (the owning model element, or null).
///     </para>
/// </remarks>
public sealed class IconPrimitive : ScenePrimitive
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="IconPrimitive"/> class.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <param name="rect">The rectangle to draw it in.</param>
    /// <param name="element">The owning model element.</param>
    /// <param name="tint">The silhouette color; null draws the icon as authored.</param>
    /// <param name="opacity">The opacity of the whole icon, from 0 to 1.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="icon"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="opacity"/> is outside 0 to 1 or not finite.</exception>
    public IconPrimitive(IconSource icon, Rect rect, DiagramElement? element = null, Color? tint = null, double opacity = 1)
        : base(rect, element)
    {
        ArgumentNullException.ThrowIfNull(icon);
        if (!double.IsFinite(opacity) || opacity < 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), opacity, "The opacity must be between 0 and 1.");
        }

        Icon = icon;
        Rect = rect;
        Tint = tint;
        Opacity = opacity;
    }

    /// <summary>
    ///     Gets the icon.
    /// </summary>
    public IconSource Icon { get; }

    /// <summary>
    ///     Gets the rectangle the icon is drawn in.
    /// </summary>
    public Rect Rect { get; }

    /// <summary>
    ///     Gets the silhouette color, or null when the icon is drawn as authored.
    /// </summary>
    public Color? Tint { get; }

    /// <summary>
    ///     Gets the opacity of the whole icon, from 0 (invisible) to 1 (opaque).
    /// </summary>
    public double Opacity { get; }
}
