namespace DemaConsulting.Plexus.Geometry;

/// <summary>
///     Small immutable value types for positions, sizes, rectangles and colors, independent of any UI framework.
/// </summary>
/// <example>
///     Everything in a diagram is positioned in diagram coordinates (x grows to the right, y grows downward).
///     <code>
///     using DemaConsulting.Plexus.Geometry;
///
///     var position = new Point(30, 45);
///     var box = new Rect(position, new Size(90, 60));      // top-left corner plus size
///     bool inside = box.Contains(new Point(50, 60));       // true
///     var padded = box.Inflate(15);                        // grown by 15 on every side
///     var color = Color.Parse("#336699");                  // #RRGGBB or #AARRGGBB
///     </code>
///     <para>
///         <see cref="Point"/>, <see cref="Size"/>, <see cref="Rect"/> and <see cref="Color"/> are readonly record
///         structs, so they have value equality and can be copied freely. Plexus defines its own types so the core
///         library stays UI-free. The short names collide with Avalonia's own <c>Point</c>, <c>Size</c>, <c>Rect</c> and <c>Color</c>
///         (<c>Avalonia.Media.Color</c>), so a file that imports both namespaces must disambiguate with an alias, for example
///         <c>using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;</c>. The <c>DemaConsulting.Plexus.Avalonia</c> package provides
///         <c>ToAvalonia()</c> and <c>ToPlexus()</c> extension methods (<c>PlexusAvaloniaExtensions</c>) that convert between the two. There is nothing to extend here.
///     </para>
/// </example>
internal static class NamespaceDoc
{
}
