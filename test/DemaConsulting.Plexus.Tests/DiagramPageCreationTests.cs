using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the creation API of <see cref="DiagramPage"/>, <see cref="Node"/>, <see cref="Port"/> and <see cref="Annotation"/>.
/// </summary>
public class DiagramPageCreationTests
{
    /// <summary>
    ///     Validates that AddNode by key creates a node at the requested position with the type's ports.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_ByTypeKey_CreatesNodeAtPosition()
    {
        // Arrange: a diagram with one page
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");

        // Act: add a producer
        var node = page.AddNode("acme.Producer", 30, 45);

        // Assert: placed, typed, registered and findable
        Assert.Equal(new Point(30, 45), node.Position);
        Assert.Same(diagram.Types.Get("acme.Producer"), node.Type);
        Assert.Same(page, node.Page);
        Assert.Contains(node, page.Nodes);
        Assert.Contains(node, page.Elements);
        Assert.Same(node, diagram.FindElement(node.Id));
        Assert.Single(node.Outputs);
        Assert.Empty(node.Inputs);
    }

    /// <summary>
    ///     Validates that an unknown type key is rejected with an argument exception.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_UnknownTypeKey_ThrowsArgumentException()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => page.AddNode("acme.Missing", 0, 0));
    }

    /// <summary>
    ///     Validates that a node type from a different registry is rejected by the NodeType overload.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_UnregisteredNodeType_ThrowsArgumentException()
    {
        // Arrange: a type with the same key but not the registered instance
        var page = TestModel.CreateDiagram().Pages.Add("Main");
        var foreign = new NodeType("acme.Producer");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => page.AddNode(foreign, new Point(0, 0)));
    }

    /// <summary>
    ///     Validates that non-finite positions are rejected.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_NonFinitePosition_ThrowsArgumentException()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => page.AddNode("acme.Producer", double.NaN, 0));
    }

    /// <summary>
    ///     Validates that the NodeType overload works with an explicit name and id.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_ExplicitNameAndId_UsesThem()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");

        // Act: add with explicit identity (name is trimmed)
        var node = page.AddNode(diagram.Types.Get("acme.Gain"), new Point(0, 0), "  Amp  ", "fixed-id");

        // Assert
        Assert.Equal("Amp", node.Name);
        Assert.Equal("fixed-id", node.Id);
        Assert.Same(node, diagram.FindElement("fixed-id"));
    }

    /// <summary>
    ///     Validates that a duplicate element id is rejected.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_DuplicateId_ThrowsArgumentException()
    {
        // Arrange: a node with a known id
        var page = TestModel.CreateDiagram().Pages.Add("Main");
        page.AddNode("acme.Producer", 0, 0, id: "dup");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => page.AddNode("acme.Consumer", 0, 0, id: "dup"));
    }

    /// <summary>
    ///     Validates that generated ids are unique and compact hexadecimal.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_DefaultId_IsUniqueGuidWithoutDashes()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");

        // Act
        var a = page.AddNode("acme.Producer", 0, 0);
        var b = page.AddNode("acme.Producer", 0, 0);

        // Assert
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(32, a.Id.Length);
        Assert.DoesNotContain('-', a.Id);
    }

    /// <summary>
    ///     Validates auto-naming: the last key segment plus the lowest unused number.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_NoName_GeneratesUniqueNamesFromKeyLastSegment()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var other = diagram.Pages.Add("Other");

        // Act: add three producers (two on one page, one on another) and a consumer
        var a = page.AddNode("acme.Producer", 0, 0);
        var b = page.AddNode("acme.Producer", 0, 0);
        var c = other.AddNode("acme.Producer", 0, 0);
        var d = page.AddNode("acme.Consumer", 0, 0);

        // Assert: numbering is diagram-wide per base name
        Assert.Equal("Producer1", a.Name);
        Assert.Equal("Producer2", b.Name);
        Assert.Equal("Producer3", c.Name);
        Assert.Equal("Consumer1", d.Name);
    }

    /// <summary>
    ///     Validates that auto-naming reuses the lowest free number after a node is removed.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_NoName_ReusesFreedNumber()
    {
        // Arrange: Producer1 and Producer2, then remove Producer1
        var page = TestModel.CreateDiagram().Pages.Add("Main");
        var first = page.AddNode("acme.Producer", 0, 0);
        page.AddNode("acme.Producer", 0, 0);
        page.Remove(first);

        // Act
        var next = page.AddNode("acme.Producer", 0, 0);

        // Assert
        Assert.Equal("Producer1", next.Name);
    }

    /// <summary>
    ///     Validates that a duplicate or empty explicit name is rejected when uniqueness is required.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_DuplicateOrEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");
        page.AddNode("acme.Producer", 0, 0, "Same");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => page.AddNode("acme.Producer", 0, 0, "Same"));
        Assert.Throws<ArgumentException>(() => page.AddNode("acme.Producer", 0, 0, "   "));
    }

    /// <summary>
    ///     Validates that duplicate names are accepted when uniqueness is switched off.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_DuplicateNameWithUniquenessOff_Succeeds()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        diagram.RequireUniqueNodeNames = false;
        var page = diagram.Pages.Add("Main");
        page.AddNode("acme.Producer", 0, 0, "Same");

        // Act
        var second = page.AddNode("acme.Producer", 0, 0, "Same");

        // Assert
        Assert.Equal("Same", second.Name);
    }

    /// <summary>
    ///     Validates that a node's default size is the theme default for few ports.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_FewPorts_UsesThemeDefaultSize()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");

        // Act
        var node = page.AddNode("acme.Gain", 0, 0);

        // Assert: 90 x 90 from the classic theme
        Assert.Equal(new Size(90, 90), node.Bounds.Size);
    }

    /// <summary>
    ///     Validates that a node with many ports grows to fit them.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_ManyPorts_GrowsToFitPorts()
    {
        // Arrange: a type with five inputs
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Wide")
        {
            Inputs =
            {
                new PortDefinition("a", "f32"),
                new PortDefinition("b", "f32"),
                new PortDefinition("c", "f32"),
                new PortDefinition("d", "f32"),
                new PortDefinition("e", "f32"),
            },
        });
        var page = new Diagram(types).Pages.Add("Main");

        // Act: five centered inputs need twice the 30 header clearance plus 4 * pitch 15 = 120
        var node = page.AddNode("acme.Wide", 0, 0);

        // Assert
        Assert.Equal(120, node.Bounds.Height);
        Assert.Equal(90, node.Bounds.Width);
    }

    /// <summary>
    ///     Validates that a size declared on the node type wins over the computed default.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_TypeWithDefaultSize_UsesDeclaredSize()
    {
        // Arrange: a type with an explicit size
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Big") { DefaultSize = new Size(200, 120) });
        var page = new Diagram(types).Pages.Add("Main");

        // Act
        var node = page.AddNode("acme.Big", 5, 6);

        // Assert
        Assert.Equal(new Rect(5, 6, 200, 120), node.Bounds);
    }

    /// <summary>
    ///     Validates that a declared size shorter than the ports need is kept when the type has no port provider.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_DeclaredSizeWithoutProvider_KeepsDeclaredHeight()
    {
        // Arrange: a type whose declared 40 px height is shorter than its two inputs need
        var types = TestModel.CreateTypes();
        var type = new NodeType("acme.Short")
        {
            DefaultSize = new Size(100, 40),
            Inputs = { new PortDefinition("a", "f32"), new PortDefinition("b", "f32") },
        };
        types.Add(type);
        var diagram = new Diagram(types);
        var page = diagram.Pages.Add("Main");

        // Act
        var node = page.AddNode("acme.Short", 5, 6);

        // Assert: the declared size is authoritative, as the palette ghost shows it
        Assert.Equal(new Rect(5, 6, 100, 40), node.Bounds);
        Assert.Equal(NodeGeometry.DefaultNodeSize(type, diagram.Theme), node.Bounds.Size);
    }

    /// <summary>
    ///     Validates that a port provider building an invalid port set makes AddNode throw without adding anything.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_ProviderBuildsInvalidPortSet_ThrowsAndAddsNothing()
    {
        // Arrange: a provider that returns two inputs with the same name
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Dup")
        {
            PortProvider = _ => new PortSet([new PortDefinition("a", "f32"), new PortDefinition("a", "f32")], []),
        });
        var diagram = new Diagram(types);
        var page = diagram.Pages.Add("Main");
        var entriesBefore = diagram.History.UndoCount;

        // Act / Assert: the error reaches the caller, and no node or history entry is left behind
        Assert.Throws<ArgumentException>(() => page.AddNode("acme.Dup", 0, 0));
        Assert.Empty(page.Nodes);
        Assert.Equal(entriesBefore, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that a new node grows to fit the ports its type's port provider returns, even over a declared size.
    /// </summary>
    [Fact]
    public void DiagramPage_AddNode_ProviderNeedsMoreHeight_GrowsNewNode()
    {
        // Arrange: the router declares 120 x 60 but its default payload yields two outputs
        var page = TestModel.CreateDynamicDiagram().Pages.Add("Main");

        // Act
        var router = page.AddNode("acme.Router", 5, 6);

        // Assert: declared width, height of two port rows
        Assert.Equal(new Rect(5, 6, 120, NodeGeometry.RequiredHeight(2, DiagramTheme.Classic)), router.Bounds);
        Assert.Equal(["x", "y"], router.Outputs.Select(p => p.Name));
    }

    /// <summary>
    ///     Validates that a port a port provider took off its node can no longer be connected.
    /// </summary>
    [Fact]
    public void DiagramPage_CanConnect_PortRemovedByProvider_IsDenied()
    {
        // Arrange: hold a reference to in1, then remove it by lowering the channel count
        var page = TestModel.CreateDynamicDiagram().Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0);
        var bus = page.AddNode("acme.Bus", 300, 0);
        var in1 = bus.Input("in1");
        bus.Properties["Channels"] = 1;

        // Act
        var check = page.CanConnect(producer.Output("value"), in1);

        // Assert: refused with a reason, and Connect throws
        Assert.False(check.IsAllowed);
        Assert.Contains("no longer exists", check.Reason, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => page.Connect(producer.Output("value"), in1));
    }

    /// <summary>
    ///     Validates port lookup by name and the shape of the cached port objects.
    /// </summary>
    [Fact]
    public void Node_Port_ByName_ReturnsCachedPortWithDefinitionData()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");
        var node = page.AddNode("acme.Gain", 15, 30);

        // Act
        var input = node.Port("in");
        var output = node.Port("out");

        // Assert: same instance every time, direction and anchors follow the geometry rules
        Assert.Same(input, node.Port("in"));
        Assert.Equal(PortDirection.Input, input.Direction);
        Assert.Equal(PortDirection.Output, output.Direction);
        Assert.Equal("f32", input.TypeKey);
        Assert.Equal("in", input.Label);
        Assert.Same(node, input.Node);
        Assert.Equal(new[] { input, output }, node.Ports);
        Assert.Equal(new Point(15 - NodeGeometry.PortReach("in", DiagramTheme.Classic), 30 + 45), input.Anchor);
        Assert.Equal(new Point(15 + 90 + NodeGeometry.PortReach("out", DiagramTheme.Classic), 30 + 45), output.Anchor);
        Assert.Throws<ArgumentException>(() => node.Port("nope"));
    }

    /// <summary>
    ///     Validates that a custom port label is exposed on the port.
    /// </summary>
    [Fact]
    public void PortDefinition_Label_CustomLabelIsExposedOnPort()
    {
        // Arrange: a type with a labeled port
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("t.Labeled") { Inputs = { new PortDefinition("x", "f32") { Label = "X input" } } });
        var node = new Diagram(types).Pages.Add("P").AddNode("t.Labeled", 0, 0);

        // Assert
        Assert.Equal("X input", node.Port("x").Label);
        Assert.Equal("x", node.Port("x").Name);
    }

    /// <summary>
    ///     Validates that annotations are created with the given text and bounds and can be edited.
    /// </summary>
    [Fact]
    public void DiagramPage_AddAnnotation_CreatesAnnotationWithTextAndBounds()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");

        // Act
        var note = page.AddAnnotation("Hello", new Rect(10, 20, 100, 50));

        // Assert
        Assert.Equal("Hello", note.Text);
        Assert.Equal(new Rect(10, 20, 100, 50), note.Bounds);
        Assert.Null(note.Fill);
        Assert.Contains(note, page.Annotations);
        Assert.Same(note, diagram.FindElement(note.Id));
    }

    /// <summary>
    ///     Validates annotation argument validation.
    /// </summary>
    [Fact]
    public void DiagramPage_AddAnnotation_InvalidArguments_Throw()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => page.AddAnnotation(null!, new Rect(0, 0, 1, 1)));
        Assert.Throws<ArgumentException>(() => page.AddAnnotation("x", new Rect(0, 0, -1, 1)));
    }

    /// <summary>
    ///     Validates that FindNode searches across pages and returns null for unknown names.
    /// </summary>
    [Fact]
    public void Diagram_FindNode_SearchesAllPages()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        diagram.Pages.Add("A").AddNode("acme.Producer", 0, 0, "One");
        var onB = diagram.Pages.Add("B").AddNode("acme.Producer", 0, 0, "Two");

        // Assert
        Assert.Same(onB, diagram.FindNode("Two"));
        Assert.Null(diagram.FindNode("Three"));
        Assert.Null(diagram.FindElement("missing"));
    }

    /// <summary>
    ///     Validates that the theme drives the default node size and anchors.
    /// </summary>
    [Fact]
    public void Diagram_Theme_CustomMetrics_AffectNewNodes()
    {
        // Arrange: a theme with a bigger default node
        var diagram = TestModel.CreateDiagram();
        diagram.Theme = new DiagramTheme { DefaultNodeWidth = 150, DefaultNodeHeight = 120 };
        var page = diagram.Pages.Add("Main");

        // Act
        var node = page.AddNode("acme.Gain", 0, 0);

        // Assert
        Assert.Equal(new Size(150, 120), node.Bounds.Size);
        Assert.Throws<ArgumentNullException>(() => diagram.Theme = null!);
    }

    /// <summary>
    ///     Validates that adding a node, annotation and wire are each exactly one undoable entry that round-trips.
    /// </summary>
    [Fact]
    public void DiagramPage_Creation_AddNodeAnnotationAndWire_UndoRedoRoundTrip()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        Node? a = null;
        Node? b = null;

        // Act / Assert: each creation step round-trips on its own
        TestModel.AssertRoundTrip(diagram, () => a = page.AddNode("acme.Producer", 0, 0));
        TestModel.AssertRoundTrip(diagram, () => b = page.AddNode("acme.Consumer", 300, 0));
        TestModel.AssertRoundTrip(diagram, () => page.AddAnnotation("note", new Rect(0, 200, 100, 40)));
        TestModel.AssertRoundTrip(diagram, () => a!.Port("value").ConnectTo(b!.Port("in")));
    }
}
