using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Unit tests of the operating-system clipboard bridge and of the text formatting helper shared by painting and measuring.
/// </summary>
public class DiagramClipboardTests
{
    /// <summary>
    ///     The Plexus clipboard format as the bridge registers it.
    /// </summary>
    private static readonly DataFormat<string> PlexusFormat = DataFormat.CreateStringApplicationFormat(PlexusClipboard.FormatId);

    /// <summary>
    ///     Validates that one write puts both the Plexus format and the plain-text fallback on the clipboard.
    /// </summary>
    [Fact]
    public Task SystemDiagramClipboard_SetAsync_Json_WritesTaggedFormatAndTextTogether() => UiTest.RunAsync(async host =>
    {
        // Arrange: the window's real clipboard wrapped by the bridge
        var system = SystemClipboardOf(host);
        var bridge = new SystemDiagramClipboard(system);

        // Act
        await bridge.SetAsync("{\"plexus\":1}");

        // Assert: both representations carry the same JSON
        var tagged = await system.TryGetValueAsync(PlexusFormat);
        var text = await system.TryGetTextAsync();
        Assert.Multiple(
            () => Assert.Equal("{\"plexus\":1}", tagged),
            () => Assert.Equal("{\"plexus\":1}", text));
    });

    /// <summary>
    ///     Validates that reading prefers the Plexus format over differing plain text.
    /// </summary>
    [Fact]
    public Task SystemDiagramClipboard_GetAsync_TaggedFormatPresent_PrefersIt() => UiTest.RunAsync(async host =>
    {
        // Arrange: a clipboard entry whose tagged and text representations differ
        var system = SystemClipboardOf(host);
        var item = new DataTransferItem();
        item.Set(PlexusFormat, "tagged-json");
        item.SetText("plain words");
        var transfer = new DataTransfer();
        transfer.Add(item);
        await system.SetDataAsync(transfer);

        // Act
        var read = await new SystemDiagramClipboard(system).GetAsync();

        // Assert
        Assert.Equal("tagged-json", read);
    });

    /// <summary>
    ///     Validates that reading falls back to plain text when another application put only text on the clipboard.
    /// </summary>
    [Fact]
    public Task SystemDiagramClipboard_GetAsync_OnlyPlainText_FallsBackToText() => UiTest.RunAsync(async host =>
    {
        // Arrange: text copied in some other application
        var system = SystemClipboardOf(host);
        await system.ClearAsync();
        var item = new DataTransferItem();
        item.SetText("copied elsewhere");
        var transfer = new DataTransfer();
        transfer.Add(item);
        await system.SetDataAsync(transfer);

        // Act
        var read = await new SystemDiagramClipboard(system).GetAsync();

        // Assert
        Assert.Equal("copied elsewhere", read);
    });

    /// <summary>
    ///     Validates that reading an empty clipboard yields null rather than an error.
    /// </summary>
    [Fact]
    public Task SystemDiagramClipboard_GetAsync_EmptyClipboard_ReturnsNull() => UiTest.RunAsync(async host =>
    {
        // Arrange
        var system = SystemClipboardOf(host);
        await system.ClearAsync();

        // Act
        var read = await new SystemDiagramClipboard(system).GetAsync();

        // Assert
        Assert.Null(read);
    });

    /// <summary>
    ///     Validates that formatting empty text yields a single space, so the line height can still be measured.
    /// </summary>
    [Fact]
    public Task TextFormats_Create_EmptyText_FormatsASingleSpace() => UiTest.Run(() =>
    {
        // Arrange
        var style = new TextStyle("Arial", 12, PlexusColor.Black);

        // Act
        var empty = TextFormats.Create(string.Empty, style);

        // Assert: the space has a width and the line has a height
        Assert.Multiple(
            () => Assert.True(empty.WidthIncludingTrailingWhitespace > 0),
            () => Assert.True(empty.Height > 0));
    });

    /// <summary>
    ///     Validates that the style's font size reaches the formatted text: a larger size is taller and wider.
    /// </summary>
    [Fact]
    public Task TextFormats_Create_LargerFontSize_IsTallerAndWider() => UiTest.Run(() =>
    {
        // Arrange
        var small = new TextStyle("Arial", 10, PlexusColor.Black);
        var large = small with { FontSize = 30 };

        // Act
        var smallText = TextFormats.Create("Plexus", small);
        var largeText = TextFormats.Create("Plexus", large);

        // Assert
        Assert.Multiple(
            () => Assert.True(largeText.Height > smallText.Height),
            () => Assert.True(largeText.Width > smallText.Width));
    });

    /// <summary>
    ///     Validates that a bold style, which needs its own typeface, formats text at least as wide as the regular style.
    /// </summary>
    [Fact]
    public Task TextFormats_Create_BoldStyle_IsAtLeastAsWideAsRegular() => UiTest.Run(() =>
    {
        // Arrange: the same text in both weights, twice so the second request is served from the typeface cache
        var regular = new TextStyle("Arial", 20, PlexusColor.Parse("#336699"));
        var bold = regular with { Bold = true };

        // Act
        var regularText = TextFormats.Create("Plexus", regular);
        var boldText = TextFormats.Create("Plexus", bold);
        var boldAgain = TextFormats.Create("Plexus", bold);

        // Assert
        Assert.Multiple(
            () => Assert.True(boldText.Width >= regularText.Width),
            () => Assert.Equal(boldText.Width, boldAgain.Width));
    });

    /// <summary>
    ///     Gets the operating-system clipboard of the editor's window.
    /// </summary>
    /// <param name="host">The editor host.</param>
    /// <returns>The clipboard.</returns>
    private static IClipboard SystemClipboardOf(EditorHost host) =>
        TopLevel.GetTopLevel(host.Editor)?.Clipboard ?? throw new InvalidOperationException("The window has no clipboard.");
}
