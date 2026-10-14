using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of the Avalonia-backed text measurer.
/// </summary>
public class AvaloniaTextMeasurerTests
{
    /// <summary>
    ///     The style used by the tests.
    /// </summary>
    private static readonly TextStyle Style = new("Segoe UI", 11, PlexusColor.Black);

    /// <summary>
    ///     Validates that longer text measures wider and that every non-empty text has a height.
    /// </summary>
    [Fact]
    public Task AvaloniaTextMeasurer_Measure_LongerText_IsWider() => UiTest.Run(() =>
    {
        // Arrange: a measurer
        var measurer = new AvaloniaTextMeasurer();

        // Act: measure a short and a long string
        var shortSize = measurer.Measure("Hi", Style);
        var longSize = measurer.Measure("A considerably longer piece of text", Style);

        // Assert: width grows with the text and height is positive
        Assert.Multiple(
            () => Assert.True(longSize.Width > shortSize.Width * 3),
            () => Assert.True(shortSize.Height > 5),
            () => Assert.Equal(shortSize.Height, longSize.Height, 3));
    });

    /// <summary>
    ///     Validates that bigger fonts measure bigger and that colors do not matter.
    /// </summary>
    [Fact]
    public Task AvaloniaTextMeasurer_Measure_FontSizeAndColor_AffectSizeCorrectly() => UiTest.Run(() =>
    {
        // Arrange: a measurer and three styles differing in size and color
        var measurer = new AvaloniaTextMeasurer();
        var big = Style with { FontSize = 22 };
        var red = Style with { Color = PlexusColor.Red };

        // Act: measure the same text with each
        var normal = measurer.Measure("Sample", Style);
        var larger = measurer.Measure("Sample", big);
        var recolored = measurer.Measure("Sample", red);

        // Assert: doubling the font roughly doubles the size; color changes nothing
        Assert.Multiple(
            () => Assert.True(larger.Width > normal.Width * 1.8),
            () => Assert.Equal(normal, recolored));
    });

    /// <summary>
    ///     Validates that empty text has no width but still reports a line height.
    /// </summary>
    [Fact]
    public Task AvaloniaTextMeasurer_Measure_EmptyText_HasLineHeightOnly() => UiTest.Run(() =>
    {
        // Arrange / Act: measure the empty string
        var size = new AvaloniaTextMeasurer().Measure(string.Empty, Style);

        // Assert: zero width, positive height
        Assert.Multiple(
            () => Assert.Equal(0, size.Width),
            () => Assert.True(size.Height > 0));
    });

    /// <summary>
    ///     Validates that null arguments are rejected.
    /// </summary>
    [Fact]
    public Task AvaloniaTextMeasurer_Measure_NullArguments_Throw() => UiTest.Run(() =>
    {
        // Arrange: a measurer
        var measurer = new AvaloniaTextMeasurer();

        // Act / Assert: each null argument throws
        Assert.Throws<ArgumentNullException>(() => measurer.Measure(null!, Style));
        Assert.Throws<ArgumentNullException>(() => measurer.Measure("x", null!));
    });
}
