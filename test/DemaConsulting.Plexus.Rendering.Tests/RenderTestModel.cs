using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Rendering.Tests;

/// <summary>
///     Shared fixtures for the rendering tests: a tiny node type registry and helpers to decode PNG output.
/// </summary>
internal static class RenderTestModel
{
    /// <summary>
    ///     An SVG that fills its whole viewBox with opaque red, so an icon box shows as a solid red square.
    /// </summary>
    public const string RedSquareSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"#FF0000\"/></svg>";

    /// <summary>
    ///     Creates node types: <c>t.Plain</c> (no title, no ports), <c>t.Titled</c>, <c>t.Icon</c> (red square icon),
    ///     <c>t.Source</c> (one output) and <c>t.Sink</c> (one input).
    /// </summary>
    /// <returns>The registry.</returns>
    public static NodeTypeRegistry CreateTypes()
    {
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType("f32", Color.Parse("#FF8C00")));
        types.Add(new NodeType("t.Plain") { Title = string.Empty });
        types.Add(new NodeType("t.Titled") { Title = "MMMM" });
        types.Add(new NodeType("t.Icon") { Title = string.Empty, Icon = IconSource.FromSvg(RedSquareSvg) });
        types.Add(new NodeType("t.Source") { Title = string.Empty, Outputs = { new PortDefinition("value", "f32") } });
        types.Add(new NodeType("t.Sink") { Title = string.Empty, Inputs = { new PortDefinition("in", "f32") } });
        return types;
    }

    /// <summary>
    ///     Creates a diagram with the test types and one empty page.
    /// </summary>
    /// <returns>The diagram and its page.</returns>
    public static (Diagram Diagram, DiagramPage Page) CreatePage()
    {
        var diagram = new Diagram(CreateTypes());
        return (diagram, diagram.Pages.Add("Main"));
    }

    /// <summary>
    ///     Decodes PNG bytes into a surface.
    /// </summary>
    /// <param name="png">The PNG file contents.</param>
    /// <returns>The decoded surface.</returns>
    public static Surface Decode(byte[] png)
    {
        using var stream = new MemoryStream(png);
        return PngCodec.Load(stream);
    }

    /// <summary>
    ///     Converts a Plexus color to a CanvasNet pixel.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The pixel value.</returns>
    public static Rgba32 Pixel(Color color) => new(color.R, color.G, color.B, color.A);

    /// <summary>
    ///     Counts the pixels in a region that differ from a reference color.
    /// </summary>
    /// <param name="surface">The surface to scan.</param>
    /// <param name="x">The left pixel column of the region.</param>
    /// <param name="y">The top pixel row of the region.</param>
    /// <param name="width">The region width in pixels.</param>
    /// <param name="height">The region height in pixels.</param>
    /// <param name="reference">The color to compare against.</param>
    /// <returns>The number of differing pixels.</returns>
    public static int CountDifferent(Surface surface, int x, int y, int width, int height, Rgba32 reference)
    {
        var count = 0;
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                if (!surface[column, row].Equals(reference))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
