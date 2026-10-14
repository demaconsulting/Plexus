using AvaloniaColor = Avalonia.Media.Color;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;
using AvaloniaSize = Avalonia.Size;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;
using PlexusSize = DemaConsulting.Plexus.Geometry.Size;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Extension methods that convert between the Plexus geometry primitives and their Avalonia counterparts.
/// </summary>
/// <remarks>
///     <para>
///         Plexus defines its own <c>Point</c>, <c>Size</c>, <c>Rect</c> and <c>Color</c> so that the core library has no
///         UI dependency. They share their short names with the Avalonia types, so a file that imports both
///         <c>Avalonia</c> and <c>DemaConsulting.Plexus.Geometry</c> must disambiguate, for example with
///         <c>using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;</c>. These methods are the converters to use at
///         that boundary: <c>ToAvalonia()</c> turns a Plexus value into the Avalonia one and <c>ToPlexus()</c> goes the
///         other way. Every conversion is lossless (all components are copied unchanged) and allocation-free.
///     </para>
///     <para>
///         Typical uses are painting your own overlay with Plexus coordinates (<c>rect.ToAvalonia()</c>), converting a
///         pointer position into diagram input (<c>e.GetPosition(editor).ToPlexus()</c>) and turning a
///         <c>Color</c>-valued node property into a brush color (<c>plexusColor.ToAvalonia()</c>).
///     </para>
/// </remarks>
/// <example>
///     Convert at the boundary only; everything else can stay in Plexus types.
///     <code>
///     using Avalonia.Media;
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia;
///     using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
///
///     static class ConversionSample
///     {
///         // Brush for a node whose "Tint" property is a Plexus Color
///         public static IBrush TintBrush(Node node) =>
///             new SolidColorBrush(node.Properties.Get&lt;PlexusColor&gt;("Tint").ToAvalonia());
///
///         // Avalonia bounds of a node, e.g. to place a popup next to it
///         public static global::Avalonia.Rect BoundsOf(Node node) => node.GetBounds().ToAvalonia();
///
///         // Plexus size of an Avalonia control
///         public static DemaConsulting.Plexus.Geometry.Size SizeOf(global::Avalonia.Controls.Control control) =>
///             control.Bounds.Size.ToPlexus();
///     }
///     </code>
/// </example>
public static class PlexusAvaloniaExtensions
{
    /// <summary>
    ///     Converts a Plexus point to an Avalonia point.
    /// </summary>
    /// <param name="point">The Plexus point.</param>
    /// <returns>The Avalonia point with the same X and Y.</returns>
    public static AvaloniaPoint ToAvalonia(this PlexusPoint point) => new(point.X, point.Y);

    /// <summary>
    ///     Converts a Plexus size to an Avalonia size.
    /// </summary>
    /// <param name="size">The Plexus size.</param>
    /// <returns>The Avalonia size with the same width and height.</returns>
    public static AvaloniaSize ToAvalonia(this PlexusSize size) => new(size.Width, size.Height);

    /// <summary>
    ///     Converts a Plexus rectangle to an Avalonia rectangle.
    /// </summary>
    /// <param name="rect">The Plexus rectangle.</param>
    /// <returns>The Avalonia rectangle with the same position and size.</returns>
    public static AvaloniaRect ToAvalonia(this PlexusRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    ///     Converts a Plexus color to an Avalonia color.
    /// </summary>
    /// <param name="color">The Plexus color.</param>
    /// <returns>The Avalonia color with the same alpha, red, green and blue channels.</returns>
    public static AvaloniaColor ToAvalonia(this PlexusColor color) => AvaloniaColor.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary>
    ///     Converts an Avalonia point to a Plexus point.
    /// </summary>
    /// <param name="point">The Avalonia point.</param>
    /// <returns>The Plexus point with the same X and Y.</returns>
    public static PlexusPoint ToPlexus(this AvaloniaPoint point) => new(point.X, point.Y);

    /// <summary>
    ///     Converts an Avalonia size to a Plexus size.
    /// </summary>
    /// <param name="size">The Avalonia size.</param>
    /// <returns>The Plexus size with the same width and height.</returns>
    public static PlexusSize ToPlexus(this AvaloniaSize size) => new(size.Width, size.Height);

    /// <summary>
    ///     Converts an Avalonia rectangle to a Plexus rectangle.
    /// </summary>
    /// <param name="rect">The Avalonia rectangle.</param>
    /// <returns>The Plexus rectangle with the same position and size.</returns>
    public static PlexusRect ToPlexus(this AvaloniaRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    ///     Converts an Avalonia color to a Plexus color.
    /// </summary>
    /// <param name="color">The Avalonia color.</param>
    /// <returns>The Plexus color with the same alpha, red, green and blue channels.</returns>
    public static PlexusColor ToPlexus(this AvaloniaColor color) => new(color.R, color.G, color.B, color.A);
}
