namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Unit tests for <see cref="NodeLayout"/>: port anchors, label placement, node text arrangement, icon and bounds.
/// </summary>
public class NodeLayoutTests
{
    /// <summary>
    ///     Validates that every port anchor equals the model's anchor and the shared geometry function.
    /// </summary>
    [Fact]
    public void NodeLayout_Compute_PortAnchors_MatchModelAndNodeGeometry()
    {
        // Arrange: a mixer has four inputs and one output
        var (diagram, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Mixer", 30, 60, "M");

        // Act
        var layout = NodeLayout.Compute(node, diagram.Theme, new ApproximateTextMeasurer());

        // Assert
        Assert.Equal(node.Ports.Count, layout.Ports.Count);
        foreach (var port in layout.Ports)
        {
            Assert.Equal(port.Port.Anchor, port.Anchor);
            Assert.Equal(NodeGeometry.PortAnchor(node.Bounds, port.Port.Direction, port.Port.Index, port.Port.Direction == PortDirection.Input ? node.Inputs.Count : node.Outputs.Count, port.Port.Label, diagram.Theme), port.Anchor);
        }
    }

    /// <summary>
    ///     Validates that port anchors do not depend on the text measurer.
    /// </summary>
    [Fact]
    public void NodeLayout_Compute_PortAnchors_AreTextIndependent()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Mixer", 30, 60, "M");

        // Act
        var approximate = NodeLayout.Compute(node, diagram.Theme, new ApproximateTextMeasurer());
        var fixedWidth = NodeLayout.Compute(node, diagram.Theme, new FixedTextMeasurer());

        // Assert
        Assert.Equal(approximate.Ports.Select(p => p.Anchor), fixedWidth.Ports.Select(p => p.Anchor));
        Assert.NotEqual(approximate.Name.Bounds.Width, fixedWidth.Name.Bounds.Width);
    }

    /// <summary>
    ///     Validates that input labels lie left of the box and output labels right of it, between box and anchor, centered on the port row.
    /// </summary>
    [Fact]
    public void NodeLayout_Compute_PortLabels_AreBesideTheTrianglesCenteredOnTheRow()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Gain", 30, 60, "G");

        // Act
        var layout = NodeLayout.Compute(node, diagram.Theme, new ApproximateTextMeasurer());

        // Assert
        var input = layout.Ports.Single(p => p.Port.Direction == PortDirection.Input);
        var output = layout.Ports.Single(p => p.Port.Direction == PortDirection.Output);
        Assert.True(input.Label.Bounds.Right <= layout.Box.Left);
        Assert.True(output.Label.Bounds.Left >= layout.Box.Right);
        Assert.Equal(input.Anchor.Y, input.Label.Bounds.Center.Y, 6);
        Assert.Equal(output.Anchor.Y, output.Label.Bounds.Center.Y, 6);
        Assert.True(input.Label.Bounds.Right <= input.Triangle.Left);
        Assert.True(output.Label.Bounds.Left >= output.Triangle.Right);
        Assert.True(input.Label.Bounds.Left >= input.Anchor.X);
        Assert.True(output.Label.Bounds.Right <= output.Anchor.X);
        Assert.Equal("in", input.Label.Text);
        Assert.Equal("out", output.Label.Text);
    }

    /// <summary>
    ///     Validates that triangles lie inside the box against their edge and hit areas surround the anchors.
    /// </summary>
    [Fact]
    public void NodeLayout_Compute_PortTrianglesAndHitAreas_SitAtTheAnchors()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Gain", 30, 60, "G");

        // Act
        var layout = NodeLayout.Compute(node, diagram.Theme, new ApproximateTextMeasurer());

        // Assert
        var input = layout.Ports.Single(p => p.Port.Direction == PortDirection.Input);
        var output = layout.Ports.Single(p => p.Port.Direction == PortDirection.Output);
        // Assert: triangles sit outside the box, an input's tip and an output's base touching the edge
        Assert.Equal(layout.Box.Left, input.Triangle.Right);
        Assert.Equal(layout.Box.Right, output.Triangle.Left);
        Assert.False(layout.Box.Contains(input.Triangle.Center));
        Assert.False(layout.Box.Contains(output.Triangle.Center));
        Assert.True(input.HitBounds.Contains(input.Anchor));
        Assert.True(output.HitBounds.Contains(output.Anchor));

        // The hit area spans triangle and label, from just inside the box edge out to the anchor
        Assert.True(input.HitBounds.Contains(input.Triangle.Center));
        Assert.True(output.HitBounds.Contains(output.Triangle.Center));
        Assert.True(input.HitBounds.Contains(input.Label.Bounds.Center));
        Assert.True(output.HitBounds.Contains(output.Label.Bounds.Center));
    }

    /// <summary>
    ///     Validates the title band, the name above the box and the summary lines below it.
    /// </summary>
    [Fact]
    public void NodeLayout_Compute_TitleNameAndSummary_AreArrangedAroundTheBox()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Producer", 30, 60, "P");

        // Act
        var layout = NodeLayout.Compute(node, diagram.Theme, new ApproximateTextMeasurer());

        // Assert: title fills the header band, name is centered above, summary lines stack below
        Assert.Equal("PRODUCER", layout.Title.Text);
        Assert.Equal(diagram.Theme.HeaderHeight, layout.Title.Bounds.Height);
        Assert.Equal(node.Bounds.Top, layout.Title.Bounds.Top);
        Assert.Equal("P", layout.Name.Text);
        Assert.True(layout.Name.Bounds.Bottom <= layout.Box.Top);
        Assert.Equal(layout.Box.Center.X, layout.Name.Bounds.Center.X, 6);
        Assert.Equal(node.Type.GetSummaryLines(node).Count, layout.SummaryLines.Count);
        Assert.NotEmpty(layout.SummaryLines);
        Assert.True(layout.SummaryLines[0].Bounds.Top >= layout.Box.Bottom);
        for (var i = 1; i < layout.SummaryLines.Count; i++)
        {
            Assert.True(layout.SummaryLines[i].Bounds.Top >= layout.SummaryLines[i - 1].Bounds.Bottom - 1e-9);
        }
    }

    /// <summary>
    ///     Validates that the icon rectangle is centered in the body below the header, and absent without an icon.
    /// </summary>
    [Fact]
    public void NodeLayout_Compute_Icon_IsCenteredInBodyOnlyWhenTypeHasIcon()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var iconic = page.AddNode("acme.Iconic", 30, 60, "I");
        var plain = page.AddNode("acme.Consumer", 300, 60, "C");

        // Act
        var iconicLayout = NodeLayout.Compute(iconic, diagram.Theme, new ApproximateTextMeasurer());
        var plainLayout = NodeLayout.Compute(plain, diagram.Theme, new ApproximateTextMeasurer());

        // Assert
        Assert.Null(plainLayout.IconBounds);
        var icon = Assert.NotNull(iconicLayout.IconBounds);
        Assert.Equal(diagram.Theme.IconBoxSize, icon.Width);
        Assert.Equal(iconicLayout.Box.Center.X, icon.Center.X, 6);
        Assert.True(icon.Top >= iconicLayout.Box.Top + diagram.Theme.HeaderHeight);
        Assert.True(icon.Bottom <= iconicLayout.Box.Bottom);
    }

    /// <summary>
    ///     Validates that the layout bounds cover the box, name, summary and port labels.
    /// </summary>
    [Fact]
    public void NodeLayout_Compute_Bounds_CoverEverythingDrawn()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Producer", 30, 60, "P");

        // Act
        var layout = NodeLayout.Compute(node, diagram.Theme, new ApproximateTextMeasurer());

        // Assert
        Assert.True(layout.Bounds.Contains(layout.Box));
        Assert.True(layout.Bounds.Contains(layout.Name.Bounds));
        Assert.All(layout.SummaryLines, line => Assert.True(layout.Bounds.Contains(line.Bounds)));
        Assert.All(layout.Ports, port => Assert.True(layout.Bounds.Contains(port.Label.Bounds)));
    }

    /// <summary>
    ///     Validates that null arguments are rejected.
    /// </summary>
    [Fact]
    public void NodeLayout_Compute_NullArguments_ThrowArgumentNullException()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Producer", 0, 0, "P");

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => NodeLayout.Compute(null!, diagram.Theme, new ApproximateTextMeasurer()));
        Assert.Throws<ArgumentNullException>(() => NodeLayout.Compute(node, null!, new ApproximateTextMeasurer()));
        Assert.Throws<ArgumentNullException>(() => NodeLayout.Compute(node, diagram.Theme, null!));
    }

    /// <summary>
    ///     Validates that annotation text wraps at word boundaries, keeps explicit line breaks and splits words wider than a line.
    /// </summary>
    [Fact]
    public void TextWrapper_Wrap_BreaksAtWordsAndSplitsLongWords()
    {
        // Arrange: every character is 10 wide, the line is 50 wide (five characters)
        var style = new TextStyle("Segoe UI", 10, Color.Black);
        var measurer = new FixedTextMeasurer();

        // Act
        var words = TextWrapper.Wrap("ab cd ef\ngh", 50, style, measurer);
        var longWord = TextWrapper.Wrap("abcdefghijk", 50, style, measurer);

        // Assert
        Assert.Equal(new[] { "ab cd", "ef", "gh" }, words);
        Assert.Equal(new[] { "abcde", "fghij", "k" }, longWord);
    }
}
