using DemaConsulting.Plexus.Avalonia;

namespace DemaConsulting.Plexus.Puppet.Tests;

/// <summary>
///     Tests of the palette id rule the Puppet restates (<see cref="PaletteIds"/>): it must agree with the library's
///     <see cref="NodePalette.GetAutomationId(string)"/>, and short type names must resolve against the ids that exist.
/// </summary>
public class PaletteIdsTests
{
    /// <summary>
    ///     Validates that the Puppet's rule gives exactly the id the library's palette assigns, for plain, punctuated and odd keys.
    /// </summary>
    /// <param name="key">The node type key.</param>
    [Theory]
    [InlineData("demo.audio.Mixer")]
    [InlineData("Simple")]
    [InlineData("com.acme/Weird Key-1")]
    [InlineData("a-b.c d_e")]
    [InlineData("under_score.Name2")]
    [InlineData("café.ünïcode")]
    [InlineData("")]
    public void PaletteIds_ForKey_AgreesWithTheLibrary(string key)
    {
        // Act
        var puppet = PaletteIds.ForKey(key);
        var library = NodePalette.GetAutomationId(key);
        var libraryByType = NodePalette.GetAutomationId(new NodeType(string.IsNullOrEmpty(key) ? "x" : key));

        // Assert
        Assert.Equal(library, puppet);
        Assert.True(string.IsNullOrEmpty(key) || libraryByType == library);
    }

    /// <summary>
    ///     Validates that the fixed ids the Puppet looks for are the library's.
    /// </summary>
    [Fact]
    public void PaletteIds_Constants_AgreeWithTheLibrary()
    {
        // Assert
        Assert.Multiple(
            () => Assert.Equal(NodePalette.FilterAutomationId, PaletteIds.Filter),
            () => Assert.Equal(PaletteIds.Prefix, NodePalette.GetAutomationId(string.Empty)),
            () => Assert.StartsWith(PaletteIds.Palette + "_", PaletteIds.Prefix, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a full key is turned into its id without consulting the existing ids.
    /// </summary>
    [Fact]
    public void PaletteIds_Resolve_FullKey_IsTheComputedId()
    {
        // Act
        var id = PaletteIds.Resolve("demo.audio.Mixer", []);

        // Assert
        Assert.Equal("NodePalette_demo_audio_Mixer", id);
    }

    /// <summary>
    ///     Validates that a short name finds the single existing entry that ends with it, ignores other controls, and falls back to the computed id.
    /// </summary>
    [Fact]
    public void PaletteIds_Resolve_ShortName_FindsTheExistingEntry()
    {
        // Arrange
        string[] existing =
        [
            "NodePalette", "NodePalette_Filter", "NodePalette_demo_audio_Mixer", "NodePalette_demo_math_Add", "NodePalette_demo_audio_Mixer", "Editor",
        ];

        // Act
        var found = PaletteIds.Resolve("Mixer", existing);
        var missing = PaletteIds.Resolve("Nothing", existing);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("NodePalette_demo_audio_Mixer", found),
            () => Assert.Equal("NodePalette_Nothing", missing));
    }

    /// <summary>
    ///     Validates that a short name shared by several entries is reported as ambiguous and lists the candidates.
    /// </summary>
    [Fact]
    public void PaletteIds_Resolve_AmbiguousShortName_Throws()
    {
        // Arrange
        string[] existing = ["NodePalette_demo_audio_Mixer", "NodePalette_demo_video_Mixer"];

        // Act
        var error = Assert.Throws<PuppetException>(() => PaletteIds.Resolve("Mixer", existing));

        // Assert
        Assert.Contains("NodePalette_demo_video_Mixer", error.Message, StringComparison.Ordinal);
    }
}
