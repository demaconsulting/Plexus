using Avalonia;
using DemaConsulting.Plexus.Demo.Gui.Inspectors;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the pure label layout of <see cref="SignalPlot"/>: where region labels and the vertical axis title go, so they
///     never overlap the tick labels or each other.
/// </summary>
public class SignalPlotLayoutTests
{
    /// <summary>
    ///     The plot area the tests lay labels out in: 44 px from the left (the tick-label column), 300 px wide.
    /// </summary>
    private static readonly Rect Area = new(44, 20, 300, 180);

    /// <summary>
    ///     Validates that a label that fits its region is centered over it.
    /// </summary>
    [Fact]
    public void SignalPlot_LayoutRegionLabels_WideSegments_AreCentered()
    {
        // Arrange: two wide regions with 8 px labels
        (double Left, double Right, double Width)[] labels = [(44, 144, 8), (144, 344, 8)];

        // Act
        var positions = SignalPlot.LayoutRegionLabels(labels, Area.Left, Area.Right);

        // Assert
        Assert.Equal(new double?[] { 90, 240 }, positions);
    }

    /// <summary>
    ///     Validates that the label of a region narrower than the label at the left edge is shifted inside the area, right of
    ///     the tick labels, and that the next label moves clear of it.
    /// </summary>
    [Fact]
    public void SignalPlot_LayoutRegionLabels_ShortFirstSegment_StaysRightOfAxis()
    {
        // Arrange: a 2 px attack, then a 10 px decay, then a wide rest (an envelope with a very short attack)
        (double Left, double Right, double Width)[] labels = [(44, 46, 7), (46, 56, 7), (56, 344, 7)];

        // Act
        var positions = SignalPlot.LayoutRegionLabels(labels, Area.Left, Area.Right);

        // Assert: A starts inside the area (the tick labels end at Area.Left - 4); D starts after A plus a gap
        var a = Assert.IsType<double>(positions[0]);
        var d = Assert.IsType<double>(positions[1]);
        Assert.True(a >= Area.Left + 3);
        Assert.True(d >= a + 7 + 4);
        Assert.NotNull(positions[2]);
    }

    /// <summary>
    ///     Validates that a label is skipped when there is no room for it near its own region.
    /// </summary>
    [Fact]
    public void SignalPlot_LayoutRegionLabels_NoRoom_IsSkipped()
    {
        // Arrange: three zero-width regions on top of each other at the left edge, with wide labels
        (double Left, double Right, double Width)[] labels = [(44, 44, 20), (44, 44, 20), (44, 44, 20)];

        // Act
        var positions = SignalPlot.LayoutRegionLabels(labels, Area.Left, Area.Right);

        // Assert: the first fits at the edge; the others would land far from their region and are left out
        Assert.NotNull(positions[0]);
        Assert.Null(positions[1]);
        Assert.Null(positions[2]);
    }

    /// <summary>
    ///     Validates that placed labels never overlap each other and stay inside the area, whatever the regions.
    /// </summary>
    [Fact]
    public void SignalPlot_LayoutRegionLabels_NeverOverlap()
    {
        // Arrange: regions of mixed widths, including one at the very right edge
        (double Left, double Right, double Width)[] labels = [(44, 47, 7), (47, 60, 7), (60, 62, 7), (62, 340, 7), (340, 344, 7)];

        // Act
        var positions = SignalPlot.LayoutRegionLabels(labels, Area.Left, Area.Right);

        // Assert
        var placed = positions.Select((x, i) => (X: x, labels[i].Width)).Where(p => p.X is not null).Select(p => (Left: p.X!.Value, Right: p.X.Value + p.Width)).ToList();
        Assert.NotEmpty(placed);
        Assert.All(placed, p => Assert.True(p.Left >= Area.Left + 3 && p.Right <= Area.Right - 3));
        Assert.All(placed.Zip(placed.Skip(1)), pair => Assert.True(pair.Second.Left >= pair.First.Right + 4));
    }

    /// <summary>
    ///     Validates that the vertical axis title clears the top tick label and stays inside the control.
    /// </summary>
    [Fact]
    public void SignalPlot_YAxisTitle_ClearsTopTickLabel()
    {
        // Arrange: a "100%" tick label on the top edge of the area and an "Amplitude" title
        var tick = SignalPlot.YTickLabelBounds(Area, Area.Top, new Size(26, 13));
        var titleSize = new Size(48, 13);

        // Act
        var title = new Rect(SignalPlot.YAxisTitleOrigin(Area, titleSize), titleSize);

        // Assert
        Assert.False(title.Intersects(tick));
        Assert.True(title.Top >= 0);
        Assert.True(title.Bottom <= Area.Top);
        Assert.True(tick.Right <= Area.Left);
    }
}
