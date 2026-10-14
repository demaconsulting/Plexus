using Avalonia;
using DemaConsulting.Plexus.Demo.Gui;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the arithmetic of <see cref="WindowPlacement"/>, which keeps the demo's windows on the screen.
/// </summary>
public class WindowPlacementTests
{
    /// <summary>
    ///     A 1920x1080 screen whose bottom 40 pixels are taken by the taskbar.
    /// </summary>
    private static readonly PixelRect WorkArea = new(0, 0, 1920, 1040);

    /// <summary>
    ///     The client size of a 1280x800 window.
    /// </summary>
    private static readonly Size Client = new(1280, 800);

    /// <summary>
    ///     The frame of that window: the client plus a 32-unit title bar.
    /// </summary>
    private static readonly Size Frame = new(1280, 832);

    /// <summary>
    ///     The smallest size a window may be given.
    /// </summary>
    private static readonly Size Minimum = new(640, 420);

    /// <summary>
    ///     Validates that a window already inside the working area is left exactly as it is.
    /// </summary>
    [Fact]
    public void WindowPlacement_Calculate_InsideWorkArea_ChangesNothing()
    {
        // Arrange / Act
        var result = WindowPlacement.Calculate(WorkArea, 1.0, Frame, Client, Minimum, new PixelPoint(100, 50));

        // Assert
        Assert.Equal(new PixelPoint(100, 50), result.Position);
        Assert.Null(result.Width);
        Assert.Null(result.Height);
    }

    /// <summary>
    ///     Validates that a window hanging off the right and bottom edges is moved back without being resized.
    /// </summary>
    [Fact]
    public void WindowPlacement_Calculate_OffRightAndBottom_MovesWithoutResizing()
    {
        // Arrange / Act
        var result = WindowPlacement.Calculate(WorkArea, 1.0, Frame, Client, Minimum, new PixelPoint(1500, 600));

        // Assert: the frame's far corner now touches the working area's far corner
        Assert.Equal(new PixelPoint(1920 - 1280, 1040 - 832), result.Position);
        Assert.Null(result.Width);
        Assert.Null(result.Height);
    }

    /// <summary>
    ///     Validates that a window above and left of the working area is moved to its top-left corner.
    /// </summary>
    [Fact]
    public void WindowPlacement_Calculate_OffTopLeft_MovesToTopLeft()
    {
        // Arrange
        var workArea = new PixelRect(100, 50, 1920, 1040);

        // Act
        var result = WindowPlacement.Calculate(workArea, 1.0, Frame, Client, Minimum, new PixelPoint(-300, -80));

        // Assert
        Assert.Equal(new PixelPoint(100, 50), result.Position);
        Assert.Null(result.Width);
        Assert.Null(result.Height);
    }

    /// <summary>
    ///     Validates that the frame (title bar included) decides whether the bottom edge is on the screen, not the client.
    /// </summary>
    [Fact]
    public void WindowPlacement_Calculate_UsesFrameHeightNotClientHeight()
    {
        // Arrange: the client alone would fit at y = 230 (230 + 800 = 1030), the frame does not (230 + 832 = 1062)
        // Act
        var result = WindowPlacement.Calculate(WorkArea, 1.0, Frame, Client, Minimum, new PixelPoint(0, 230));

        // Assert
        Assert.Equal(1040 - 832, result.Position.Y);
    }

    /// <summary>
    ///     Validates that the frame is scaled to physical pixels before it is compared with the working area.
    /// </summary>
    [Fact]
    public void WindowPlacement_Calculate_ScaledScreen_ConvertsFrameToPhysicalPixels()
    {
        // Arrange: at 150% a 1280x832 frame is 1920x1248 physical pixels, which fits a 2560x1400 working area
        var workArea = new PixelRect(0, 0, 2560, 1400);

        // Act
        var result = WindowPlacement.Calculate(workArea, 1.5, Frame, Client, Minimum, new PixelPoint(2000, 400));

        // Assert
        Assert.Equal(new PixelPoint(2560 - 1920, 1400 - 1248), result.Position);
        Assert.Null(result.Width);
        Assert.Null(result.Height);
    }

    /// <summary>
    ///     Validates that a window larger than the working area is pinned to the top-left and shrunk, less its chrome.
    /// </summary>
    [Fact]
    public void WindowPlacement_Calculate_TooBig_PinsTopLeftAndShrinksClient()
    {
        // Arrange: a 1000x728 working area, smaller than the window in both directions
        var workArea = new PixelRect(0, 0, 1000, 728);

        // Act
        var result = WindowPlacement.Calculate(workArea, 1.0, Frame, Client, Minimum, new PixelPoint(300, 200));

        // Assert: the 32-unit title bar comes off the height; the width has no chrome
        Assert.Equal(new PixelPoint(0, 0), result.Position);
        Assert.Equal(1000, result.Width);
        Assert.Equal(728 - 32, result.Height);
    }

    /// <summary>
    ///     Validates that a window is never shrunk below its minimum size, and stays pinned to the top-left.
    /// </summary>
    [Fact]
    public void WindowPlacement_Calculate_MinimumDoesNotFit_KeepsMinimumAndTopLeft()
    {
        // Arrange: a 500x400 working area is smaller than the 640x420 minimum
        var workArea = new PixelRect(0, 0, 500, 400);

        // Act
        var result = WindowPlacement.Calculate(workArea, 1.0, Frame, Client, Minimum, new PixelPoint(50, 50));

        // Assert
        Assert.Equal(new PixelPoint(0, 0), result.Position);
        Assert.Equal(640, result.Width);
        Assert.Equal(420, result.Height);
    }

    /// <summary>
    ///     Validates that a zero or negative scaling is treated as 1 instead of dividing by zero.
    /// </summary>
    /// <param name="scaling">The invalid scaling.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-2.0)]
    public void WindowPlacement_Calculate_InvalidScaling_TreatedAsOne(double scaling)
    {
        // Arrange / Act
        var result = WindowPlacement.Calculate(WorkArea, scaling, Frame, Client, Minimum, new PixelPoint(1500, 600));

        // Assert
        Assert.Equal(new PixelPoint(1920 - 1280, 1040 - 832), result.Position);
    }

    /// <summary>
    ///     Validates that a working area on a second monitor (a non-zero origin) is respected on every edge.
    /// </summary>
    [Fact]
    public void WindowPlacement_Calculate_SecondMonitor_StaysWithinItsWorkArea()
    {
        // Arrange: a monitor to the left of the primary one
        var workArea = new PixelRect(-1920, 0, 1920, 1040);

        // Act
        var result = WindowPlacement.Calculate(workArea, 1.0, Frame, Client, Minimum, new PixelPoint(-100, 900));

        // Assert
        Assert.Equal(new PixelPoint(-1280, 1040 - 832), result.Position);
    }
}
