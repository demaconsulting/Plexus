using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Subsystem tests for Types: the node type registry, port types and icons working together through the public API.
/// </summary>
public class TypesTests
{
    private static readonly string[] ValidKeys = ["acme.Valid"];

    private static readonly string[] BlockInputNames = ["a", "b"];

    private static readonly string[] BlockInputTypeKeys = ["f32", "i32"];

    private static readonly string[] BlockOutputNames = ["result"];
    /// <summary>
    ///     An SVG document used as an icon.
    /// </summary>
    private const string Svg = "<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'/>";

    /// <summary>
    ///     Validates that a host can declare port types and a node type with ports, properties and an icon, and read them back.
    /// </summary>
    [Fact]
    public void Types_Declaration_PortTypesNodeTypesAndIcon_AreRetrievable()
    {
        // Arrange: declare a port type and a node type that uses it, with an icon
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType("f32", Color.Parse("#FF8C00")));
        var icon = IconSource.FromSvg(Svg);
        var gain = new NodeType("acme.math.Gain")
        {
            DisplayName = "Gain stage",
            Icon = icon,
            Inputs = { new PortDefinition("in", "f32") { Label = "Input" } },
            Outputs = { new PortDefinition("out", "f32") },
            Properties = { new PropertyDefinition("Factor", 1.0) },
        };

        // Act
        types.Add(gain);
        var found = types.Get("acme.math.Gain");

        // Assert: the type, its derived title, ports, property, icon and port color are all reachable from the registry
        Assert.Multiple(
            () => Assert.Same(gain, found),
            () => Assert.Equal("Gain", found.Title),
            () => Assert.Equal("Gain stage", found.DisplayName),
            () => Assert.Same(icon, found.Icon),
            () => Assert.Equal("Input", found.Inputs[0].Label),
            () => Assert.Equal("out", found.Outputs[0].Label),
            () => Assert.NotNull(found.FindProperty("Factor")),
            () => Assert.Equal(Color.Parse("#FF8C00"), types.PortTypes.Get(found.Inputs[0].TypeKey).Color),
            () => Assert.Equal(new[] { gain }, types));
    }

    /// <summary>
    ///     Validates that duplicate keys, port names and property names are rejected and leave the registry unchanged.
    /// </summary>
    [Fact]
    public void Types_Validation_DuplicateKeyAndMembers_AreRejectedLeavingRegistryUnchanged()
    {
        // Arrange: one valid registered type and port type
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType("f32", Color.Red));
        types.Add(new NodeType("acme.Valid"));

        // Act / Assert: each invalid declaration is rejected
        Assert.Throws<ArgumentException>(() => types.Add(new NodeType("acme.Valid")));
        Assert.Throws<ArgumentException>(() => types.Add(new NodeType("acme.DupIn")
        {
            Inputs = { new PortDefinition("x", "f32"), new PortDefinition("x", "f32") },
        }));
        Assert.Throws<ArgumentException>(() => types.Add(new NodeType("acme.DupOut")
        {
            Outputs = { new PortDefinition("x", "f32"), new PortDefinition("x", "f32") },
        }));
        Assert.Throws<ArgumentException>(() => types.Add(new NodeType("acme.DupProp")
        {
            Properties = { new PropertyDefinition("p", 1), new PropertyDefinition("p", 2) },
        }));
        Assert.Throws<ArgumentException>(() => types.PortTypes.Add(new PortType("f32", Color.Blue)));

        // Assert: nothing but the original declarations is registered
        Assert.Equal(ValidKeys, types.Select(t => t.Key));
        Assert.Equal(Color.Red, types.PortTypes.Get("f32").Color);
        Assert.Equal(1, types.PortTypes.Count);
    }

    /// <summary>
    ///     Validates that a registered node type can no longer change its ports or properties, even when used by a live node.
    /// </summary>
    [Fact]
    public void Types_Validation_RegisteredType_CannotBeModified()
    {
        // Arrange: register a type and create a node from it
        var types = new NodeTypeRegistry();
        var type = new NodeType("acme.Locked") { Inputs = { new PortDefinition("in", "f32") } };
        types.Add(type);
        var node = new Diagram(types).Pages.Add("Main").AddNode("acme.Locked", 0, 0);

        // Act / Assert: every list rejects modification and the node keeps its ports
        Assert.Throws<NotSupportedException>(() => type.Inputs.Add(new PortDefinition("extra", "f32")));
        Assert.Throws<NotSupportedException>(() => type.Outputs.Add(new PortDefinition("extra", "f32")));
        Assert.Throws<NotSupportedException>(() => type.Properties.Add(new PropertyDefinition("p", 1)));
        Assert.Throws<NotSupportedException>(() => type.Inputs.Clear());
        Assert.Single(node.Ports);
    }

    /// <summary>
    ///     Validates that node types sharing the same SVG content share an equal icon, and that the icon survives registration.
    /// </summary>
    [Fact]
    public void Types_Icons_SameSvgOnTwoNodeTypes_SharesKeyAndEquality()
    {
        // Arrange: two icons created independently from identical text, and one from different text
        var types = new NodeTypeRegistry
        {
            new NodeType("acme.A") { Icon = IconSource.FromSvg(Svg) },
            new NodeType("acme.B") { Icon = IconSource.FromSvg(Svg) },
            new NodeType("acme.C") { Icon = IconSource.FromSvg("<svg/>") },
            new NodeType("acme.D"),
        };

        // Act
        var a = types.Get("acme.A").Icon;
        var b = types.Get("acme.B").Icon;
        var c = types.Get("acme.C").Icon;

        // Assert: equal content gives equal icons and keys; different content differs; no icon stays null
        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.Equal(a!.Key, b!.Key);
        Assert.NotEqual(a, c);
        Assert.Equal(Svg, a.SvgText);
        Assert.Null(types.Get("acme.D").Icon);
    }

    /// <summary>
    ///     Validates that a node added from a type starts with that type's ports and declared default size.
    /// </summary>
    [Fact]
    public void Types_NodeDefaults_AddNode_UsesDeclaredPortsAndSize()
    {
        // Arrange: a type with ports in each direction and an explicit size
        var types = new NodeTypeRegistry
        {
            new NodeType("acme.Block")
            {
                DefaultSize = new Size(200, 120),
                Inputs = { new PortDefinition("a", "f32"), new PortDefinition("b", "i32") { Label = "Second" } },
                Outputs = { new PortDefinition("result", "f32") },
            },
        };
        var page = new Diagram(types).Pages.Add("Main");

        // Act
        var node = page.AddNode("acme.Block", 10, 20);

        // Assert: size comes from the type, ports are created in declaration order with their types and labels
        Assert.Equal(new Rect(10, 20, 200, 120), node.Bounds);
        Assert.Equal(BlockInputNames, node.Inputs.Select(p => p.Name));
        Assert.Equal(BlockInputTypeKeys, node.Inputs.Select(p => p.TypeKey));
        Assert.Equal("Second", node.Inputs[1].Label);
        Assert.Equal(BlockOutputNames, node.Outputs.Select(p => p.Name));
        Assert.All(node.Inputs, p => Assert.Equal(PortDirection.Input, p.Direction));
        Assert.All(node.Outputs, p => Assert.Equal(PortDirection.Output, p.Direction));
    }

    /// <summary>
    ///     Validates that only currently registered node types can be used to create nodes.
    /// </summary>
    [Fact]
    public void Types_Availability_RemoveAndReRegister_ControlsNodeCreation()
    {
        // Arrange: a diagram whose registry holds one type
        var types = new NodeTypeRegistry
        {
            new NodeType("acme.Temp"),
        };
        var page = new Diagram(types).Pages.Add("Main");
        Assert.Equal("Temp1", page.AddNode("acme.Temp", 0, 0).Name);

        // Act: remove the type
        var removed = types.Remove("acme.Temp");

        // Assert: it can no longer be instantiated, nor found by lookup
        Assert.True(removed);
        Assert.Throws<ArgumentException>(() => page.AddNode("acme.Temp", 0, 0));
        Assert.False(types.TryGet("acme.Temp", out _));

        // Act: register a type with the same key again
        types.Add(new NodeType("acme.Temp"));

        // Assert: nodes can be created again
        Assert.Equal("Temp2", page.AddNode("acme.Temp", 0, 0).Name);
    }

    /// <summary>
    ///     Validates that a port provider declared on a node type supplies each node's ports while the declared ports stay the defaults.
    /// </summary>
    [Fact]
    public void Types_PortProvider_DeclaredOnType_SuppliesPerNodePorts()
    {
        // Arrange
        var types = TestModel.CreateDynamicTypes();
        var page = new Diagram(types).Pages.Add("Main");
        var a = page.AddNode("acme.Bus", 0, 0);
        var b = page.AddNode("acme.Bus", 0, 200);

        // Act: configure one node only
        a.Properties["Channels"] = 5;

        // Assert: each node has its own ports; the type's declared defaults are unchanged
        Assert.Equal(5, a.Inputs.Count);
        Assert.Equal(2, b.Inputs.Count);
        Assert.Equal(["in0", "in1"], types.Get("acme.Bus").Inputs.Select(p => p.Name));
        Assert.NotNull(types.Get("acme.Bus").PortProvider);
    }

    /// <summary>
    ///     Validates that port, node type and port type descriptions are exposed for tooltips.
    /// </summary>
    [Fact]
    public void Types_Descriptions_PortNodeAndPortType_AreExposed()
    {
        // Arrange
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType("bool", Color.Red) { DisplayName = "Boolean" });
        types.Add(new NodeType("t.Gate")
        {
            Description = "A logic gate.",
            Inputs = { new PortDefinition("a", "bool") { Description = "First operand." } },
        });
        var node = new Diagram(types).Pages.Add("Main").AddNode("t.Gate", 0, 0);

        // Act
        var port = node.Input("a");

        // Assert
        Assert.Equal("First operand.", port.Description);
        Assert.Equal("A logic gate.", node.Type.GetToolTip(node));
        Assert.Equal("Boolean", types.PortTypes.Get(port.TypeKey).DisplayName);
    }

    /// <summary>
    ///     Validates that a port provider computes both the inputs and the outputs of each node from its own properties.
    /// </summary>
    [Fact]
    public void Types_PortProvider_InputsAndOutputs_ComputedPerNode()
    {
        // Arrange: a splitter whose input and output counts are both properties
        var types = new NodeTypeRegistry
        {
            new NodeType("t.Splitter")
            {
                Inputs = { new PortDefinition("i0", "number") },
                Outputs = { new PortDefinition("o0", "number") },
                Properties = { new PropertyDefinition("Ins", 1), new PropertyDefinition("Outs", 1) },
                PortProvider = node => new PortSet(
                    Enumerable.Range(0, node.Properties.Get<int>("Ins")).Select(i => new PortDefinition($"i{i}", "number")),
                    Enumerable.Range(0, node.Properties.Get<int>("Outs")).Select(i => new PortDefinition($"o{i}", "number"))),
            },
        };
        var page = new Diagram(types).Pages.Add("Main");
        var a = page.AddNode("t.Splitter", 0, 0);
        var b = page.AddNode("t.Splitter", 0, 200);

        // Act: configure the two nodes differently in both directions
        a.Properties["Ins"] = 3;
        b.Properties["Outs"] = 4;

        // Assert: each node has its own inputs and outputs; the declared defaults are unchanged
        Assert.Equal(["i0", "i1", "i2"], a.Inputs.Select(p => p.Name));
        Assert.Equal(["o0"], a.Outputs.Select(p => p.Name));
        Assert.Equal(["i0"], b.Inputs.Select(p => p.Name));
        Assert.Equal(["o0", "o1", "o2", "o3"], b.Outputs.Select(p => p.Name));
        Assert.Equal(["i0"], types.Get("t.Splitter").Inputs.Select(p => p.Name));
        Assert.Equal(["o0"], types.Get("t.Splitter").Outputs.Select(p => p.Name));
    }

    /// <summary>
    ///     Validates that a port provider returning null makes node creation fail with an error naming the type.
    /// </summary>
    [Fact]
    public void Types_PortProvider_ReturnsNull_AddNodeFails()
    {
        // Arrange
        var types = new NodeTypeRegistry
        {
            new NodeType("t.Broken") { PortProvider = _ => null! },
        };
        var page = new Diagram(types).Pages.Add("Main");

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => page.AddNode("t.Broken", 0, 0));

        // Assert
        Assert.Contains("t.Broken", ex.Message, StringComparison.Ordinal);
        Assert.Empty(page.Nodes);
    }

    /// <summary>
    ///     Validates that a node type's tooltip function produces text from the node's current properties and payload.
    /// </summary>
    [Fact]
    public void Types_Descriptions_ToolTipFunction_FollowsNodeState()
    {
        // Arrange: a tooltip built from a property and the payload
        var types = new NodeTypeRegistry
        {
            new NodeType("t.Gauge")
            {
                Description = "Shows a value.",
                DataType = typeof(string),
                CreateData = () => "ok",
                Properties = { new PropertyDefinition("Value", 0.0) },
                ToolTip = node => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Value = {node.Properties.Get<double>("Value")} ({node.Data})"),
            },
        };
        var node = new Diagram(types).Pages.Add("Main").AddNode("t.Gauge", 0, 0);
        var initial = node.Type.GetToolTip(node);

        // Act: change both inputs of the function
        node.Properties["Value"] = 2.5;
        node.Data = "stale";
        var changed = node.Type.GetToolTip(node);

        // Assert: each call reflects the state at that time
        Assert.Equal("Value = 0 (ok)", initial);
        Assert.Equal("Value = 2.5 (stale)", changed);
    }
}
