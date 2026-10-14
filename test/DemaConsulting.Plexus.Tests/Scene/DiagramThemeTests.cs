namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Unit tests for <see cref="DiagramTheme"/>: the Classic look, non-destructive customization and derived text styles.
/// </summary>
public class DiagramThemeTests
{
    /// <summary>
    ///     Validates that the Classic theme exposes the documented colors and metrics.
    /// </summary>
    [Fact]
    public void DiagramTheme_Classic_HasDocumentedDefaults()
    {
        // Arrange / Act
        var theme = DiagramTheme.Classic;

        // Assert
        Assert.Equal(Color.Parse("#A9A9A9"), theme.CanvasBackground);
        Assert.Equal(Color.Parse("#7A7A7A"), theme.GridDotColor);
        Assert.Equal(15, theme.GridSize);
        Assert.Equal(Color.Parse("#6B6B6B"), theme.NodeFill);
        Assert.Equal(Color.White, theme.NodeHeaderText);
        Assert.Equal(Color.Parse("#1E7B1E"), theme.NodeNameText);
        Assert.Equal(Color.Black, theme.SummaryText);
        Assert.Equal(Color.Black, theme.PortLabelText);
        Assert.Equal(Color.Black, theme.WireColor);
        Assert.Equal(2, theme.WireThickness);
        Assert.Equal(Color.Parse("#1E90FF"), theme.SelectionColor);
        Assert.Equal(Color.Parse("#FFFFC0"), theme.AnnotationFill);
        Assert.Equal(Color.Parse("#C8B464"), theme.AnnotationBorder);
        Assert.Equal("Segoe UI", theme.FontFamily);
        Assert.True(theme.GroupFill.A < 255);
        Assert.False(theme.HeaderTextStyle.Bold);
        Assert.Equal(TextAlignment.Center, theme.NameTextStyle.Alignment);
    }

    /// <summary>
    ///     Validates that the Classic theme draws junction dots by default and offers handle metrics for selected wires.
    /// </summary>
    [Fact]
    public void DiagramTheme_Classic_WireJunctionAndHandleDefaults()
    {
        // Arrange / Act
        var theme = DiagramTheme.Classic;

        // Assert
        Assert.True(theme.WireJunctionDots);
        Assert.Equal(3, theme.WireJunctionRadius);
        Assert.Equal(8, theme.WireHandleSize);
    }

    /// <summary>
    ///     Validates that the Classic theme smooths small wire jogs by default and that a customized copy can switch that off.
    /// </summary>
    [Fact]
    public void DiagramTheme_WireSmoothSmallJogs_DefaultsToTrueAndCanBeSwitchedOff()
    {
        // Arrange / Act
        var custom = DiagramTheme.Classic with { WireSmoothSmallJogs = false };

        // Assert
        Assert.True(DiagramTheme.Classic.WireSmoothSmallJogs);
        Assert.False(custom.WireSmoothSmallJogs);
    }

    /// <summary>
    ///     Validates that the Classic theme draws crossing jumps by default with a radius of 4, and that a customized copy can change both.
    /// </summary>
    [Fact]
    public void DiagramTheme_WireCrossingJumps_DefaultToOnWithRadiusFourAndCanBeChanged()
    {
        // Arrange / Act
        var custom = DiagramTheme.Classic with { WireCrossingJumps = false, WireJumpRadius = 6 };

        // Assert
        Assert.Multiple(
            () => Assert.True(DiagramTheme.Classic.WireCrossingJumps),
            () => Assert.Equal(4, DiagramTheme.Classic.WireJumpRadius),
            () => Assert.False(custom.WireCrossingJumps),
            () => Assert.Equal(6, custom.WireJumpRadius));
    }

    /// <summary>
    ///     Validates that a customized copy changes only the named settings and leaves the shared Classic theme untouched.
    /// </summary>
    [Fact]
    public void DiagramTheme_With_CustomizedCopy_LeavesClassicUnchanged()
    {
        // Arrange
        var classicFill = DiagramTheme.Classic.NodeFill;
        var classicGrid = DiagramTheme.Classic.GridSize;

        // Act
        var custom = DiagramTheme.Classic with { NodeFill = Color.Parse("#336699"), GridSize = 20 };

        // Assert: the copy carries the new values and keeps every other setting
        Assert.Equal(Color.Parse("#336699"), custom.NodeFill);
        Assert.Equal(20, custom.GridSize);
        Assert.Equal(DiagramTheme.Classic.WireColor, custom.WireColor);
        Assert.Equal(DiagramTheme.Classic.HeaderHeight, custom.HeaderHeight);

        // The shared instance is not modified, and repeated reads return the same instance
        Assert.Equal(classicFill, DiagramTheme.Classic.NodeFill);
        Assert.Equal(classicGrid, DiagramTheme.Classic.GridSize);
        Assert.Same(DiagramTheme.Classic, DiagramTheme.Classic);
    }

    /// <summary>
    ///     Validates that the derived text styles follow the theme's font family, sizes and colors.
    /// </summary>
    [Fact]
    public void DiagramTheme_TextStyles_FollowFontSettings()
    {
        // Arrange
        var theme = DiagramTheme.Classic with
        {
            FontFamily = "Arial",
            HeaderFontSize = 20,
            NameFontSize = 21,
            SummaryFontSize = 9,
            PortLabelFontSize = 8,
            AnnotationFontSize = 17,
            NodeHeaderText = Color.Parse("#010203"),
        };

        // Act / Assert
        Assert.Equal("Arial", theme.HeaderTextStyle.FontFamily);
        Assert.Equal(20, theme.HeaderTextStyle.FontSize);
        Assert.Equal(Color.Parse("#010203"), theme.HeaderTextStyle.Color);
        Assert.Equal(21, theme.NameTextStyle.FontSize);
        Assert.Equal(9, theme.SummaryTextStyle.FontSize);
        Assert.Equal(8, theme.PortLabelTextStyle.FontSize);
        Assert.Equal(17, theme.AnnotationTextStyle.FontSize);
        Assert.Equal("Arial", theme.GroupTitleTextStyle.FontFamily);
        Assert.True(theme.GroupTitleTextStyle.Bold);
    }
}
