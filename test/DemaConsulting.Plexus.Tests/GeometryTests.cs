using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the geometry primitives <see cref="Point"/>, <see cref="Size"/>, <see cref="Rect"/> and <see cref="Color"/>.
/// </summary>
public class GeometryTests
{
    /// <summary>
    ///     Validates that a six-digit hexadecimal color parses as opaque.
    /// </summary>
    [Fact]
    public void Color_Parse_RgbHex_ReturnsOpaqueColor()
    {
        // Act: parse an orange color
        var color = Color.Parse("#FF8C00");

        // Assert: channels are decoded and alpha defaults to opaque
        Assert.Equal(new Color(255, 140, 0, 255), color);
    }

    /// <summary>
    ///     Validates that an eight-digit hexadecimal color carries alpha in the leading byte.
    /// </summary>
    [Fact]
    public void Color_Parse_ArgbHex_ReturnsColorWithAlpha()
    {
        // Act: parse a half-transparent color
        var color = Color.Parse("#80112233");

        // Assert: alpha is the first byte
        Assert.Equal(new Color(0x11, 0x22, 0x33, 0x80), color);
    }

    /// <summary>
    ///     Validates that malformed color text is rejected by Parse and TryParse.
    /// </summary>
    /// <param name="text">The malformed text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("FF8C00")]
    [InlineData("#FF8C0")]
    [InlineData("#GG8C00")]
    [InlineData("#+F8C00")]
    [InlineData("#FF8C00FF0")]
    public void Color_Parse_InvalidText_ThrowsFormatException(string text)
    {
        // Act / Assert: both entry points reject the text
        Assert.Throws<FormatException>(() => Color.Parse(text));
        Assert.False(Color.TryParse(text, out _));
    }

    /// <summary>
    ///     Validates that a null string is rejected by Parse with an argument exception.
    /// </summary>
    [Fact]
    public void Color_Parse_Null_ThrowsArgumentNullException()
    {
        // Act / Assert: null is a programming error, not a format error
        Assert.Throws<ArgumentNullException>(() => Color.Parse(null!));
    }

    /// <summary>
    ///     Validates that ToString output parses back to the same color for opaque and translucent colors.
    /// </summary>
    /// <param name="text">The canonical text.</param>
    [Theory]
    [InlineData("#1E90FF")]
    [InlineData("#80112233")]
    public void Color_ToString_RoundTripsThroughParse(string text)
    {
        // Arrange: parse the canonical text
        var color = Color.Parse(text);

        // Act: format and parse again
        var reparsed = Color.Parse(color.ToString());

        // Assert: nothing is lost and the canonical text is reproduced
        Assert.Equal(color, reparsed);
        Assert.Equal(text, color.ToString());
    }

    /// <summary>
    ///     Validates that the named color statics have the documented values.
    /// </summary>
    [Fact]
    public void Color_NamedStatics_HaveExpectedValues()
    {
        // Assert: spot-check the named statics and factories
        Assert.Equal(new Color(0, 0, 0, 0), Color.Transparent);
        Assert.Equal(Color.FromRgb(0, 0, 0), Color.Black);
        Assert.Equal(Color.FromArgb(255, 255, 255, 255), Color.White);
        Assert.Equal(new Color(255, 165, 0), Color.Orange);
    }

    /// <summary>
    ///     Validates that Rect edge and center properties derive from position and size.
    /// </summary>
    [Fact]
    public void Rect_Edges_DeriveFromPositionAndSize()
    {
        // Arrange: a rectangle at (10, 20) of size 30 x 40
        var rect = new Rect(new Point(10, 20), new Size(30, 40));

        // Assert: every derived value matches
        Assert.Multiple(
            () => Assert.Equal(40, rect.Right),
            () => Assert.Equal(60, rect.Bottom),
            () => Assert.Equal(new Point(25, 40), rect.Center),
            () => Assert.Equal(new Point(10, 20), rect.Position),
            () => Assert.Equal(new Size(30, 40), rect.Size),
            () => Assert.False(rect.IsEmpty));
    }

    /// <summary>
    ///     Validates that FromPoints normalizes corners passed in any order.
    /// </summary>
    [Fact]
    public void Rect_FromPoints_ReversedCorners_ReturnsNormalizedRect()
    {
        // Act: build a rectangle from bottom-right to top-left
        var rect = Rect.FromPoints(new Point(50, 60), new Point(10, 20));

        // Assert: the rectangle has positive size anchored at the top-left
        Assert.Equal(new Rect(10, 20, 40, 40), rect);
    }

    /// <summary>
    ///     Validates point and rectangle containment, including edges.
    /// </summary>
    [Fact]
    public void Rect_Contains_PointsAndRects_HonorsEdges()
    {
        // Arrange: a 10 x 10 rectangle at the origin
        var rect = new Rect(0, 0, 10, 10);

        // Assert: edges are inclusive and outside points are rejected
        Assert.True(rect.Contains(new Point(10, 10)));
        Assert.False(rect.Contains(new Point(10.1, 5)));
        Assert.True(rect.Contains(new Rect(2, 2, 8, 8)));
        Assert.False(rect.Contains(new Rect(2, 2, 9, 8)));
    }

    /// <summary>
    ///     Validates intersection, union, inflate and offset helpers.
    /// </summary>
    [Fact]
    public void Rect_IntersectsUnionInflateOffset_ProduceExpectedRects()
    {
        // Arrange: two overlapping rectangles
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(5, 5, 10, 10);

        // Assert: relations and derived rectangles are correct
        Assert.True(a.Intersects(b));
        Assert.False(a.Intersects(new Rect(11, 0, 5, 5)));
        Assert.Equal(new Rect(0, 0, 15, 15), a.Union(b));
        Assert.Equal(new Rect(-2, -2, 14, 14), a.Inflate(2));
        Assert.Equal(new Rect(3, 4, 10, 10), a.Offset(3, 4));
        Assert.Equal(new Rect(7, 8, 10, 10), a.WithPosition(new Point(7, 8)));
    }

    /// <summary>
    ///     Validates that TryParse reports failure for null text without throwing.
    /// </summary>
    [Fact]
    public void Color_TryParse_Null_ReturnsFalse()
    {
        // Act: parse null text
        var parsed = Color.TryParse(null, out var color);

        // Assert: failure is reported with the default color
        Assert.False(parsed);
        Assert.Equal(default, color);
    }

    /// <summary>
    ///     Validates that parsing is case-insensitive and that lowercase text formats back in canonical uppercase.
    /// </summary>
    [Fact]
    public void Color_Parse_LowercaseHex_ParsesAndFormatsUppercase()
    {
        // Act: parse lowercase digits
        var color = Color.Parse("#ff8c00");

        // Assert: the value matches and the canonical text is uppercase
        Assert.Equal(new Color(255, 140, 0), color);
        Assert.Equal("#FF8C00", color.ToString());
    }

    /// <summary>
    ///     Validates that points, sizes, rectangles and colors compare by value and hash consistently.
    /// </summary>
    [Fact]
    public void Geometry_ValueTypes_CompareByValueAndHashConsistently()
    {
        // Arrange: two independently created values of each type
        var points = (new Point(1, 2), new Point(1, 2));
        var sizes = (new Size(3, 4), new Size(3, 4));
        var rects = (new Rect(1, 2, 3, 4), new Rect(new Point(1, 2), new Size(3, 4)));
        var colors = (Color.Parse("#112233"), new Color(0x11, 0x22, 0x33));

        // Assert: equal values are equal and hash alike; a single differing component breaks equality
        Assert.Multiple(
            () => Assert.Equal(points.Item1, points.Item2),
            () => Assert.Equal(points.Item1.GetHashCode(), points.Item2.GetHashCode()),
            () => Assert.Equal(sizes.Item1, sizes.Item2),
            () => Assert.Equal(sizes.Item1.GetHashCode(), sizes.Item2.GetHashCode()),
            () => Assert.Equal(rects.Item1, rects.Item2),
            () => Assert.Equal(rects.Item1.GetHashCode(), rects.Item2.GetHashCode()),
            () => Assert.Equal(colors.Item1, colors.Item2),
            () => Assert.Equal(colors.Item1.GetHashCode(), colors.Item2.GetHashCode()),
            () => Assert.NotEqual(new Point(1, 2), new Point(1, 3)),
            () => Assert.NotEqual(new Rect(1, 2, 3, 4), new Rect(1, 2, 3, 5)),
            () => Assert.NotEqual(Color.Parse("#112233"), Color.Parse("#80112233")));
    }

    /// <summary>
    ///     Validates that a rectangle with a zero or negative extent is empty.
    /// </summary>
    /// <param name="width">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, 10)]
    public void Rect_IsEmpty_NonPositiveExtent_ReturnsTrue(double width, double height)
    {
        // Act: build a degenerate rectangle
        var rect = new Rect(0, 0, width, height);

        // Assert: it has no area
        Assert.True(rect.IsEmpty);
    }

    /// <summary>
    ///     Validates the arithmetic operators on <see cref="Point"/>.
    /// </summary>
    [Fact]
    public void Point_Operators_AddSubtractAndOffset()
    {
        // Arrange: two points
        var a = new Point(5, 7);
        var b = new Point(2, 3);

        // Assert: operators and helpers behave component-wise
        Assert.Equal(new Point(3, 4), a - b);
        Assert.Equal(new Point(7, 10), a + b);
        Assert.Equal(new Point(8, 12), a + new Size(3, 5));
        Assert.Equal(new Point(6, 5), a.Offset(1, -2));
        Assert.Equal(new Point(0, 0), Point.Zero);
        Assert.Equal(new Size(0, 0), Size.Empty);
    }
}
