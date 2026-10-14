using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Rendering;

/// <summary>
///     Settings for <see cref="PlexusRenderer"/>. Every value has a friendly default, so
///     <c>new RenderOptions()</c> (or passing null) renders the whole page at 1:1.
/// </summary>
/// <remarks>
///     Options are immutable after construction (init-only; set them with an object initializer) so one instance can be shared between
///     renders and threads.
/// </remarks>
/// <example>
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Rendering;
///     using DemaConsulting.Plexus.Scene;
///
///     var diagram = new Diagram(new NodeTypeRegistry());
///     var page = diagram.Pages.Add("Main");
///
///     var options = new RenderOptions { Scale = 2, Padding = 30, ShowGrid = false };
///     var themed = new RenderOptions { Theme = DiagramTheme.Classic with { CanvasBackground = Color.White } };
///     PlexusRenderer.SavePng(page, "diagram.png", options);
///     PlexusRenderer.SavePng(page, "themed.png", themed);
///     </code>
/// </example>
public sealed class RenderOptions
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RenderOptions"/> class with the default options: 1:1 scale, a grid, and the diagram's own theme and background.
    /// </summary>
    public RenderOptions()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Gets or initializes the number of output pixels per diagram unit (2 doubles the resolution). Must be
    ///     finite and greater than zero. Defaults to 1.
    /// </summary>
    public double Scale { get; init; } = 1;

    /// <summary>
    ///     Gets or initializes the empty margin, in diagram units, added around the scene content when
    ///     <see cref="Bounds"/> is null. Ignored when <see cref="Bounds"/> is given. Defaults to 15.
    /// </summary>
    public double Padding { get; init; } = 15;

    /// <summary>
    ///     Gets or initializes the background color; null (the default) uses the theme's canvas background. An opaque color writes a PNG without an alpha channel; use <c>Color.Transparent</c> for a transparent image.
    /// </summary>
    public Color? Background { get; init; }

    /// <summary>
    ///     Gets or initializes a value indicating whether the dot grid is drawn. Defaults to true.
    /// </summary>
    public bool ShowGrid { get; init; } = true;

    /// <summary>
    ///     Gets or initializes the diagram-space area to render, used as is (no padding added); null (the default) fits the
    ///     scene content plus <see cref="Padding"/>.
    /// </summary>
    public Rect? Bounds { get; init; }

    /// <summary>
    ///     Gets or initializes the theme to draw with; null (the default) uses the diagram's own theme.
    /// </summary>
    public DiagramTheme? Theme { get; init; }

    /// <summary>
    ///     Gets or initializes the elements drawn highlighted as selected. Defaults to none.
    /// </summary>
    public IEnumerable<DiagramElement> Selection { get; init; } = [];
}
