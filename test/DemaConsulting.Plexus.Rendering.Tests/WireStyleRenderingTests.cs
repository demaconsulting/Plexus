using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Rendering.Tests;

/// <summary>
///     Rendering tests for dynamic wire styling: a styled wire must show up in the PNG with its color,
///     thickness and dash pattern, because the dashes are cut in the core scene and drawn as plain paths.
/// </summary>
public class WireStyleRenderingTests
{
    /// <summary>
    ///     Options that render the area (0, 0, 500, 120) without a grid at scale 1, so pixels equal diagram units.
    /// </summary>
    private static readonly RenderOptions Area = new() { Bounds = new Rect(0, 0, 500, 120), ShowGrid = false };

    /// <summary>
    ///     Proves that a styled wire is drawn in the styler's color and thickness while a default wire stays black.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_StyledWire_UsesStylerColorAndThickness()
    {
        // Arrange: a wire from the f32 source colored by its port type and drawn 6 units thick
        var (diagram, page, wire) = CreateWiredPage();
        var middle = MidPoint(wire);
        using var plain = PlexusRenderer.Render(page, Area);
        diagram.WireStyler = WireStylers.Combine(
            WireStylers.ByPortType(),
            _ => new WireStyle { Thickness = 6 });

        // Act
        using var styled = PlexusRenderer.Render(page, Area);

        // Assert: the default wire is black, the styled one is the f32 type color and wider
        Assert.Equal(RenderTestModel.Pixel(Color.Black), plain[(int)middle.X, (int)middle.Y]);
        var orange = RenderTestModel.Pixel(Color.Parse("#FF8C00"));
        Assert.Equal(orange, styled[(int)middle.X, (int)middle.Y]);
        Assert.Equal(orange, styled[(int)middle.X, (int)middle.Y - 2]);
        Assert.Equal(orange, styled[(int)middle.X, (int)middle.Y + 2]);
        Assert.NotEqual(orange, styled[(int)middle.X, (int)middle.Y + 5]);
    }

    /// <summary>
    ///     Proves that a dashed wire alternates between colored and background pixels along its length.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_DashedWire_AlternatesInkAndGaps()
    {
        // Arrange
        var (diagram, page, wire) = CreateWiredPage();
        var background = RenderTestModel.Pixel(DiagramTheme.Classic.CanvasBackground);
        var red = RenderTestModel.Pixel(Color.Parse("#FF0000"));
        diagram.WireStyler = _ => new WireStyle { Color = Color.Parse("#FF0000"), Dash = [10, 10], Arrowhead = false };

        // Act
        using var image = PlexusRenderer.Render(page, Area);

        // Assert: along the wire between the nodes there are both red runs and gaps of background
        var y = (int)wire.Source.Anchor.Y;
        var from = (int)wire.Source.Anchor.X + 2;
        var to = (int)wire.Target.Anchor.X - 2;
        var ink = Enumerable.Range(from, to - from).Count(x => image[x, y].Equals(red));
        var gaps = Enumerable.Range(from, to - from).Count(x => image[x, y].Equals(background));
        Assert.InRange(ink, (to - from) * 4 / 10, (to - from) * 6 / 10);
        Assert.InRange(gaps, (to - from) * 4 / 10, (to - from) * 6 / 10);
    }

    /// <summary>
    ///     Proves that the picture can be re-rendered after a refresh with a different result, as an editor would on
    ///     <see cref="Diagram.RefreshAppearance"/>.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_StylerDependsOnHostState_ChangesAfterRefresh()
    {
        // Arrange: the wire color follows a host variable
        var (diagram, page, wire) = CreateWiredPage();
        var hot = false;
        diagram.WireStyler = _ => new WireStyle { Color = hot ? Color.Parse("#FF0000") : Color.Parse("#00AA00") };
        var middle = MidPoint(wire);
        using var cold = PlexusRenderer.Render(page, Area);

        // Act
        hot = true;
        diagram.RefreshAppearance();
        using var warm = PlexusRenderer.Render(page, Area);

        // Assert
        Assert.Equal(RenderTestModel.Pixel(Color.Parse("#00AA00")), cold[(int)middle.X, (int)middle.Y]);
        Assert.Equal(RenderTestModel.Pixel(Color.Parse("#FF0000")), warm[(int)middle.X, (int)middle.Y]);
    }

    /// <summary>
    ///     Creates a diagram with a source wired to a sink on the same row.
    /// </summary>
    /// <returns>The diagram, its page and the wire.</returns>
    private static (Diagram Diagram, DiagramPage Page, Wire Wire) CreateWiredPage()
    {
        var (diagram, page) = RenderTestModel.CreatePage();
        var source = page.AddNode("t.Source", 30, 30, "A");
        var sink = page.AddNode("t.Sink", 300, 30, "B");
        return (diagram, page, page.Connect(source.Port("value"), sink.Port("in")));
    }

    /// <summary>
    ///     Gets the midpoint between the anchors of a straight wire.
    /// </summary>
    /// <param name="wire">The wire.</param>
    /// <returns>The point halfway along the wire.</returns>
    private static Point MidPoint(Wire wire) =>
        new((wire.Source.Anchor.X + wire.Target.Anchor.X) / 2, wire.Source.Anchor.Y);
}
