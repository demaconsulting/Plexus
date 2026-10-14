namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Builds <see cref="DiagramScene"/>s from pages.
/// </summary>
/// <remarks>
///     The scene is the shared geometry behind every painter and hit test. Building is a pure,
///     read-only pass over the page: it never changes the model (display routes for collapsed
///     groups are transient). Nodes that no longer fit the culling bounds are skipped, and each
///     node is drawn by its <see cref="NodeType.Visual"/> or the <see cref="StandardNodeVisual"/>.
///     Stateless and thread-safe as long as the page is not modified concurrently.
///     <para>
///         Text measurement: unless <see cref="SceneOptions.TextMeasurer"/> is set, text is measured with the font-free
///         <see cref="ApproximateTextMeasurer"/>. That is fine for building scenes, tests and layout logic, but the name,
///         label and summary rectangles it produces (and so the hit areas of the node name) can differ by a few pixels from what a real font draws.
///         To make hit testing agree with the pixels, pass the measurer of the painter: <c>CanvasTextMeasurer</c> (package
///         <c>DemaConsulting.Plexus.Rendering</c>, matches <c>PlexusRenderer</c> output) or <c>AvaloniaTextMeasurer</c>
///         (package <c>DemaConsulting.Plexus.Avalonia</c>, which the editor itself uses). Port anchors and node boxes never depend on the measurer.
///     </para>
/// </remarks>
/// <example>
///     Build a scene of a page and inspect what a painter would draw.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Scene;
///
///     var diagram = new Diagram(types);                         // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///     var a = page.AddNode("demo.Source", 0, 0, name: "A");
///
///     var scene = SceneBuilder.Build(page, new SceneOptions
///     {
///         ShowGrid = false,
///         Selection = [a],                                      // draw node A as selected
///         TextMeasurer = new ApproximateTextMeasurer(),         // the default; no fonts needed
///     });
///
///     foreach (var primitive in scene.Primitives)               // in painting order
///     {
///         Console.WriteLine(primitive.GetType().Name);          // RectPrimitive, TextPrimitive, PathPrimitive, ...
///     }
///
///     if (scene.TryGetNodeLayout(a, out var layout))
///     {
///         Console.WriteLine(layout!.Box);                       // where node A was laid out
///     }
///     </code>
/// </example>
public static class SceneBuilder
{
    /// <summary>
    ///     Builds the scene of a page.
    /// </summary>
    /// <param name="page">The page to draw.</param>
    /// <param name="options">The scene options; null uses the default <see cref="SceneOptions"/> (the diagram's theme, approximate text measurer, grid on, nothing selected).</param>
    /// <returns>The scene.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is null.</exception>
    public static DiagramScene Build(DiagramPage page, SceneOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new SceneComposer(page, options ?? new SceneOptions()).Compose();
    }
}
