using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Rendering.Tests;

/// <summary>
///     Tests for <see cref="ScenePainter"/>: every primitive kind painted onto a plain surface.
/// </summary>
public class ScenePainterTests
{
    /// <summary>
    ///     The white background used by every test surface.
    /// </summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     Creates a white surface and a painter drawing 1:1 with the origin at (0, 0).
    /// </summary>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="scale">The pixels per diagram unit.</param>
    /// <returns>The surface and painter.</returns>
    private static (Surface Surface, ScenePainter Painter) Create(int width, int height, double scale = 1)
    {
        var surface = new Surface(width, height);
        surface.Clear(White);
        return (surface, new ScenePainter(surface, Point.Zero, scale));
    }

    /// <summary>
    ///     Validates that a filled rectangle covers its area and nothing outside it.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_FilledRect_CoversAreaOnly()
    {
        // Arrange
        var (surface, painter) = Create(60, 40);
        using var _ = surface;

        // Act
        painter.Paint(new RectPrimitive(new Rect(10, 10, 20, 10), Color.Parse("#336699"), null));

        // Assert
        Assert.Equal(new Rgba32(0x33, 0x66, 0x99, 255), surface[20, 15]);
        Assert.Equal(White, surface[5, 15]);
        Assert.Equal(White, surface[35, 15]);
    }

    /// <summary>
    ///     Validates that a one-pixel stroke on whole coordinates is crisp (one full-coverage pixel row).
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_StrokedRect_OnePixelBorderIsCrisp()
    {
        // Arrange
        var (surface, painter) = Create(60, 40);
        using var _ = surface;

        // Act
        painter.Paint(new RectPrimitive(new Rect(10, 10, 30, 20), null, Color.Black, 1));

        // Assert: the left edge occupies exactly pixel column 10; neighbors are untouched
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[10, 20]);
        Assert.Equal(White, surface[9, 20]);
        Assert.Equal(White, surface[11, 20]);
        Assert.Equal(White, surface[20, 20]);
    }

    /// <summary>
    ///     Validates that a rounded rectangle leaves its corner pixel unpainted but fills the interior.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_RoundedRect_CornerIsCutAway()
    {
        // Arrange
        var (surface, painter) = Create(60, 60);
        using var _ = surface;

        // Act
        painter.Paint(new RectPrimitive(new Rect(10, 10, 40, 40), Color.Black, null, 0, 12));

        // Assert
        Assert.Equal(White, surface[10, 10]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[30, 30]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[30, 11]);
    }

    /// <summary>
    ///     Validates that a closed path with straight lines is filled.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_FilledPath_FillsPolygon()
    {
        // Arrange: a triangle
        var (surface, painter) = Create(60, 60);
        using var _ = surface;
        var triangle = new PathPrimitive(
            [
                PathCommand.MoveTo(new Point(10, 10)),
                PathCommand.LineTo(new Point(50, 10)),
                PathCommand.LineTo(new Point(10, 50)),
                PathCommand.Close(),
            ],
            Color.Red,
            null);

        // Act
        painter.Paint(triangle);

        // Assert: inside the triangle versus the opposite corner of its bounding box
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[20, 20]);
        Assert.Equal(White, surface[45, 45]);
    }

    /// <summary>
    ///     Validates that an open path is filled as if closed, and that a stroke needs no fill.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_OpenPathWithFill_IsImplicitlyClosed()
    {
        // Arrange: three points without a Close command
        var (surface, painter) = Create(60, 60);
        using var _ = surface;
        var open = new PathPrimitive(
            [PathCommand.MoveTo(new Point(10, 10)), PathCommand.LineTo(new Point(50, 10)), PathCommand.LineTo(new Point(10, 50))],
            Color.Red,
            null);

        // Act
        painter.Paint(open);

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[20, 20]);
    }

    /// <summary>
    ///     Validates that cubic curves are stroked along the curve rather than along the chord.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_StrokedCubicPath_FollowsTheCurve()
    {
        // Arrange: a curve that bulges up to y = 25 between two points at y = 60
        var (surface, painter) = Create(100, 80);
        using var _ = surface;
        var curve = new PathPrimitive(
            [
                PathCommand.MoveTo(new Point(10, 60)),
                PathCommand.CubicTo(new Point(10, 0), new Point(90, 0), new Point(90, 60)),
            ],
            null,
            Color.Black,
            3);

        // Act
        painter.Paint(curve);

        // Assert: the curve crosses the middle column near y = 15 (the curve's apex) and not along the chord
        var apexPainted = Enumerable.Range(10, 15).Any(y => surface[50, y].R < 100);
        Assert.True(apexPainted);
        Assert.Equal(White, surface[50, 60]);
    }

    /// <summary>
    ///     Validates that strokes are anti-aliased: a diagonal line produces partially covered pixels.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_DiagonalStroke_IsAntiAliased()
    {
        // Arrange
        var (surface, painter) = Create(60, 60);
        using var _ = surface;

        // Act
        painter.Paint(new PathPrimitive([PathCommand.MoveTo(new Point(5, 5)), PathCommand.LineTo(new Point(55, 30))], null, Color.Black, 1));

        // Assert: some pixels are neither pure white nor pure black
        var grays = 0;
        for (var x = 5; x < 55; x++)
        {
            for (var y = 5; y < 31; y++)
            {
                var value = surface[x, y].R;
                if (value is > 20 and < 235)
                {
                    grays++;
                }
            }
        }

        Assert.True(grays > 20);
    }

    /// <summary>
    ///     Validates that text paints glyph pixels inside its rectangle only.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_Text_PaintsGlyphsInsideRect()
    {
        // Arrange
        var (surface, painter) = Create(140, 50);
        using var _ = surface;
        var style = new TextStyle("Segoe UI", 14, Color.Black, false, TextAlignment.Left);

        // Act
        painter.Paint(new TextPrimitive("Hello", new Rect(10, 10, 100, 24), style));

        // Assert: ink inside the rectangle, none below it
        Assert.True(RenderTestModel.CountDifferent(surface, 10, 10, 60, 24, White) > 30);
        Assert.Equal(0, RenderTestModel.CountDifferent(surface, 0, 40, 140, 10, White));
    }

    /// <summary>
    ///     Validates that left, center and right alignment place the text at the matching part of its rectangle.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TextAlignment_AnchorsToRectEdges()
    {
        // Arrange: three rows of the same short word in a 200-wide rectangle
        var (surface, painter) = Create(220, 90);
        using var _ = surface;
        var rect = new Rect(10, 0, 200, 24);

        // Act
        painter.Paint(new TextPrimitive("Hi", rect, new TextStyle("Segoe UI", 14, Color.Black, false, TextAlignment.Left)));
        painter.Paint(new TextPrimitive("Hi", rect.Offset(0, 30), new TextStyle("Segoe UI", 14, Color.Black, false, TextAlignment.Center)));
        painter.Paint(new TextPrimitive("Hi", rect.Offset(0, 60), new TextStyle("Segoe UI", 14, Color.Black, false, TextAlignment.Right)));

        // Assert: left text is in the left part, centered text in the middle, right text in the right part
        Assert.True(RenderTestModel.CountDifferent(surface, 10, 0, 40, 24, White) > 0);
        Assert.Equal(0, RenderTestModel.CountDifferent(surface, 100, 0, 110, 24, White));
        Assert.True(RenderTestModel.CountDifferent(surface, 95, 30, 30, 24, White) > 0);
        Assert.Equal(0, RenderTestModel.CountDifferent(surface, 10, 30, 60, 24, White));
        Assert.True(RenderTestModel.CountDifferent(surface, 170, 60, 40, 24, White) > 0);
        Assert.Equal(0, RenderTestModel.CountDifferent(surface, 10, 60, 150, 24, White));
    }

    /// <summary>
    ///     Validates that bold text draws more ink than regular text of the same size.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_BoldText_DrawsMoreInk()
    {
        // Arrange
        var (surface, painter) = Create(140, 70);
        using var _ = surface;
        var rect = new Rect(10, 0, 120, 30);

        // Act
        painter.Paint(new TextPrimitive("WWWW", rect, new TextStyle("Segoe UI", 18, Color.Black, false, TextAlignment.Left)));
        painter.Paint(new TextPrimitive("WWWW", rect.Offset(0, 35), new TextStyle("Segoe UI", 18, Color.Black, true, TextAlignment.Left)));

        // Assert: compare the darkness sums of the two rows
        var regular = Darkness(surface, 0, 30);
        var bold = Darkness(surface, 35, 30);
        Assert.True(bold > regular);
    }

    /// <summary>
    ///     Validates that an icon is rasterized to its rectangle and composited with its transparency intact.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_Icon_CompositesRasterizedSvg()
    {
        // Arrange: an icon with a red left half and a fully transparent right half
        var (surface, painter) = Create(60, 40);
        using var _ = surface;
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 20 10\"><rect width=\"10\" height=\"10\" fill=\"#FF0000\"/></svg>";

        // Act
        painter.Paint(new IconPrimitive(IconSource.FromSvg(svg), new Rect(10, 10, 40, 20)));

        // Assert: opaque red where the SVG paints, untouched background where it is transparent
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[20, 20]);
        Assert.Equal(White, surface[40, 20]);
        Assert.Equal(White, surface[5, 20]);
    }

    /// <summary>
    ///     Validates that an icon that cannot be rasterized is skipped instead of failing the whole render.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_MalformedIcon_IsSkipped()
    {
        // Arrange
        var (surface, painter) = Create(40, 40);
        using var _ = surface;

        // Act
        painter.Paint(new IconPrimitive(IconSource.FromSvg("this is not svg"), new Rect(5, 5, 20, 20)));

        // Assert
        Assert.Equal(0, RenderTestModel.CountDifferent(surface, 0, 0, 40, 40, White));
    }

    /// <summary>
    ///     Validates that grid dots appear on multiples of the spacing only.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_Grid_DrawsDotsAtSpacing()
    {
        // Arrange
        var (surface, painter) = Create(70, 70);
        using var _ = surface;

        // Act
        painter.Paint(new GridPrimitive(new Rect(0, 0, 70, 70), 20, Color.Black, 2));

        // Assert: dots at multiples of 20, nothing in between
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[20, 40]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[60, 60]);
        Assert.Equal(White, surface[30, 30]);
        Assert.Equal(White, surface[19, 40]);
    }

    /// <summary>
    ///     Validates that the painter's scale multiplies positions, sizes and stroke widths.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_Scale_MultipliesGeometry()
    {
        // Arrange: a 10 x 10 square at (5, 5) painted at 3x
        var (surface, painter) = Create(60, 60, 3);
        using var _ = surface;

        // Act
        painter.Paint(new RectPrimitive(new Rect(5, 5, 10, 10), Color.Black, null));

        // Assert: covers pixels 15..44
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[16, 16]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[43, 43]);
        Assert.Equal(White, surface[10, 10]);
        Assert.Equal(White, surface[47, 47]);
    }

    /// <summary>
    ///     Validates that a primitive with a translucent color blends with what is beneath it.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TranslucentFill_BlendsWithBackdrop()
    {
        // Arrange
        var (surface, painter) = Create(30, 30);
        using var _ = surface;

        // Act: 50% black over white
        painter.Paint(new RectPrimitive(new Rect(5, 5, 20, 20), Color.FromArgb(128, 0, 0, 0), null));

        // Assert: mid gray, still opaque
        var pixel = surface[15, 15];
        Assert.InRange(pixel.R, 120, 135);
        Assert.Equal(255, pixel.A);
    }

    /// <summary>
    ///     Validates the argument checks of the painter.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_NullArguments_Throw()
    {
        // Arrange
        var (surface, painter) = Create(10, 10);
        using var _ = surface;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new ScenePainter(null!, Point.Zero, 1));
        Assert.Throws<ArgumentNullException>(() => painter.Paint((ScenePrimitive)null!));
        Assert.Throws<ArgumentNullException>(() => painter.Paint((IEnumerable<ScenePrimitive>)null!));
    }

    /// <summary>
    ///     Validates that a sequence of primitives is painted back to front, so later primitives cover earlier ones.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_Sequence_PaintsBackToFront()
    {
        // Arrange: two overlapping rectangles, red first and blue second
        var (surface, painter) = Create(40, 40);
        using var _ = surface;
        ScenePrimitive[] primitives =
        [
            new RectPrimitive(new Rect(5, 5, 20, 20), Color.Red, null),
            new RectPrimitive(new Rect(15, 15, 20, 20), Color.Parse("#0000FF"), null),
        ];

        // Act
        painter.Paint(primitives);

        // Assert: the overlap shows the later primitive, the rest of each rectangle keeps its own color
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[20, 20]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[8, 8]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[30, 30]);
    }

    /// <summary>
    ///     Validates that the painter's origin is the diagram point that maps to the surface's top-left pixel.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_Origin_OffsetsGeometry()
    {
        // Arrange: a painter whose top-left pixel is the diagram point (100, 50)
        using var surface = new Surface(40, 40);
        surface.Clear(White);
        var painter = new ScenePainter(surface, new Point(100, 50), 1);

        // Act: a rectangle at diagram (110, 60)
        painter.Paint(new RectPrimitive(new Rect(110, 60, 10, 10), Color.Black, null));

        // Assert: it lands at pixel (10, 10)
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[15, 15]);
        Assert.Equal(White, surface[5, 5]);
        Assert.Equal(White, surface[25, 25]);
    }

    /// <summary>
    ///     Validates that primitives reaching beyond the surface are clipped to it without failing.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_PrimitivesBeyondSurface_AreClipped()
    {
        // Arrange: a small surface and primitives that start before it and end after it
        var (surface, painter) = Create(30, 30);
        using var _ = surface;
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\" fill=\"#FF0000\"/></svg>";

        // Act
        painter.Paint(new RectPrimitive(new Rect(-20, -20, 40, 40), Color.Black, null));
        painter.Paint(new IconPrimitive(IconSource.FromSvg(svg), new Rect(20, 20, 30, 30)));
        painter.Paint(new GridPrimitive(new Rect(-100, -100, 300, 300), 10, Color.Parse("#00FF00")));

        // Assert: the visible parts are painted and nothing outside the surface was touched or thrown
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[5, 5]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[25, 25]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[20, 0]);
    }

    /// <summary>
    ///     Sums how dark the pixels of a horizontal band are.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="top">The first row of the band.</param>
    /// <param name="height">The band height.</param>
    /// <returns>The sum of (255 - red) over the band.</returns>
    private static long Darkness(Surface surface, int top, int height)
    {
        long sum = 0;
        for (var y = top; y < top + height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                sum += 255 - surface[x, y].R;
            }
        }

        return sum;
    }
}
