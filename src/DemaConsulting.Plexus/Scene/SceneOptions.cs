namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Options that control how <see cref="SceneBuilder"/> builds a scene.
/// </summary>
/// <remarks>
///     Every option has a friendly default, so <c>SceneBuilder.Build(page)</c> works without
///     Every option has a friendly default, so <c>SceneBuilder.Build(page)</c> works without
///     configuration. Options are read once during the build; they are not retained. All options are
///     init-only: set them with an object initializer, <c>new SceneOptions { ShowGrid = false }</c>.
/// </remarks>
/// <example>
///     Build a scene for a headless hit test that matches the pixels of the PNG renderer.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Rendering;
///     using DemaConsulting.Plexus.Scene;
///
///     var diagram = new Diagram(new NodeTypeRegistry());
///     var page = diagram.Pages.Add("Main");
///
///     var options = new SceneOptions
///     {
///         TextMeasurer = new CanvasTextMeasurer(),     // the renderer's measurer: name and label areas match the drawn text
///         ShowGrid = false,
///         Bounds = new Rect(0, 0, 800, 600),           // cull everything outside this area
///     };
///     var scene = SceneBuilder.Build(page, options);
///     var hit = scene.HitTest(new Point(100, 50));
///     </code>
/// </example>
public sealed class SceneOptions
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SceneOptions"/> class with the default options.
    /// </summary>
    public SceneOptions()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Gets or initializes the theme to use instead of the diagram's own theme; null (the default) uses <see cref="Diagram.Theme"/>. Set with an object initializer.
    /// </summary>
    public DiagramTheme? Theme { get; init; }

    /// <summary>
    ///     Gets or initializes the text measurer; null (the default) uses an <see cref="ApproximateTextMeasurer"/>.
    /// </summary>
    /// <remarks>
    ///     The approximate measurer needs no fonts but may place name, label and summary rectangles a few pixels differently from a
    ///     real font. Pass <c>CanvasTextMeasurer</c> (Rendering package) or <c>AvaloniaTextMeasurer</c> (Avalonia package) when the scene's
    ///     hit areas must match what that painter draws.
    /// </remarks>
    public ITextMeasurer? TextMeasurer { get; init; }

    /// <summary>
    ///     Gets or initializes the elements to draw highlighted as selected; empty by default.
    /// </summary>
    /// <remarks>
    ///     The sequence is read once, when <see cref="SceneBuilder.Build"/> runs, and copied into a
    ///     set; later changes to the source collection do not affect a scene that was already built.
    ///     Pass a snapshot of the current selection (for example <c>editor.Selection.ToArray()</c>)
    ///     and rebuild the scene when the selection changes. Elements on other pages are ignored.
    ///     Selecting a node highlights its box, a wire its stroke, and an annotation additionally
    ///     shows (and makes hittable) its resize handle.
    /// </remarks>
    public IEnumerable<DiagramElement> Selection { get; init; } = [];

    /// <summary>
    ///     Gets or initializes the element to draw highlighted as hovered; null (the default) means none.
    /// </summary>
    public DiagramElement? Hover { get; init; }

    /// <summary>
    ///     Gets or initializes a value indicating whether the background grid is included (default true).
    /// </summary>
    public bool ShowGrid { get; init; } = true;

    /// <summary>
    ///     Gets or initializes the visible area used for culling; elements entirely outside it are left out. Null (the default) builds everything.
    /// </summary>
    /// <remarks>
    ///     When set, the grid covers exactly this area; otherwise it covers the content plus a margin.
    /// </remarks>
    public Rect? Bounds { get; init; }
}
