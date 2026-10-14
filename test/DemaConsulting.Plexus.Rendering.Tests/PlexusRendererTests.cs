using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Rendering.Tests;

/// <summary>
///     Tests for <see cref="PlexusRenderer"/> and <see cref="RenderOptions"/>. They render real pages
///     and inspect the decoded pixels, which also verifies the CanvasNet functionality the renderer relies on
///     (path filling and stroking, text, SVG rasterization and the PNG codec).
/// </summary>
public class PlexusRendererTests
{
    /// <summary>
    ///     The theme background (#A9A9A9) as a pixel.
    /// </summary>
    private static readonly Rgba32 ThemeBackground = RenderTestModel.Pixel(DiagramTheme.Classic.CanvasBackground);

    /// <summary>
    ///     The standard node fill (#6B6B6B) as a pixel.
    /// </summary>
    private static readonly Rgba32 NodeFill = RenderTestModel.Pixel(DiagramTheme.Classic.NodeFill);

    /// <summary>
    ///     Options that render exactly the area (0, 0, 200, 200) without a grid, for pixel sampling.
    /// </summary>
    private static readonly RenderOptions FixedArea = new() { Bounds = new Rect(0, 0, 200, 200), ShowGrid = false };

    /// <summary>
    ///     Validates that the output is a PNG file (signature check).
    /// </summary>
    [Fact]
    public void PlexusRenderer_RenderPng_DefaultOptions_ProducesPngSignature()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Plain", 30, 30, "N");

        // Act
        var png = PlexusRenderer.RenderPng(page);

        // Assert: the eight-byte PNG signature
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png.Take(8).ToArray());
    }

    /// <summary>
    ///     Validates that an empty page renders as a padding-only image (two paddings square).
    /// </summary>
    [Fact]
    public void PlexusRenderer_RenderPng_EmptyPage_ProducesPaddingSizedImage()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();

        // Act
        using var image = RenderTestModel.Decode(PlexusRenderer.RenderPng(page));

        // Assert: default padding 15 on each side of nothing
        Assert.Equal(30, image.Width);
        Assert.Equal(30, image.Height);
    }

    /// <summary>
    ///     Validates that explicit bounds fix the image size at the requested scale.
    /// </summary>
    [Fact]
    public void PlexusRenderer_RenderPng_BoundsAndScale_DetermineImageSize()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Plain", 30, 30, "N");

        // Act
        using var unscaled = RenderTestModel.Decode(PlexusRenderer.RenderPng(page, new RenderOptions { Bounds = new Rect(0, 0, 200, 100) }));
        using var scaled = RenderTestModel.Decode(PlexusRenderer.RenderPng(page, new RenderOptions { Bounds = new Rect(0, 0, 200, 100), Scale = 1.5 }));

        // Assert
        Assert.Equal((200, 100), (unscaled.Width, unscaled.Height));
        Assert.Equal((300, 150), (scaled.Width, scaled.Height));
    }

    /// <summary>
    ///     Validates that, without explicit bounds, the image fits the scene content plus the padding.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_NoBounds_FitsContentPlusPadding()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Plain", 30, 30, "Node");
        var content = SceneBuilder.Build(page, new SceneOptions { TextMeasurer = new CanvasTextMeasurer(), ShowGrid = false }).Bounds;

        // Act
        using var tight = PlexusRenderer.Render(page, new RenderOptions { Padding = 0 });
        using var padded = PlexusRenderer.Render(page, new RenderOptions { Padding = 30 });

        // Assert: padding adds its width on both sides (within one pixel of flooring/ceiling)
        Assert.InRange(tight.Width, Math.Floor(content.Width), Math.Ceiling(content.Width) + 1);
        Assert.InRange(padded.Width - tight.Width, 59, 61);
        Assert.InRange(padded.Height - tight.Height, 59, 61);
    }

    /// <summary>
    ///     Validates that doubling the scale doubles the image and keeps content at the scaled position.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_Scale2_DoublesSizeAndPositions()
    {
        // Arrange: a node box at (30, 30)
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Plain", 30, 30, "N");

        // Act
        using var image = PlexusRenderer.Render(page, new RenderOptions { Bounds = FixedArea.Bounds, ShowGrid = false, Scale = 2 });

        // Assert: twice the size, and the box interior sits at twice the coordinates
        Assert.Equal((400, 400), (image.Width, image.Height));
        Assert.Equal(NodeFill, image[2 * 40, 2 * 100]);
        Assert.Equal(ThemeBackground, image[2 * 10, 2 * 100]);
    }

    /// <summary>
    ///     Validates that the background color option is used, and that the theme background is the default.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_Background_UsesOptionOrTheme()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();

        // Act
        using var custom = PlexusRenderer.Render(page, new RenderOptions { Bounds = FixedArea.Bounds, ShowGrid = false, Background = Color.Parse("#336699") });
        using var themed = PlexusRenderer.Render(page, FixedArea);

        // Assert
        Assert.Equal(new Rgba32(0x33, 0x66, 0x99, 255), custom[5, 5]);
        Assert.Equal(ThemeBackground, themed[5, 5]);
    }

    /// <summary>
    ///     Validates that a transparent background is preserved in the PNG (alpha channel written).
    /// </summary>
    [Fact]
    public void PlexusRenderer_RenderPng_TransparentBackground_KeepsAlpha()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();

        // Act
        using var image = RenderTestModel.Decode(PlexusRenderer.RenderPng(page, new RenderOptions { Bounds = FixedArea.Bounds, ShowGrid = false, Background = Color.Transparent }));

        // Assert
        Assert.Equal(0, image[5, 5].A);
    }

    /// <summary>
    ///     Validates that grid dots sit on multiples of the grid size and can be switched off.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_ShowGrid_DrawsDotsOnGridPoints()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();
        var area = new Rect(0, 0, 60, 60);
        var dot = RenderTestModel.Pixel(DiagramTheme.Classic.GridDotColor);

        // Act
        using var withGrid = PlexusRenderer.Render(page, new RenderOptions { Bounds = area });
        using var withoutGrid = PlexusRenderer.Render(page, new RenderOptions { Bounds = area, ShowGrid = false });

        // Assert: the dot's top-left pixel is fully covered; between dots the background shows
        Assert.Equal(dot, withGrid[15, 15]);
        Assert.Equal(dot, withGrid[30, 45]);
        Assert.Equal(ThemeBackground, withGrid[22, 22]);
        Assert.Equal(ThemeBackground, withoutGrid[15, 15]);
    }

    /// <summary>
    ///     Validates that a node box is filled with the theme's node color.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_Node_PaintsBoxWithNodeFill()
    {
        // Arrange: a 90 x 90 box at (30, 30)
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Plain", 30, 30, "N");

        // Act
        using var image = PlexusRenderer.Render(page, FixedArea);

        // Assert: inside the box, and background just outside it
        Assert.Equal(NodeFill, image[40, 100]);
        Assert.Equal(NodeFill, image[75, 110]);
        Assert.Equal(ThemeBackground, image[20, 100]);
    }

    /// <summary>
    ///     Validates that node titles and names are painted as text (header and above the box).
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_NodeTextTitleAndName_PaintsGlyphPixels()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Titled", 30, 30, "Name");

        // Act
        using var image = PlexusRenderer.Render(page, FixedArea);

        // Assert: the white title changes pixels inside the header band; the green name appears above the box
        Assert.True(RenderTestModel.CountDifferent(image, 35, 33, 80, 24, NodeFill) > 20);
        Assert.True(RenderTestModel.CountDifferent(image, 30, 5, 90, 22, ThemeBackground) > 20);
    }

    /// <summary>
    ///     Validates that an SVG icon is rasterized and composited into the node's icon box.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_NodeIcon_PaintsRasterizedSvg()
    {
        // Arrange: the icon box is 56 x 56 centered in the body of the 90 x 90 node at (30, 30)
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Icon", 30, 30, "N");

        // Act
        using var image = PlexusRenderer.Render(page, FixedArea);

        // Assert: the icon's solid red shows at its center and the box fill around it
        Assert.Equal(new Rgba32(255, 0, 0, 255), image[75, 90]);
        Assert.Equal(NodeFill, image[36, 95]);
    }

    /// <summary>
    ///     Validates that a selected node gets the theme's selection outline.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_SelectedNode_DrawsSelectionOutline()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();
        var node = page.AddNode("t.Plain", 30, 30, "N");

        // Act
        using var selected = PlexusRenderer.Render(page, new RenderOptions { Bounds = FixedArea.Bounds, ShowGrid = false, Selection = [node] });
        using var plain = PlexusRenderer.Render(page, FixedArea);

        // Assert: the outline is two pixels wide, centered two units outside the box edge
        Assert.Equal(RenderTestModel.Pixel(DiagramTheme.Classic.SelectionColor), selected[28, 70]);
        Assert.Equal(ThemeBackground, plain[28, 70]);
    }

    /// <summary>
    ///     Validates that wires are stroked in the wire color between connected ports.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_Wire_StrokesBetweenPorts()
    {
        // Arrange: a source and a sink at the same height, so the wire runs straight at y = 45 (the centers of the boxes)
        var (_, page) = RenderTestModel.CreatePage();
        var source = page.AddNode("t.Source", 0, 0, "A");
        var sink = page.AddNode("t.Sink", 300, 0, "B");
        page.Connect(source.Port("value"), sink.Port("in"));

        // Act
        using var image = PlexusRenderer.Render(page, new RenderOptions { Bounds = new Rect(0, 0, 400, 100), ShowGrid = false });

        // Assert: a two-pixel black line at y 44 and 45, with background above and below
        var black = RenderTestModel.Pixel(DiagramTheme.Classic.WireColor);
        Assert.Equal(black, image[200, 44]);
        Assert.Equal(black, image[200, 45]);
        Assert.Equal(ThemeBackground, image[200, 55]);
    }

    /// <summary>
    ///     Validates that annotations and groups (including a collapsed group) render without error.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_AnnotationAndGroups_PaintsAnnotationFill()
    {
        // Arrange: an annotation, an expanded group and a collapsed group
        var (_, page) = RenderTestModel.CreatePage();
        page.AddAnnotation("note", new Rect(10, 10, 100, 50));
        var a = page.AddNode("t.Plain", 150, 30, "A");
        var b = page.AddNode("t.Plain", 400, 30, "B");
        page.CreateGroup([a], "Open");
        page.CreateGroup([b], "Closed").IsCollapsed = true;

        // Act
        using var image = PlexusRenderer.Render(page);

        // Assert: everything renders, and with only the annotation in view its yellow fill shows near the bottom-right corner
        Assert.True(image.Width > 400);
        using var annotationOnly = PlexusRenderer.Render(page, new RenderOptions { Bounds = new Rect(0, 0, 130, 80), ShowGrid = false });
        Assert.Equal(RenderTestModel.Pixel(DiagramTheme.Classic.AnnotationFill), annotationOnly[100, 55]);
    }

    /// <summary>
    ///     Validates that a theme in the options overrides the diagram's theme.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_ThemeOption_OverridesDiagramTheme()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Plain", 30, 30, "N");
        var theme = new DiagramTheme { CanvasBackground = Color.Parse("#102030"), NodeFill = Color.Parse("#405060") };

        // Act
        using var image = PlexusRenderer.Render(page, new RenderOptions { Bounds = FixedArea.Bounds, ShowGrid = false, Theme = theme });

        // Assert
        Assert.Equal(new Rgba32(0x10, 0x20, 0x30, 255), image[5, 5]);
        Assert.Equal(new Rgba32(0x40, 0x50, 0x60, 255), image[40, 100]);
    }

    /// <summary>
    ///     Validates that SavePng writes a PNG file.
    /// </summary>
    [Fact]
    public void PlexusRenderer_SavePng_WritesPngFile()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Plain", 30, 30, "N");
        var path = Path.Combine(Path.GetTempPath(), $"plexus-{Guid.NewGuid():N}.png");

        try
        {
            // Act
            PlexusRenderer.SavePng(page, path, new RenderOptions { Scale = 2 });

            // Assert
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
            using var image = RenderTestModel.Decode(bytes);
            Assert.True(image.Width > 60);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that unusable arguments and options are rejected with clear exceptions.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_InvalidInput_Throws()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PlexusRenderer.Render(null!));
        Assert.Throws<ArgumentException>(() => PlexusRenderer.Render(page, new RenderOptions { Scale = 0 }));
        Assert.Throws<ArgumentException>(() => PlexusRenderer.Render(page, new RenderOptions { Scale = double.NaN }));
        Assert.Throws<ArgumentException>(() => PlexusRenderer.Render(page, new RenderOptions { Padding = -1 }));
        Assert.Throws<ArgumentException>(() => PlexusRenderer.Render(page, new RenderOptions { Bounds = new Rect(0, 0, 0, 10) }));
        Assert.Throws<ArgumentException>(() => PlexusRenderer.Render(page, new RenderOptions { Bounds = new Rect(0, 0, 100000, 10) }));
        Assert.Throws<ArgumentException>(() => PlexusRenderer.SavePng(page, " "));
    }
}
