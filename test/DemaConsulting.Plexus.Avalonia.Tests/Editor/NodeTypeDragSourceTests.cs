using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Unit tests of the helper that turns a palette control into a node type drag source.
/// </summary>
/// <remarks>
///     The headless platform has no operating-system drag support, so a started drag cannot be observed
///     at the platform. The helper holds the pending press until it either becomes a drag or is
///     abandoned, so the tests observe that state: a pending press is consumed exactly when the
///     helper asks the editor to start the drag.
/// </remarks>
public class NodeTypeDragSourceTests
{
    /// <summary>
    ///     The private field holding the pending press.
    /// </summary>
    private static readonly FieldInfo PressField =
        typeof(NodeTypeDragSource).GetField("_press", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The drag source has no pending-press field.");

    /// <summary>
    ///     Validates that a press stays pending below the threshold and is consumed to start the drag once it is passed.
    /// </summary>
    [Fact]
    public Task NodeTypeDragSource_Move_PastThreshold_ConsumesPressToStartDrag() => UiTest.Run(() =>
    {
        // Arrange: a palette entry in a window, made a drag source
        var (window, entry) = CreateWindow();
        using var source = new NodeTypeDragSource(entry, "test.Source");

        // Act / Assert: the press is pending, a small movement keeps it pending
        window.MouseDown(new Point(80, 100), MouseButton.Left);
        var afterPress = HasPendingPress(source);
        window.MouseMove(new Point(82, 101), RawInputModifiers.LeftMouseButton);
        var afterSmallMove = HasPendingPress(source);

        // Act / Assert: moving well past the threshold hands the press to the drag
        window.MouseMove(new Point(120, 100), RawInputModifiers.LeftMouseButton);
        var afterBigMove = HasPendingPress(source);
        window.Close();
        Assert.Multiple(
            () => Assert.True(afterPress),
            () => Assert.True(afterSmallMove),
            () => Assert.False(afterBigMove));
    });

    /// <summary>
    ///     Validates that releasing the button abandons the pending press, so later movement starts no drag.
    /// </summary>
    [Fact]
    public Task NodeTypeDragSource_Release_AbandonsPendingPress() => UiTest.Run(() =>
    {
        // Arrange
        var (window, entry) = CreateWindow();
        using var source = new NodeTypeDragSource(entry, "test.Source");
        window.MouseDown(new Point(80, 100), MouseButton.Left);
        Assert.True(HasPendingPress(source));

        // Act: release, then move with no button held
        window.MouseUp(new Point(80, 100), MouseButton.Left);
        var afterRelease = HasPendingPress(source);
        window.MouseMove(new Point(120, 100), RawInputModifiers.None);
        var afterMove = HasPendingPress(source);
        window.Close();

        // Assert
        Assert.Multiple(
            () => Assert.False(afterRelease),
            () => Assert.False(afterMove));
    });

    /// <summary>
    ///     Validates that only the left button arms a drag.
    /// </summary>
    [Fact]
    public Task NodeTypeDragSource_Press_RightButton_DoesNotArm() => UiTest.Run(() =>
    {
        // Arrange
        var (window, entry) = CreateWindow();
        using var source = new NodeTypeDragSource(entry, "test.Source");

        // Act
        window.MouseDown(new Point(80, 100), MouseButton.Right);
        var armed = HasPendingPress(source);
        window.MouseUp(new Point(80, 100), MouseButton.Right);
        window.Close();

        // Assert
        Assert.False(armed);
    });

    /// <summary>
    ///     Validates that a disposed source no longer listens to the control.
    /// </summary>
    [Fact]
    public Task NodeTypeDragSource_Dispose_StopsListening() => UiTest.Run(() =>
    {
        // Arrange
        var (window, entry) = CreateWindow();
        var source = new NodeTypeDragSource(entry, "test.Source");
        source.Dispose();

        // Act
        window.MouseDown(new Point(80, 100), MouseButton.Left);
        var armed = HasPendingPress(source);
        window.Close();

        // Assert
        Assert.False(armed);
    });

    /// <summary>
    ///     Validates that on a platform that cannot start a drag the failure stays inside the helper and later presses work again.
    /// </summary>
    [Fact]
    public Task NodeTypeDragSource_Move_PlatformCannotDrag_IsIgnoredAndStaysUsable() => UiTest.Run(() =>
    {
        // Arrange: the headless platform has no drag support, like the platforms the helper must tolerate
        var (window, entry) = CreateWindow();
        using var source = new NodeTypeDragSource(entry, "test.Source");

        // Act: a complete press-drag-release sequence must not throw, and the helper must arm again for the next press
        var failure = Record.Exception(() =>
        {
            window.MouseDown(new Point(80, 100), MouseButton.Left);
            window.MouseMove(new Point(120, 100), RawInputModifiers.LeftMouseButton);
            window.MouseUp(new Point(120, 100), MouseButton.Left);
        });
        window.MouseMove(new Point(60, 140), RawInputModifiers.None);
        window.MouseDown(new Point(60, 140), MouseButton.Left);
        var armedAgain = HasPendingPress(source);
        window.MouseUp(new Point(60, 140), MouseButton.Left);
        window.Close();

        // Assert
        Assert.Multiple(
            () => Assert.Null(failure),
            () => Assert.True(armedAgain));
    });

    /// <summary>
    ///     Determines whether the source holds a pending press.
    /// </summary>
    /// <param name="source">The drag source.</param>
    /// <returns><see langword="true"/> when a press is waiting to become a drag.</returns>
    private static bool HasPendingPress(NodeTypeDragSource source) => PressField.GetValue(source) is not null;

    /// <summary>
    ///     Creates a window holding one hit-testable control, standing in for a palette entry.
    /// </summary>
    /// <returns>The window and the control.</returns>
    private static (Window Window, Border Entry) CreateWindow()
    {
        var entry = new Border { Width = 100, Height = 100, Background = Brushes.Gray };
        var window = new Window { Width = 200, Height = 200, Content = entry };
        window.Show();
        return (window, entry);
    }
}
