using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the element conveniences <see cref="DiagramElement.IsVisible"/> and <see cref="DiagramElement.GetBounds"/>.
/// </summary>
public class ElementConvenienceTests
{
    /// <summary>
    ///     Validates that elements are visible until an ancestor group is collapsed, at any nesting depth.
    /// </summary>
    [Fact]
    public void DiagramElement_IsVisible_FalseWhenAnyAncestorGroupIsCollapsed()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");
        var a = page.AddNode("acme.Producer", 0, 0);
        var b = page.AddNode("acme.Consumer", 200, 0);
        var inner = page.CreateGroup([a], "Inner");
        var outer = page.CreateGroup([inner, b], "Outer");
        Assert.True(a.IsVisible);

        // Act
        inner.IsCollapsed = true;

        // Assert: the collapsed group itself and its siblings stay visible, its members do not
        Assert.False(a.IsVisible);
        Assert.True(inner.IsVisible);
        Assert.True(b.IsVisible);

        // Act
        outer.IsCollapsed = true;

        // Assert: an outer collapse hides the inner group as well
        Assert.False(inner.IsVisible);
        Assert.False(b.IsVisible);
        Assert.True(outer.IsVisible);

        // Act
        outer.IsCollapsed = false;
        inner.IsCollapsed = false;

        // Assert
        Assert.True(a.IsVisible);
    }

    /// <summary>
    ///     Validates that nodes, annotations and groups report their own boxes.
    /// </summary>
    [Fact]
    public void DiagramElement_GetBounds_NodeAnnotationGroup_ReturnTheirBounds()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 30, 45);
        var annotation = page.AddAnnotation("Note", new Rect(200, 10, 120, 60));
        var group = page.CreateGroup([node], "G");

        // Assert
        Assert.Equal(node.Bounds, node.GetBounds());
        Assert.Equal(new Rect(200, 10, 120, 60), annotation.GetBounds());
        Assert.Equal(group.Bounds, group.GetBounds());
    }

    /// <summary>
    ///     Validates that a wire's bounds cover its anchors and every bend.
    /// </summary>
    [Fact]
    public void Wire_GetBounds_CoversFullPolyline()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0);
        var consumer = page.AddNode("acme.Consumer", 300, 100);
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(150, 30), new Point(150, 400)]);

        // Act
        var bounds = wire.GetBounds();

        // Assert
        var points = wire.GetPoints();
        Assert.Equal(points.Min(p => p.X), bounds.Left);
        Assert.Equal(points.Max(p => p.X), bounds.Right);
        Assert.Equal(30, bounds.Top);
        Assert.Equal(400, bounds.Bottom);
        Assert.True(bounds.Contains(wire.Source.Anchor));
        Assert.True(bounds.Contains(wire.Target.Anchor));
    }

    /// <summary>
    ///     Validates that a straight wire has a zero-height box.
    /// </summary>
    [Fact]
    public void Wire_GetBounds_StraightWire_HasZeroHeight()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0);
        var consumer = page.AddNode("acme.Consumer", 400, 0);
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), []);

        // Act
        var bounds = wire.GetBounds();

        // Assert
        Assert.Equal(0, bounds.Height);
        Assert.True(bounds.Width > 0);
    }
}
