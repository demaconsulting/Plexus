namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Unit tests for the default node width (grown to fit the title) and the dark theme preset.
/// </summary>
public class DefaultNodeWidthTests
{
    /// <summary>
    ///     Computes the WCAG contrast ratio of a foreground color over a background color.
    /// </summary>
    /// <param name="foreground">The text or line color; a translucent color is composited over the background first.</param>
    /// <param name="background">The opaque background.</param>
    /// <returns>The ratio, from 1 to 21.</returns>
    private static double Contrast(Color foreground, Color background)
    {
        var alpha = foreground.A / 255.0;
        var blended = Color.FromRgb(
            (byte)Math.Round((foreground.R * alpha) + (background.R * (1 - alpha))),
            (byte)Math.Round((foreground.G * alpha) + (background.G * (1 - alpha))),
            (byte)Math.Round((foreground.B * alpha) + (background.B * (1 - alpha))));
        var a = Luminance(blended);
        var b = Luminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>
    ///     Computes the relative luminance of an opaque color.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The luminance from 0 to 1.</returns>
    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    /// <summary>
    ///     Validates that a short title keeps the theme's default width and the height rule is unchanged.
    /// </summary>
    [Fact]
    public void NodeGeometry_DefaultNodeSize_ShortTitle_KeepsThemeDefaultWidth()
    {
        // Arrange
        var type = new NodeType("acme.Add") { Title = "ADD" };

        // Act
        var size = NodeGeometry.DefaultNodeSize(type, DiagramTheme.Classic);

        // Assert
        Assert.Equal(new Size(90, 90), size);
    }

    /// <summary>
    ///     Validates that a long title widens the node so it fits on one line, in whole grid steps, and that the title then does not wrap.
    /// </summary>
    [Fact]
    public void NodeGeometry_DefaultNodeSize_LongTitle_GrowsWidthToFitSnappedToTheGrid()
    {
        // Arrange: a title that does not fit 90 units in the header font
        var type = new NodeType("acme.Thermometer") { Title = "THERMOMETER" };
        var theme = DiagramTheme.Classic;

        // Act
        var size = NodeGeometry.DefaultNodeSize(type, theme);

        // Assert: wider than the default, a multiple of the grid, and the title fits the box width the visual allows
        var titleWidth = new ApproximateTextMeasurer().Measure("THERMOMETER", theme.HeaderTextStyle).Width;
        Assert.True(size.Width > theme.DefaultNodeWidth);
        Assert.Equal(0, size.Width % theme.GridSize);
        Assert.True(titleWidth <= size.Width - (2 * theme.TextGap) - 2);
        Assert.True(size.Width - theme.GridSize < (titleWidth * 1.05) + (2 * theme.TextGap) + 2);
        Assert.Equal(theme.DefaultNodeHeight, size.Height);
    }

    /// <summary>
    ///     Validates that a size declared on the type stays authoritative, so a long title wraps inside it instead of widening it.
    /// </summary>
    [Fact]
    public void NodeGeometry_DefaultNodeSize_DeclaredSize_WinsOverTheTitle()
    {
        // Arrange
        var type = new NodeType("acme.Thermometer") { Title = "THERMOMETER", DefaultSize = new Size(90, 60) };

        // Act / Assert
        Assert.Equal(new Size(90, 60), NodeGeometry.DefaultNodeSize(type, DiagramTheme.Classic));
    }

    /// <summary>
    ///     Validates that the theme's default width is the minimum, that a theme without a grid still fits the title, and that a new node gets the computed size.
    /// </summary>
    [Fact]
    public void NodeGeometry_DefaultNodeSize_WideThemeMinimumAndNoGrid_AreHonored()
    {
        // Arrange
        var type = new NodeType("acme.Thermometer") { Title = "THERMOMETER" };
        var wide = DiagramTheme.Classic with { DefaultNodeWidth = 300 };
        var noGrid = DiagramTheme.Classic with { GridSize = 0 };

        // Act
        var wideSize = NodeGeometry.DefaultNodeSize(type, wide);
        var noGridSize = NodeGeometry.DefaultNodeSize(type, noGrid);

        // Assert: the minimum wins when larger; without a grid the width is the unrounded requirement
        Assert.Equal(300, wideSize.Width);
        Assert.True(noGridSize.Width > 90);
        var types = new NodeTypeRegistry { type };
        var node = new Diagram(types).Pages.Add("P").AddNode("acme.Thermometer", 0, 0);
        Assert.Equal(NodeGeometry.DefaultNodeSize(type, DiagramTheme.Classic).Width, node.Bounds.Width);
    }

    /// <summary>
    ///     Validates that a node of a long-titled type is drawn with its title on a single line.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_LongTitleWithDefaultSize_DrawsTheTitleOnOneLine()
    {
        // Arrange
        var types = new NodeTypeRegistry { new NodeType("acme.Thermometer") { Title = "THERMOMETER" } };
        var page = new Diagram(types).Pages.Add("P");
        var node = page.AddNode("acme.Thermometer", 0, 0);

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Assert: the whole title text is one primitive
        var texts = SceneTestModel.For<TextPrimitive>(scene, node).Select(t => t.Text).ToList();
        Assert.Contains("THERMOMETER", texts);
    }

    /// <summary>
    ///     Validates that the dark theme is a distinct preset with the same metrics as the classic theme and a dark canvas.
    /// </summary>
    [Fact]
    public void DiagramTheme_Dark_IsADistinctPresetWithClassicMetrics()
    {
        // Arrange / Act
        var dark = DiagramTheme.Dark;

        // Assert: same instance every time, different colors, identical geometry
        Assert.Same(DiagramTheme.Dark, dark);
        Assert.NotEqual(DiagramTheme.Classic, dark);
        Assert.True(Luminance(dark.CanvasBackground) < 0.05);
        Assert.Equal(DiagramTheme.Classic.GridSize, dark.GridSize);
        Assert.Equal(DiagramTheme.Classic.PortPitch, dark.PortPitch);
        Assert.Equal(DiagramTheme.Classic.HeaderHeight, dark.HeaderHeight);
        Assert.Equal(DiagramTheme.Classic.DefaultNodeWidth, dark.DefaultNodeWidth);
    }

    /// <summary>
    ///     Validates that every text, wire and frame color of the dark theme is readable against what it is drawn on.
    /// </summary>
    [Fact]
    public void DiagramTheme_Dark_TextsWiresAndFramesAreReadableOnTheirBackgrounds()
    {
        // Arrange
        var dark = DiagramTheme.Dark;
        var canvas = dark.CanvasBackground;
        var groupTitleBackground = Color.FromRgb(
            (byte)Math.Round((dark.GroupTitleFill.R * (dark.GroupTitleFill.A / 255.0)) + (canvas.R * (1 - (dark.GroupTitleFill.A / 255.0)))),
            (byte)Math.Round((dark.GroupTitleFill.G * (dark.GroupTitleFill.A / 255.0)) + (canvas.G * (1 - (dark.GroupTitleFill.A / 255.0)))),
            (byte)Math.Round((dark.GroupTitleFill.B * (dark.GroupTitleFill.A / 255.0)) + (canvas.B * (1 - (dark.GroupTitleFill.A / 255.0)))));

        // Assert: texts need 4.5:1, lines and outlines 3:1; the grid dots are visible but quieter than the content
        Assert.Multiple(
            () => Assert.True(Contrast(dark.NodeNameText, canvas) >= 4.5, "instance name"),
            () => Assert.True(Contrast(dark.SummaryText, canvas) >= 4.5, "summary"),
            () => Assert.True(Contrast(dark.PortLabelText, canvas) >= 4.5, "port label"),
            () => Assert.True(Contrast(dark.NodeHeaderText, dark.NodeFill) >= 4.5, "title"),
            () => Assert.True(Contrast(dark.AnnotationText, dark.AnnotationFill) >= 4.5, "annotation"),
            () => Assert.True(Contrast(dark.GroupTitleText, groupTitleBackground) >= 4.5, "group title"),
            () => Assert.True(Contrast(dark.WireColor, canvas) >= 3, "wire"),
            () => Assert.True(Contrast(dark.SelectionColor, canvas) >= 3, "selection"),
            () => Assert.True(Contrast(dark.GroupBorder, canvas) >= 3, "group border"),
            () => Assert.True(Contrast(dark.NodeBorder, canvas) >= 3, "node border"),
            () => Assert.True(Contrast(dark.GridDotColor, canvas) > 1.2, "grid dots"),
            () => Assert.True(Contrast(dark.GridDotColor, canvas) < Contrast(dark.WireColor, canvas), "grid quieter than wires"));
    }

    /// <summary>
    ///     Validates that a diagram can use the dark theme and a scene built with it paints the dark canvas and the dark colors.
    /// </summary>
    [Fact]
    public void DiagramTheme_Dark_AssignedToDiagram_DrivesTheScene()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        diagram.Theme = DiagramTheme.Dark;
        var page = diagram.Pages.Add("P");
        var node = page.AddNode("acme.Producer", 0, 0);

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Assert: the node box is filled with the dark node fill and the instance name uses the dark name color
        var rects = SceneTestModel.For<RectPrimitive>(scene, node);
        Assert.Contains(rects, r => r.Fill == DiagramTheme.Dark.NodeFill);
        var texts = SceneTestModel.For<TextPrimitive>(scene, node);
        Assert.Contains(texts, t => t.Style.Color == DiagramTheme.Dark.NodeNameText);
    }
}
