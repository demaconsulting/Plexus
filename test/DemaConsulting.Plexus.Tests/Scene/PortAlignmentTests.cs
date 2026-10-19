namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Unit tests for <see cref="PortAlignment"/>: centered and top-aligned port stacks, the default height rule,
///     layouts, wires between aligned nodes and the proxy ports of collapsed groups.
/// </summary>
public class PortAlignmentTests
{
    /// <summary>
    ///     The classic theme with the port stacks starting below the header.
    /// </summary>
    private static readonly DiagramTheme TopTheme = DiagramTheme.Classic with { PortAlignment = PortAlignment.Top };

    /// <summary>
    ///     Validates that ports are centered by default and that a copy of the theme can opt back to top alignment.
    /// </summary>
    [Fact]
    public void DiagramTheme_PortAlignment_DefaultsToCenterAndCanBeChanged()
    {
        // Assert
        Assert.Equal(PortAlignment.Center, DiagramTheme.Classic.PortAlignment);
        Assert.Equal(PortAlignment.Top, TopTheme.PortAlignment);
        Assert.Equal(PortAlignment.Center, DiagramTheme.Classic.PortAlignment);
    }

    /// <summary>
    ///     Validates that a column with an odd number of ports has its middle port on the box's vertical center.
    /// </summary>
    [Fact]
    public void NodeGeometry_PortEdge_Center_OddCountPutsMiddlePortOnTheBoxCenter()
    {
        // Arrange
        var bounds = new Rect(30, 60, 90, 90);

        // Act
        var rows = Enumerable.Range(0, 3).Select(i => NodeGeometry.PortEdge(bounds, PortDirection.Input, i, 3, DiagramTheme.Classic).Y).ToArray();

        // Assert: pitch 15 around the center at 105
        Assert.Equal([90.0, 105.0, 120.0], rows);
        Assert.Equal(bounds.Center.Y, rows[1]);
    }

    /// <summary>
    ///     Validates that a column with an even number of ports straddles the box center on half-grid positions.
    /// </summary>
    [Fact]
    public void NodeGeometry_PortEdge_Center_EvenCountStraddlesTheBoxCenter()
    {
        // Arrange
        var bounds = new Rect(0, 0, 90, 90);

        // Act
        var rows = Enumerable.Range(0, 2).Select(i => NodeGeometry.PortEdge(bounds, PortDirection.Output, i, 2, DiagramTheme.Classic).Y).ToArray();

        // Assert
        Assert.Equal([37.5, 52.5], rows);
        Assert.Equal(bounds.Center.Y, rows.Average());
    }

    /// <summary>
    ///     Validates that centering follows the actual box height, not the default height.
    /// </summary>
    [Fact]
    public void NodeGeometry_PortEdge_Center_FollowsTheBoxHeight()
    {
        // Arrange: a node resized to 150 high
        var bounds = new Rect(0, 15, 90, 150);

        // Act
        var edge = NodeGeometry.PortEdge(bounds, PortDirection.Input, 0, 1, DiagramTheme.Classic);

        // Assert
        Assert.Equal(new Point(0, 90), edge);
    }

    /// <summary>
    ///     Validates that top alignment keeps the previous rule: the first port sits at the bottom of the header plus the first-port offset, ignoring the count.
    /// </summary>
    [Fact]
    public void NodeGeometry_PortEdge_Top_StartsBelowTheHeaderRegardlessOfCount()
    {
        // Arrange
        var bounds = new Rect(30, 60, 90, 90);
        var offset = TopTheme with { FirstPortOffset = 5 };

        // Act
        var first = NodeGeometry.PortEdge(bounds, PortDirection.Input, 0, 1, TopTheme);
        var third = NodeGeometry.PortEdge(bounds, PortDirection.Output, 2, 3, TopTheme);
        var shifted = NodeGeometry.PortEdge(bounds, PortDirection.Input, 0, 5, offset);

        // Assert
        Assert.Equal(new Point(30, 90), first);
        Assert.Equal(new Point(120, 120), third);
        Assert.Equal(new Point(30, 95), shifted);
    }

    /// <summary>
    ///     Validates the minimum node height for centered columns of ports: twice the header clearance plus the stack span.
    /// </summary>
    /// <param name="ports">The number of ports in the longest column.</param>
    /// <param name="expected">The expected minimum height.</param>
    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 60)]
    [InlineData(2, 75)]
    [InlineData(5, 120)]
    public void NodeGeometry_RequiredHeight_Center_IsTwiceTheHeaderClearancePlusTheSpan(int ports, double expected) =>
        Assert.Equal(expected, NodeGeometry.RequiredHeight(ports, DiagramTheme.Classic));

    /// <summary>
    ///     Validates the minimum node height for top-aligned columns: the previous rule of header, offset, span and bottom padding.
    /// </summary>
    /// <param name="ports">The number of ports in the longest column.</param>
    /// <param name="expected">The expected minimum height.</param>
    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 45)]
    [InlineData(4, 90)]
    [InlineData(5, 105)]
    public void NodeGeometry_RequiredHeight_Top_IsHeaderOffsetSpanAndPadding(int ports, double expected) =>
        Assert.Equal(expected, NodeGeometry.RequiredHeight(ports, TopTheme));

    /// <summary>
    ///     Validates that the clearance used by the height rule honors the first-port offset and that centered ports never rise above the header.
    /// </summary>
    [Fact]
    public void NodeGeometry_DefaultNodeSize_Center_KeepsTheTopPortBelowTheHeader()
    {
        // Arrange
        var theme = DiagramTheme.Classic with { FirstPortOffset = 6 };
        var type = new NodeType("acme.Tall")
        {
            Inputs = { new PortDefinition("a", "f32"), new PortDefinition("b", "f32"), new PortDefinition("c", "f32"), new PortDefinition("d", "f32"), new PortDefinition("e", "f32") },
            Outputs = { new PortDefinition("o", "f32") },
        };

        // Act
        var size = NodeGeometry.DefaultNodeSize(type, theme);
        var box = new Rect(0, 0, size.Width, size.Height);
        var topInput = NodeGeometry.PortEdge(box, PortDirection.Input, 0, 5, theme);
        var bottomInput = NodeGeometry.PortEdge(box, PortDirection.Input, 4, 5, theme);

        // Assert: 2 * (30 + 6) + 4 * 15 = 132, so the first anchor lies exactly header plus offset down and the last as far from the bottom
        Assert.Equal(132, size.Height);
        Assert.Equal(theme.HeaderHeight + theme.FirstPortOffset, topInput.Y);
        Assert.Equal(size.Height - topInput.Y, bottomInput.Y);
    }

    /// <summary>
    ///     Validates that the default size is not shorter than the theme minimum and that a declared size wins regardless of alignment.
    /// </summary>
    [Fact]
    public void NodeGeometry_DefaultNodeSize_Center_KeepsMinimumAndHonorsDeclaredSize()
    {
        // Arrange
        var small = new NodeType("acme.Small") { Inputs = { new PortDefinition("a", "f32") } };
        var declared = new NodeType("acme.Declared") { DefaultSize = new Size(100, 40), Inputs = { new PortDefinition("a", "f32"), new PortDefinition("b", "f32") } };

        // Act / Assert
        Assert.Equal(new Size(90, 90), NodeGeometry.DefaultNodeSize(small, DiagramTheme.Classic));
        Assert.Equal(new Size(100, 40), NodeGeometry.DefaultNodeSize(declared, DiagramTheme.Classic));
    }

    /// <summary>
    ///     Validates that a layout centers its inputs and outputs on the box and that triangles, hit areas and labels share the port row.
    /// </summary>
    [Fact]
    public void NodeLayout_Compute_Center_StacksAreSymmetricAboutTheBoxCenter()
    {
        // Arrange: a mixer has four inputs and one output
        var (diagram, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Mixer", 30, 60, "M");

        // Act
        var layout = NodeLayout.Compute(node, diagram.Theme, new ApproximateTextMeasurer());

        // Assert
        var inputs = layout.Ports.Where(p => p.Port.Direction == PortDirection.Input).ToList();
        var output = layout.Ports.Single(p => p.Port.Direction == PortDirection.Output);
        Assert.Equal(layout.Box.Center.Y, inputs.Average(p => p.Anchor.Y), 9);
        Assert.Equal(layout.Box.Center.Y, output.Anchor.Y, 9);
        for (var i = 1; i < inputs.Count; i++)
        {
            Assert.Equal(diagram.Theme.PortPitch, inputs[i].Anchor.Y - inputs[i - 1].Anchor.Y, 9);
        }

        Assert.All(layout.Ports, p =>
        {
            Assert.Equal(p.Anchor.Y, p.Triangle.Center.Y, 9);
            Assert.Equal(p.Anchor.Y, p.HitBounds.Center.Y, 9);
            Assert.Equal(p.Anchor.Y, p.Label.Bounds.Center.Y, 9);
            Assert.Equal(p.Port.Anchor, p.Anchor);
        });
    }

    /// <summary>
    ///     Validates that a theme with top alignment makes the layout and the model agree on the previous port rows.
    /// </summary>
    [Fact]
    public void NodeLayout_Compute_Top_PortsStartBelowTheHeaderAndMatchTheModel()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        diagram.Theme = TopTheme;
        var node = page.AddNode("acme.Mixer", 30, 60, "M");

        // Act
        var layout = NodeLayout.Compute(node, diagram.Theme, new ApproximateTextMeasurer());

        // Assert: the first input is 30 below the box top, then every 15
        var inputs = layout.Ports.Where(p => p.Port.Direction == PortDirection.Input).ToList();
        Assert.Equal([90.0, 105.0, 120.0, 135.0], inputs.Select(p => p.Anchor.Y));
        Assert.All(layout.Ports, p => Assert.Equal(p.Port.Anchor, p.Anchor));
    }

    /// <summary>
    ///     Validates that the wire between two aligned nodes with the same number of ports is perfectly straight.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_AlignedNodesWithSamePortCounts_RouteStraight()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 30);
        var b = page.AddNode("acme.Consumer", 300, 30);

        // Act
        var wire = page.Connect(a.Port("value"), b.Port("in"));

        // Assert
        Assert.Equal(a.Port("value").Anchor.Y, b.Port("in").Anchor.Y);
        Assert.All(wire.GetPoints(), p => Assert.Equal(a.Port("value").Anchor.Y, p.Y));
        Assert.Equal(2, wire.GetPoints().Count);
    }

    /// <summary>
    ///     Validates that collapsed-group proxy ports are centered on the collapsed box, like node ports.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroup_ProxyPortsAreCenteredOnTheBox()
    {
        // Arrange: a mixer with four inputs fed from outside, and one output leaving
        var (_, page) = SceneTestModel.CreatePage();
        var mixer = page.AddNode("acme.Mixer", 300, 0, "M");
        var consumer = page.AddNode("acme.Consumer", 600, 0, "C");
        foreach (var input in mixer.Inputs)
        {
            var producer = page.AddNode("acme.Producer", 0, mixer.Wires.Count * 120.0);
            page.Connect(producer.Port("value"), input);
        }

        page.Connect(mixer.Port("out"), consumer.Port("in"));
        var group = page.CreateGroup([mixer], "Grp");
        group.IsCollapsed = true;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.True(scene.TryGetGroupLayout(group, out var layout));
        var center = layout.Frame.Center.Y;
        Assert.Equal(4, layout.InputPorts.Count);
        Assert.Equal(center, layout.InputPorts.Average(p => p.Anchor.Y), 9);
        Assert.Equal(center, Assert.Single(layout.OutputPorts).Anchor.Y, 9);
        Assert.Equal(15, layout.InputPorts[1].Anchor.Y - layout.InputPorts[0].Anchor.Y, 9);
        Assert.All(layout.InputPorts.Concat(layout.OutputPorts), p => Assert.Equal(p.Anchor.Y, p.Triangle.Center.Y, 9));
        Assert.True(layout.Frame.Height >= page.Diagram.Theme.GroupTitleHeight + (4 * page.Diagram.Theme.PortPitch));
    }

    /// <summary>
    ///     Validates that top alignment keeps the previous proxy rows below the group's title bar.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroup_Top_ProxyPortsStartBelowTheTitleBar()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        diagram.Theme = TopTheme;
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Gain", 200, 0, "B");
        page.Connect(a.Port("value"), b.Port("in"));
        var group = page.CreateGroup([b], "Grp");
        group.IsCollapsed = true;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.True(scene.TryGetGroupLayout(group, out var layout));
        var input = Assert.Single(layout.InputPorts);
        Assert.Equal(layout.Frame.Top + diagram.Theme.GroupTitleHeight + (diagram.Theme.PortPitch / 2), input.Anchor.Y, 9);
    }
}
