using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of <see cref="PlexusIcons.GetImage"/>.
/// </summary>
public class PlexusIconsTests
{
    /// <summary>
    ///     A small valid SVG document.
    /// </summary>
    private const string ValidSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\"><rect width=\"16\" height=\"16\" fill=\"#336699\"/></svg>";

    /// <summary>
    ///     Validates that a valid SVG icon yields an image of its size.
    /// </summary>
    [Fact]
    public Task PlexusIcons_GetImage_ValidSvg_ReturnsImage() => UiTest.Run(() =>
    {
        // Arrange
        var icon = IconSource.FromSvg(ValidSvg);

        // Act
        var image = PlexusIcons.GetImage(icon);

        // Assert
        Assert.NotNull(image);
        Assert.Equal(16, image.Size.Width, 1);
        Assert.Equal(16, image.Size.Height, 1);
    });

    /// <summary>
    ///     Validates that the same icon (or an equal one) is served from one cache entry.
    /// </summary>
    [Fact]
    public Task PlexusIcons_GetImage_SameIcon_ReturnsCachedInstance() => UiTest.Run(() =>
    {
        // Arrange: two sources with the same SVG share a key
        var first = IconSource.FromSvg(ValidSvg);
        var second = IconSource.FromSvg(ValidSvg);

        // Act
        var a = PlexusIcons.GetImage(first);
        var b = PlexusIcons.GetImage(first);
        var c = PlexusIcons.GetImage(second);

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(a),
            () => Assert.Same(a, b),
            () => Assert.Same(a, c));
    });

    /// <summary>
    ///     Validates that a malformed SVG never throws and yields null (also on the second request).
    /// </summary>
    [Fact]
    public Task PlexusIcons_GetImage_BrokenSvg_ReturnsNullWithoutThrowing() => UiTest.Run(() =>
    {
        // Arrange
        var icon = IconSource.FromSvg("this is <not> svg");

        // Act
        var first = PlexusIcons.GetImage(icon);
        var second = PlexusIcons.GetImage(icon);

        // Assert
        Assert.Multiple(
            () => Assert.Null(first),
            () => Assert.Null(second));
    });

    /// <summary>
    ///     Validates that a null icon is rejected.
    /// </summary>
    [Fact]
    public Task PlexusIcons_GetImage_Null_Throws() => UiTest.Run(() =>
        Assert.Throws<ArgumentNullException>(() => PlexusIcons.GetImage(null!)));
}
