using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Rendering.Tests;

/// <summary>
///     Tests for <see cref="CanvasTextMeasurer"/> and the shared font lookup.
/// </summary>
public class CanvasTextMeasurerTests
{
    /// <summary>
    ///     The style used by most tests.
    /// </summary>
    private static readonly TextStyle Regular = new("Segoe UI", 12, DemaConsulting.Plexus.Geometry.Color.Black);

    /// <summary>
    ///     Validates that longer text is wider and has a sensible, size-proportional height.
    /// </summary>
    [Fact]
    public void CanvasTextMeasurer_Measure_LongerText_IsWider()
    {
        // Arrange
        var measurer = new CanvasTextMeasurer();

        // Act
        var shortText = measurer.Measure("ab", Regular);
        var longText = measurer.Measure("abcdefghij", Regular);

        // Assert
        Assert.True(shortText.Width > 0);
        Assert.True(longText.Width > shortText.Width * 3);
        Assert.InRange(shortText.Height, 12, 12 * 1.25 + 0.001);
        Assert.Equal(shortText.Height, longText.Height);
    }

    /// <summary>
    ///     Validates that width scales with the font size.
    /// </summary>
    [Fact]
    public void CanvasTextMeasurer_Measure_DoubleFontSize_DoublesWidth()
    {
        // Arrange
        var measurer = new CanvasTextMeasurer();

        // Act
        var small = measurer.Measure("Plexus", Regular);
        var large = measurer.Measure("Plexus", Regular with { FontSize = 24 });

        // Assert
        Assert.Equal(small.Width * 2, large.Width, 3);
    }

    /// <summary>
    ///     Validates that bold text is not narrower than regular text.
    /// </summary>
    [Fact]
    public void CanvasTextMeasurer_Measure_Bold_IsAtLeastAsWide()
    {
        // Arrange
        var measurer = new CanvasTextMeasurer();

        // Act
        var regular = measurer.Measure("Hello World", Regular);
        var bold = measurer.Measure("Hello World", Regular with { Bold = true });

        // Assert
        Assert.True(bold.Width >= regular.Width);
    }

    /// <summary>
    ///     Validates that every text of one style has the same line height, whatever its length or glyphs.
    /// </summary>
    [Fact]
    public void CanvasTextMeasurer_Measure_DifferentTexts_ShareLineHeight()
    {
        // Arrange
        var measurer = new CanvasTextMeasurer();

        // Act
        var heights = new[] { "a", "Hello", "WWWWWWWW", "line with spaces" }.Select(text => measurer.Measure(text, Regular).Height).ToList();

        // Assert
        Assert.All(heights, height => Assert.Equal(heights[0], height));
    }

    /// <summary>
    ///     Validates that an empty string (as opposed to null) measures zero width and the line height of the style.
    /// </summary>
    [Fact]
    public void CanvasTextMeasurer_Measure_EmptyString_HasZeroWidth()
    {
        // Arrange
        var measurer = new CanvasTextMeasurer();

        // Act
        var empty = measurer.Measure(string.Empty, Regular);
        var text = measurer.Measure("abc", Regular);

        // Assert
        Assert.Equal(0, empty.Width);
        Assert.Equal(text.Height, empty.Height);
    }

    /// <summary>
    ///     Validates that empty text has no width and a zero font size measures as empty.
    /// </summary>
    [Fact]
    public void CanvasTextMeasurer_Measure_EmptyTextOrZeroSize_HasNoWidth()
    {
        // Arrange
        var measurer = new CanvasTextMeasurer();

        // Act
        var empty = measurer.Measure(string.Empty, Regular);
        var zero = measurer.Measure("abc", Regular with { FontSize = 0 });

        // Assert
        Assert.Equal(0, empty.Width);
        Assert.Equal(DemaConsulting.Plexus.Geometry.Size.Empty, zero);
    }

    /// <summary>
    ///     Validates that negative, NaN and infinite font sizes measure as an empty size instead of failing.
    /// </summary>
    [Fact]
    public void CanvasTextMeasurer_Measure_NegativeOrNonFiniteFontSize_IsEmpty()
    {
        // Arrange
        var measurer = new CanvasTextMeasurer();
        double[] sizes = [-1, double.NaN, double.PositiveInfinity, double.NegativeInfinity];

        // Act / Assert
        Assert.All(sizes, size => Assert.Equal(DemaConsulting.Plexus.Geometry.Size.Empty, measurer.Measure("abc", Regular with { FontSize = size })));
    }

    /// <summary>
    ///     Validates that an unknown font family falls back to a usable font instead of failing.
    /// </summary>
    [Fact]
    public void CanvasTextMeasurer_Measure_UnknownFamily_FallsBackToBundledFont()
    {
        // Arrange
        var measurer = new CanvasTextMeasurer();

        // Act
        var size = measurer.Measure("fallback", Regular with { FontFamily = "No Such Font Family 123" });

        // Assert
        Assert.True(size.Width > 20);
    }

    /// <summary>
    ///     Validates that measuring is deterministic and agrees with the font lookup shared with the painter.
    /// </summary>
    [Fact]
    public void CanvasTextMeasurer_Measure_SameInput_IsDeterministic()
    {
        // Arrange
        var measurer = new CanvasTextMeasurer();

        // Act / Assert
        Assert.Equal(measurer.Measure("Scope", Regular), measurer.Measure("Scope", Regular));
        Assert.Same(FontProvider.Get("Segoe UI", false), FontProvider.Get("segoe ui", false));
    }

    /// <summary>
    ///     Validates the argument checks.
    /// </summary>
    [Fact]
    public void CanvasTextMeasurer_Measure_NullArguments_Throw()
    {
        // Arrange
        var measurer = new CanvasTextMeasurer();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => measurer.Measure(null!, Regular));
        Assert.Throws<ArgumentNullException>(() => measurer.Measure("x", null!));
    }
}
