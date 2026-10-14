using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Rendering;

/// <summary>
///     Renders a <see cref="DiagramPage"/> to a PNG image without any UI framework.
/// </summary>
/// <remarks>
///     Rendering is two steps that mirror the interactive editor: the page is turned into a
///     painter-agnostic <see cref="DiagramScene"/> (using <see cref="CanvasTextMeasurer"/> so text
///     layout matches the pixels), and the scene's primitives are painted with CanvasNet. The
///     renderer is stateless and thread-safe as long as the page is not modified concurrently.
///     The image covers the scene content plus padding unless explicit bounds are requested.
/// </remarks>
/// <example>
///     Render a page with the diagram's own look, then a 2x copy of a fixed area with a transparent background.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Rendering;
///
///     var diagram = new Diagram(types);                         // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///     var a = page.AddNode("demo.Source", 0, 0, name: "A");
///
///     Directory.CreateDirectory("out");                         // the file's folder must exist
///     PlexusRenderer.SavePng(page, "out/diagram.png");
///
///     var options = new RenderOptions
///     {
///         Scale = 2,                                            // device pixels per diagram unit
///         Bounds = new Rect(0, 0, 400, 240),                    // an explicit area instead of fit-to-content
///         Background = Color.Transparent,
///         ShowGrid = false,
///         Selection = [a],                                      // draw node A as selected
///     };
///     byte[] png = PlexusRenderer.RenderPng(page, options);
///
///     using var surface = PlexusRenderer.Render(page);          // CanvasNet surface; the caller disposes it
///     </code>
/// </example>
public static class PlexusRenderer
{
    /// <summary>
    ///     Renders a page to PNG bytes.
    /// </summary>
    /// <param name="page">The page to render. Must not be null.</param>
    /// <param name="options">The render options; null uses the default <see cref="RenderOptions"/>.</param>
    /// <returns>The PNG file contents; opaque backgrounds are written without an alpha channel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the options are invalid or the image would be larger than 8192 pixels on a side.</exception>
    public static byte[] RenderPng(DiagramPage page, RenderOptions? options = null)
    {
        // Opaque backgrounds need no alpha channel, which keeps files smaller
        using var surface = Render(page, options);
        var opaque = (options?.Background ?? page.Diagram.Theme.CanvasBackground).A == 255;
        using var stream = new MemoryStream();
        PngCodec.Save(surface, stream, opaque ? PngColorType.Rgb : PngColorType.Rgba);
        return stream.ToArray();
    }

    /// <summary>
    ///     Renders a page and writes the PNG to a file, replacing any existing file.
    /// </summary>
    /// <remarks>The folder of <paramref name="path"/> must already exist; it is not created (a missing folder throws <see cref="DirectoryNotFoundException"/>).</remarks>
    /// <param name="page">The page to render. Must not be null.</param>
    /// <param name="path">The destination file path. Must not be null or empty.</param>
    /// <param name="options">The render options; null uses the default <see cref="RenderOptions"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or the options are invalid.</exception>
    public static void SavePng(DiagramPage page, string path, RenderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, RenderPng(page, options));
    }

    /// <summary>
    ///     Renders a page to a CanvasNet surface for callers that want the pixels (to post-process
    ///     or encode in another format).
    /// </summary>
    /// <remarks>
    ///     The caller owns the returned surface and must dispose it. Pixels are straight-alpha
    ///     RGBA; the surface is fully opaque unless a translucent background was requested.
    /// </remarks>
    /// <param name="page">The page to render. Must not be null.</param>
    /// <param name="options">The render options; null uses the default <see cref="RenderOptions"/>.</param>
    /// <returns>A new surface owned by the caller.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the scale is not a finite positive number, the padding is negative or not
    ///     finite, or the image would exceed the maximum surface size (8192 pixels on a side).
    /// </exception>
    public static Surface Render(DiagramPage page, RenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        options ??= new RenderOptions();
        ValidateOptions(options);

        // Build the scene without a grid: the grid must cover the final image area, which is only
        // known once the content bounds are, so the renderer adds it itself below
        var theme = options.Theme ?? page.Diagram.Theme;
        var scene = SceneBuilder.Build(
            page,
            new SceneOptions
            {
                Theme = theme,
                TextMeasurer = new CanvasTextMeasurer(),
                Selection = options.Selection,
                ShowGrid = false,
                Bounds = options.Bounds,
            });

        // Fit the content plus padding unless the caller chose the area explicitly. The area's
        // origin is floored to a whole unit so integer diagram coordinates land on pixel edges
        // Compute in double first: converting an absurd size straight to int would overflow silently
        var area = options.Bounds ?? scene.Bounds.Inflate(options.Padding);
        var origin = new Point(Math.Floor(area.Left), Math.Floor(area.Top));
        var widthPixels = Math.Ceiling((area.Right - origin.X) * options.Scale);
        var heightPixels = Math.Ceiling((area.Bottom - origin.Y) * options.Scale);
        if (widthPixels > Surface.MaxDimension || heightPixels > Surface.MaxDimension)
        {
            throw new ArgumentException(
                $"The image would be {widthPixels}x{heightPixels} pixels; the maximum is {Surface.MaxDimension}. Reduce the scale or the bounds.",
                nameof(options));
        }

        var width = (int)widthPixels;
        var height = (int)heightPixels;
        var surface = new Surface(Math.Max(1, width), Math.Max(1, height));
        try
        {
            // Background first, then the grid (behind everything), then the scene in z-order
            surface.Clear(ScenePainter.ToPixel(options.Background ?? theme.CanvasBackground));
            var painter = new ScenePainter(surface, origin, options.Scale);
            if (options.ShowGrid && theme.GridSize > 0)
            {
                painter.Paint(new GridPrimitive(area, theme.GridSize, theme.GridDotColor));
            }

            painter.Paint(scene.Primitives);
            return surface;
        }
        catch
        {
            // The caller never sees the surface when painting fails, so release it here
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Rejects option values that cannot produce an image.
    /// </summary>
    /// <param name="options">The options to check.</param>
    /// <exception cref="ArgumentException">Thrown when the scale, padding or bounds are unusable.</exception>
    private static void ValidateOptions(RenderOptions options)
    {
        if (!double.IsFinite(options.Scale) || options.Scale <= 0)
        {
            throw new ArgumentException("Scale must be a finite number greater than zero.", nameof(options));
        }

        if (!double.IsFinite(options.Padding) || options.Padding < 0)
        {
            throw new ArgumentException("Padding must be a finite, non-negative number.", nameof(options));
        }

        if (options.Bounds is { } bounds && (bounds.IsEmpty || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)))
        {
            throw new ArgumentException("Bounds must be a non-empty area.", nameof(options));
        }
    }
}
