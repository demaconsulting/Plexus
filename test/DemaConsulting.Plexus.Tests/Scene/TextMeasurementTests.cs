namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Unit tests for <see cref="ApproximateTextMeasurer"/> and the <see cref="ITextMeasurer"/> contract it fulfils.
/// </summary>
public class TextMeasurementTests
{
    /// <summary>
    ///     The style used by most tests.
    /// </summary>
    private static readonly TextStyle Regular = new("Segoe UI", 10, Color.Black);

    /// <summary>
    ///     Validates that longer, bolder and larger text measures wider and that empty text has no width.
    /// </summary>
    [Fact]
    public void ApproximateTextMeasurer_Measure_ScalesWithLengthWeightAndSize()
    {
        // Arrange
        var measurer = new ApproximateTextMeasurer();
        var bold = Regular with { Bold = true };
        var large = Regular with { FontSize = 20 };

        // Act
        var empty = measurer.Measure(string.Empty, Regular);
        var shortText = measurer.Measure("Mixer", Regular);
        var longText = measurer.Measure("Mixer Controller", Regular);

        // Assert
        Assert.Equal(0, empty.Width);
        Assert.True(longText.Width > shortText.Width);
        Assert.True(measurer.Measure("Mixer", bold).Width > shortText.Width);
        Assert.Equal(shortText.Width * 2, measurer.Measure("Mixer", large).Width, 6);
        Assert.Equal(shortText, measurer.Measure("Mixer", Regular));
        Assert.True(shortText.Height > 10);
    }

    /// <summary>
    ///     Validates that narrow glyphs measure narrower than ordinary ones and wide glyphs wider.
    /// </summary>
    [Fact]
    public void ApproximateTextMeasurer_Measure_NarrowNormalWide_AreOrderedByWidth()
    {
        // Arrange
        var measurer = new ApproximateTextMeasurer();

        // Act
        var narrow = measurer.Measure("iiii", Regular).Width;
        var normal = measurer.Measure("aaaa", Regular).Width;
        var wide = measurer.Measure("mmmm", Regular).Width;
        var capitals = measurer.Measure("AAAA", Regular).Width;

        // Assert
        Assert.True(narrow < normal);
        Assert.True(normal < wide);
        Assert.Equal(wide, capitals, 6);
    }

    /// <summary>
    ///     Validates that the line height is independent of the text and proportional to the font size.
    /// </summary>
    [Fact]
    public void ApproximateTextMeasurer_Measure_LineHeight_DependsOnlyOnFontSize()
    {
        // Arrange
        var measurer = new ApproximateTextMeasurer();

        // Act
        var emptyHeight = measurer.Measure(string.Empty, Regular).Height;
        var textHeight = measurer.Measure("Some longer text", Regular).Height;
        var doubleHeight = measurer.Measure("Some longer text", Regular with { FontSize = 20 }).Height;

        // Assert
        Assert.Equal(emptyHeight, textHeight);
        Assert.Equal(emptyHeight * 2, doubleHeight, 6);
    }

    /// <summary>
    ///     Validates that measuring the same text and style repeatedly, with separate instances, gives identical results.
    /// </summary>
    [Fact]
    public void ApproximateTextMeasurer_Measure_SameInput_IsDeterministic()
    {
        // Arrange
        var first = new ApproximateTextMeasurer().Measure("Gain stage 1", Regular);

        // Act
        var second = new ApproximateTextMeasurer().Measure("Gain stage 1", Regular);
        var third = new ApproximateTextMeasurer().Measure("Gain stage 1", Regular);

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(first, third);
    }

    /// <summary>
    ///     Validates that a null text or style is rejected.
    /// </summary>
    [Fact]
    public void ApproximateTextMeasurer_Measure_NullArguments_ThrowArgumentNullException()
    {
        // Arrange
        var measurer = new ApproximateTextMeasurer();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => measurer.Measure(null!, Regular));
        Assert.Throws<ArgumentNullException>(() => measurer.Measure("text", null!));
    }

    /// <summary>
    ///     Validates that the measurer works without any painter or font, as an <see cref="ITextMeasurer"/>.
    /// </summary>
    [Fact]
    public void ApproximateTextMeasurer_AsTextMeasurer_MeasuresWithoutPainterOrFonts()
    {
        // Arrange: a font family that cannot be installed anywhere
        ITextMeasurer measurer = new ApproximateTextMeasurer();
        var style = new TextStyle("No Such Font Family", 12, Color.Black);

        // Act
        var size = measurer.Measure("Headless", style);

        // Assert
        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    }
}
