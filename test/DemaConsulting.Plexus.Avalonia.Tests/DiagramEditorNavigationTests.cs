using Avalonia;
using Avalonia.Controls;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Unit tests of the editor's navigation: <see cref="DiagramEditor.BringIntoView"/> and <see cref="DiagramEditor.ZoomToFit(IEnumerable{DiagramElement}, double)"/>.
/// </summary>
public class DiagramEditorNavigationTests
{
    /// <summary>
    ///     Validates that a far-away node is scrolled to the center of the editor at the current zoom.
    /// </summary>
    [Fact]
    public Task DiagramEditor_BringIntoView_FarNode_CentersItWithoutChangingZoom() => UiTest.Run(host =>
    {
        // Arrange
        host.AddSource(0, 0);
        var far = host.AddSink(3000, 2000);

        // Act
        host.Editor.BringIntoView(far);

        // Assert: zoom unchanged, the node is in view near the center
        var box = host.Editor.GetElementScreenBounds(far);
        Assert.Multiple(
            () => Assert.Equal(1.0, host.Editor.Zoom),
            () => Assert.True(View(host.Editor).Contains(box)),
            () => Assert.True(Distance(box.Center, View(host.Editor).Center) < 40));
    });

    /// <summary>
    ///     Validates that bringing a node on another page into view switches the editor to that page.
    /// </summary>
    [Fact]
    public Task DiagramEditor_BringIntoView_NodeOnOtherPage_SwitchesPage() => UiTest.Run(host =>
    {
        // Arrange
        var second = host.Diagram.Pages.Add("Second");
        var node = second.AddNode("test.Source", 2000, 1500);

        // Act
        host.Editor.BringIntoView(node);

        // Assert
        Assert.Multiple(
            () => Assert.Same(second, host.Editor.Page),
            () => Assert.True(View(host.Editor).Contains(host.Editor.GetElementScreenBounds(node))));
    });

    /// <summary>
    ///     Validates that a node hidden in a collapsed group brings the group into view instead.
    /// </summary>
    [Fact]
    public Task DiagramEditor_BringIntoView_NodeInCollapsedGroup_CentersGroup() => UiTest.Run(host =>
    {
        // Arrange: a collapsed group far away around the node
        var node = host.AddSource(3000, 2000);
        var group = host.Page.CreateGroup([node], "Far");
        group.IsCollapsed = true;

        // Act
        host.Editor.BringIntoView(node);

        // Assert: the collapsed group's box is centered
        var box = host.Editor.GetElementScreenBounds(group);
        Assert.Multiple(
            () => Assert.True(View(host.Editor).Contains(box)),
            () => Assert.True(Distance(box.Center, View(host.Editor).Center) < 1));
    });

    /// <summary>
    ///     Validates that a wire with an end hidden in a collapsed group shows the visible end and that group.
    /// </summary>
    [Fact]
    public Task DiagramEditor_BringIntoView_WireWithHiddenEnd_ShowsVisibleEndAndGroup() => UiTest.Run(host =>
    {
        // Arrange: a wire whose target is inside a far collapsed group
        var a = host.AddSource(0, 0, "A");
        var b = host.AddSink(1500, 1000, "B");
        var wire = host.Page.Connect(a.Port("value"), b.Port("value"));
        var group = host.Page.CreateGroup([b], "Far");
        group.IsCollapsed = true;

        // Act
        host.Editor.BringIntoView(wire);

        // Assert: both visible boxes are in view, which needs zooming out
        var view = View(host.Editor);
        Assert.Multiple(
            () => Assert.True(view.Contains(host.Editor.GetElementScreenBounds(a))),
            () => Assert.True(view.Contains(host.Editor.GetElementScreenBounds(group))),
            () => Assert.True(host.Editor.Zoom < 1));
    });

    /// <summary>
    ///     Validates that an element larger than the view makes the editor zoom out just enough.
    /// </summary>
    [Fact]
    public Task DiagramEditor_BringIntoView_ElementLargerThanView_ZoomsOut() => UiTest.Run(host =>
    {
        // Arrange
        var note = host.Page.AddAnnotation("big", new Geometry.Rect(100, 100, 3000, 2000));

        // Act
        host.Editor.BringIntoView(note);

        // Assert
        Assert.Multiple(
            () => Assert.True(host.Editor.Zoom < 1),
            () => Assert.True(View(host.Editor).Contains(host.Editor.GetElementScreenBounds(note))));
    });

    /// <summary>
    ///     Validates that a request made before the editor has a size is applied after its first layout.
    /// </summary>
    [Fact]
    public Task DiagramEditor_BringIntoView_BeforeFirstLayout_AppliesAfterLayout() => UiTest.Run(host =>
    {
        // Arrange: an editor that is not in a window yet
        var far = host.AddSink(3000, 2000);
        var editor = new DiagramEditor { Page = host.Page };

        // Act
        editor.BringIntoView(far);
        var offsetBefore = editor.Offset;
        var window = new Window { Width = 800, Height = 600, Content = editor };
        window.Show();
        window.UpdateLayout();

        // Assert: nothing happened before the layout, and afterwards the node is in view
        Assert.Multiple(
            () => Assert.Equal(default, offsetBefore),
            () => Assert.True(View(editor).Contains(editor.GetElementScreenBounds(far))));
        window.Close();
    });

    /// <summary>
    ///     Validates that elements that are not on a page of the shown diagram are refused.
    /// </summary>
    [Fact]
    public Task DiagramEditor_BringIntoView_DetachedElement_ThrowsArgumentException() => UiTest.Run(host =>
    {
        // Arrange: a removed node and a node of another diagram
        var removed = host.AddSource(0, 0);
        removed.Remove();
        var other = new Diagram(host.Diagram.Types).Pages.Add("Other").AddNode("test.Source", 0, 0);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => host.Editor.BringIntoView(removed));
        Assert.Throws<ArgumentException>(() => host.Editor.BringIntoView(other));
        Assert.Throws<ArgumentNullException>(() => host.Editor.BringIntoView(null!));
        Assert.Same(host.Page, host.Editor.Page);
    });

    /// <summary>
    ///     Validates that fitting a subset of nodes shows those nodes and not the others.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ZoomToFitElements_SubsetOfNodes_FitsOnlyThose() => UiTest.Run(host =>
    {
        // Arrange
        var a = host.AddSource(1000, 1000);
        var b = host.AddSink(1600, 1300);
        var c = host.AddSink(6000, 6000);

        // Act
        host.Editor.ZoomToFit([a, b]);

        // Assert
        var view = View(host.Editor);
        Assert.Multiple(
            () => Assert.True(view.Contains(host.Editor.GetElementScreenBounds(a))),
            () => Assert.True(view.Contains(host.Editor.GetElementScreenBounds(b))),
            () => Assert.False(view.Intersects(host.Editor.GetElementScreenBounds(c))),
            () => Assert.True(host.Editor.Zoom <= 1));
    });

    /// <summary>
    ///     Validates that fitting a node hidden in a collapsed group fits the collapsed group instead.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ZoomToFitElements_NodeInCollapsedGroup_FitsGroup() => UiTest.Run(host =>
    {
        // Arrange: a large group, collapsed, far from another node
        var hidden = host.AddSource(3000, 2000);
        var sibling = host.AddSink(3600, 2600);
        var group = host.Page.CreateGroup([hidden, sibling], "Far");
        group.IsCollapsed = true;
        var other = host.AddSink(0, 0);

        // Act
        host.Editor.ZoomToFit([hidden]);

        // Assert: the small collapsed box fits at actual size (the expanded group would need zooming out)
        var view = View(host.Editor);
        Assert.Multiple(
            () => Assert.True(view.Contains(host.Editor.GetElementScreenBounds(group))),
            () => Assert.False(view.Intersects(host.Editor.GetElementScreenBounds(other))),
            () => Assert.Equal(1.0, host.Editor.Zoom));
    });

    /// <summary>
    ///     Validates that elements spanning pages switch to the first element's page and ignore the others.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ZoomToFitElements_OtherPage_SwitchesToFirstElementsPage() => UiTest.Run(host =>
    {
        // Arrange
        var main = host.AddSource(0, 0);
        var second = host.Diagram.Pages.Add("Second");
        var node = second.AddNode("test.Source", 2500, 2500);

        // Act
        host.Editor.ZoomToFit([node, main]);

        // Assert: the second page is shown with its node in view, at actual size (one node fits)
        Assert.Multiple(
            () => Assert.Same(second, host.Editor.Page),
            () => Assert.True(View(host.Editor).Contains(host.Editor.GetElementScreenBounds(node))),
            () => Assert.Equal(1.0, host.Editor.Zoom));
    });

    /// <summary>
    ///     Validates that fitting no elements changes nothing.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ZoomToFitElements_Empty_DoesNothing() => UiTest.Run(host =>
    {
        // Arrange
        host.AddSource(3000, 3000);
        host.Editor.Offset = new AvaloniaPoint(10, 20);

        // Act
        host.Editor.ZoomToFit(Array.Empty<DiagramElement>());

        // Assert
        Assert.Multiple(
            () => Assert.Equal(new AvaloniaPoint(10, 20), host.Editor.Offset),
            () => Assert.Equal(1.0, host.Editor.Zoom),
            () => Assert.Throws<ArgumentNullException>(() => host.Editor.ZoomToFit((IEnumerable<DiagramElement>)null!)));
    });

    /// <summary>
    ///     Validates that fitting elements before the first layout is applied once the editor has a size.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ZoomToFitElements_BeforeFirstLayout_AppliesAfterLayout() => UiTest.Run(host =>
    {
        // Arrange
        var a = host.AddSource(0, 0);
        var b = host.AddSink(3000, 2000);
        var editor = new DiagramEditor { Page = host.Page };

        // Act
        editor.ZoomToFit([a, b]);
        var zoomBefore = editor.Zoom;
        var window = new Window { Width = 800, Height = 600, Content = editor };
        window.Show();
        window.UpdateLayout();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1.0, zoomBefore),
            () => Assert.True(editor.Zoom < 1),
            () => Assert.True(View(editor).Contains(editor.GetElementScreenBounds(b))));
        window.Close();
    });

    /// <summary>
    ///     Validates that the latest view request wins, whichever method made it.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ZoomToFit_LatestRequestWins_AcrossOverloads() => UiTest.Run(host =>
    {
        // Arrange: far-apart nodes that a whole-page fit would zoom out for
        var a = host.AddSource(0, 0);
        host.AddSink(3000, 2000);
        var editor = new DiagramEditor { Page = host.Page };

        // Act: a page fit, then a bring-into-view, both before the first layout
        editor.ZoomToFit();
        editor.BringIntoView(a);
        var window = new Window { Width = 800, Height = 600, Content = editor };
        window.Show();
        window.UpdateLayout();

        // Assert: only the bring-into-view was applied (no zoom-out)
        Assert.Multiple(
            () => Assert.Equal(1.0, editor.Zoom),
            () => Assert.True(View(editor).Contains(editor.GetElementScreenBounds(a))));
        window.Close();
    });

    /// <summary>
    ///     Gets the editor's own area in control coordinates.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The visible area.</returns>
    private static AvaloniaRect View(DiagramEditor editor) => new(editor.Bounds.Size);

    /// <summary>
    ///     Computes the distance between two points.
    /// </summary>
    /// <param name="a">One point.</param>
    /// <param name="b">The other point.</param>
    /// <returns>The distance.</returns>
    private static double Distance(AvaloniaPoint a, AvaloniaPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
