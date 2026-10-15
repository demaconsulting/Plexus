using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the node type declarations: <see cref="NodeTypeRegistry"/>, <see cref="NodeType"/>,
///     <see cref="PortDefinition"/> and <see cref="PortTypeRegistry"/>. Further evidence for these units lives in
///     <c>TypesAndGeometryTests.cs</c>, <c>PortPathTests.cs</c>, <c>SharedPortNameTests.cs</c> and <c>PropertyBagTests.cs</c>.
/// </summary>
public class NodeTypeRegistryTests
{
    /// <summary>
    ///     Validates that enumeration follows registration order and that removal keeps the remaining order.
    /// </summary>
    [Fact]
    public void NodeTypeRegistry_Enumerate_ReturnsRegistrationOrder()
    {
        // Arrange: three types registered in a non-alphabetical order
        var registry = new NodeTypeRegistry();
        registry.Add(new NodeType("acme.Charlie"));
        registry.Add(new NodeType("acme.Alpha"));
        registry.Add(new NodeType("acme.Bravo"));

        // Act
        var all = registry.Select(t => t.Key).ToArray();
        registry.Remove("acme.Alpha");
        var afterRemoval = registry.Select(t => t.Key).ToArray();

        // Assert
        Assert.Equal(new[] { "acme.Charlie", "acme.Alpha", "acme.Bravo" }, all);
        Assert.Equal(new[] { "acme.Charlie", "acme.Bravo" }, afterRemoval);
    }

    /// <summary>
    ///     Validates that registering a duplicate key is rejected and the first type stays registered.
    /// </summary>
    [Fact]
    public void NodeTypeRegistry_Add_DuplicateKey_KeepsOriginalType()
    {
        // Arrange
        var registry = new NodeTypeRegistry();
        var original = new NodeType("acme.Thing") { Title = "ORIGINAL" };
        registry.Add(original);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => registry.Add(new NodeType("acme.Thing") { Title = "REPLACEMENT" }));

        // Assert: the message names the key and the original registration is untouched
        Assert.Contains("acme.Thing", ex.Message, StringComparison.Ordinal);
        Assert.Same(original, registry.Get("acme.Thing"));
        Assert.Equal(1, registry.Count);
    }

    /// <summary>
    ///     Validates that a rejected declaration is not frozen, so the host can correct and register it.
    /// </summary>
    [Fact]
    public void NodeTypeRegistry_Add_RejectedDeclaration_CanBeCorrectedAndRegistered()
    {
        // Arrange: a type with a duplicated output name
        var registry = new NodeTypeRegistry();
        var type = new NodeType("acme.Fixable") { Outputs = { new PortDefinition("x", "f32"), new PortDefinition("x", "f32") } };
        Assert.Throws<ArgumentException>(() => registry.Add(type));

        // Act: remove the duplicate and register again
        type.Outputs.RemoveAt(1);
        registry.Add(type);

        // Assert
        Assert.Same(type, registry.Get("acme.Fixable"));
    }

    /// <summary>
    ///     Validates that blank node type keys are rejected.
    /// </summary>
    /// <param name="key">The blank key.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NodeType_Constructor_BlankKey_ThrowsArgumentException(string key)
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => new NodeType(key));
    }

    /// <summary>
    ///     Validates that declared ports keep their declaration order and per-direction grouping.
    /// </summary>
    [Fact]
    public void NodeType_Ports_PreserveDeclarationOrderPerDirection()
    {
        // Arrange: inputs and outputs declared interleaved
        var type = new NodeType("acme.Ordered")
        {
            Inputs = { new PortDefinition("c", "f32"), new PortDefinition("a", "i32") },
            Outputs = { new PortDefinition("z", "f32"), new PortDefinition("y", "f32") },
        };

        // Act
        new NodeTypeRegistry().Add(type);

        // Assert: order is exactly as declared, with the type key kept
        Assert.Equal(new[] { "c", "a" }, type.Inputs.Select(p => p.Name));
        Assert.Equal(new[] { "f32", "i32" }, type.Inputs.Select(p => p.TypeKey));
        Assert.Equal(new[] { "z", "y" }, type.Outputs.Select(p => p.Name));
    }

    /// <summary>
    ///     Validates that a port label defaults to its name unless an explicit label is declared.
    /// </summary>
    [Fact]
    public void PortDefinition_Label_DefaultsToNameUnlessSet()
    {
        // Act
        var plain = new PortDefinition("in", "f32");
        var labeled = new PortDefinition("in", "f32") { Label = "Input signal" };

        // Assert
        Assert.Equal("in", plain.Label);
        Assert.Equal("in", plain.Name);
        Assert.Equal("Input signal", labeled.Label);
        Assert.Equal("in", labeled.Name);
    }

    /// <summary>
    ///     Validates that a port definition needs a non-blank name and type key.
    /// </summary>
    /// <param name="name">The port name.</param>
    /// <param name="typeKey">The port type key.</param>
    [Theory]
    [InlineData("", "f32")]
    [InlineData(" ", "f32")]
    [InlineData("in", "")]
    [InlineData("in", "  ")]
    public void PortDefinition_Constructor_BlankNameOrTypeKey_ThrowsArgumentException(string name, string typeKey)
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => new PortDefinition(name, typeKey));
    }

    /// <summary>
    ///     Validates that presentation hints are unspecified until the host declares them.
    /// </summary>
    [Fact]
    public void NodeType_Presentation_NotDeclared_IsUnspecified()
    {
        // Act
        var type = new NodeType("acme.Plain");

        // Assert: no size, not resizable, no icon, no custom summary or visual or payload handling
        Assert.Multiple(
            () => Assert.Null(type.DefaultSize),
            () => Assert.False(type.IsResizable),
            () => Assert.Null(type.Icon),
            () => Assert.Null(type.Summary),
            () => Assert.Null(type.Visual),
            () => Assert.Null(type.DataType),
            () => Assert.Null(type.DataSerializer),
            () => Assert.Empty(type.Inputs),
            () => Assert.Empty(type.Outputs),
            () => Assert.Empty(type.Properties));
    }

    /// <summary>
    ///     Validates that declared size, resizable flag, icon and payload type are exposed unchanged.
    /// </summary>
    [Fact]
    public void NodeType_Presentation_Declared_IsExposed()
    {
        // Arrange
        var icon = IconSource.FromSvg("<svg/>");

        // Act
        var type = new NodeType("acme.Rich")
        {
            DefaultSize = new Size(200, 120),
            IsResizable = true,
            Icon = icon,
            DataType = typeof(string),
        };

        // Assert
        Assert.Equal(new Size(200, 120), type.DefaultSize);
        Assert.True(type.IsResizable);
        Assert.Same(icon, type.Icon);
        Assert.Equal(typeof(string), type.DataType);
    }

    /// <summary>
    ///     Validates that registering a duplicate port type key is rejected and keeps the first definition.
    /// </summary>
    [Fact]
    public void PortTypeRegistry_Add_DuplicateKey_KeepsFirstType()
    {
        // Arrange
        var registry = new PortTypeRegistry();
        var first = new PortType("f32", Color.Red);
        registry.Add(first);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => registry.Add(new PortType("f32", Color.Blue)));

        // Assert
        Assert.Contains("f32", ex.Message, StringComparison.Ordinal);
        Assert.Same(first, registry.Get("f32"));
        Assert.Equal(1, registry.Count);
    }

    /// <summary>
    ///     Validates that an unregistered port type resolves to a neutral gray without being registered.
    /// </summary>
    [Fact]
    public void PortTypeRegistry_Get_UnknownKey_ReturnsNeutralGrayWithoutRegistering()
    {
        // Arrange
        var registry = new PortTypeRegistry();

        // Act
        var unknown = registry.Get("mystery");

        // Assert
        Assert.Equal("mystery", unknown.Key);
        Assert.Equal(new Color(0x9A, 0x9A, 0x9A), unknown.Color);
        Assert.Equal(0, registry.Count);
        Assert.Throws<ArgumentException>(() => registry.Get(string.Empty));
    }

    /// <summary>
    ///     Validates that a port set rejects null sequences, null entries and duplicate names within a direction.
    /// </summary>
    [Fact]
    public void PortSet_Constructor_DuplicateOrNull_Throws()
    {
        // Arrange
        var a = new PortDefinition("a", "f32");

        // Act / Assert: invalid sets throw; the same name on both sides is fine
        Assert.Throws<ArgumentNullException>(() => new PortSet(null!, []));
        Assert.Throws<ArgumentNullException>(() => new PortSet([], null!));
        Assert.Throws<ArgumentException>(() => new PortSet([a, null!], []));
        Assert.Throws<ArgumentException>(() => new PortSet([a, new PortDefinition("a", "i32")], []));
        var set = new PortSet([a], [new PortDefinition("a", "f32")]);
        Assert.Same(a, Assert.Single(set.Inputs));
        Assert.Single(set.Outputs);
    }

    /// <summary>
    ///     Validates that a port set copies its input sequences.
    /// </summary>
    [Fact]
    public void PortSet_Constructor_CopiesSequences()
    {
        // Arrange
        var inputs = new List<PortDefinition> { new("a", "f32") };

        // Act
        var set = new PortSet(inputs, []);
        inputs.Add(new PortDefinition("b", "f32"));

        // Assert
        Assert.Single(set.Inputs);
    }

    /// <summary>
    ///     Validates that a port set's lists cannot be changed by casting them to a writable list.
    /// </summary>
    [Fact]
    public void PortSet_InputsAndOutputs_CastToList_AreReadOnly()
    {
        // Arrange
        var set = new PortSet([new PortDefinition("a", "f32")], [new PortDefinition("o", "f32")]);

        // Act / Assert: writes through a cast are refused and the declarations are unchanged
        Assert.Throws<NotSupportedException>(() => ((IList<PortDefinition>)set.Inputs)[0] = new PortDefinition("x", "f32"));
        Assert.Throws<NotSupportedException>(() => ((IList<PortDefinition>)set.Outputs)[0] = new PortDefinition("x", "f32"));
        Assert.Equal("a", set.Inputs[0].Name);
        Assert.Equal("o", set.Outputs[0].Name);
    }

    /// <summary>
    ///     Validates that a node's tooltip text comes from the tooltip function first, then the description.
    /// </summary>
    [Fact]
    public void NodeType_GetToolTip_FunctionWinsOverDescription()
    {
        // Arrange
        var types = new NodeTypeRegistry();
        var described = new NodeType("t.Described") { Description = "Adds numbers." };
        var live = new NodeType("t.Live") { Description = "unused", ToolTip = n => $"live {n.Name}" };
        var plain = new NodeType("t.Plain");
        types.Add(described);
        types.Add(live);
        types.Add(plain);
        var page = new Diagram(types).Pages.Add("Main");

        // Act / Assert
        Assert.Equal("Adds numbers.", described.GetToolTip(page.AddNode("t.Described", 0, 0)));
        Assert.Equal("live Live1", live.GetToolTip(page.AddNode("t.Live", 0, 0)));
        Assert.Null(plain.GetToolTip(page.AddNode("t.Plain", 0, 0)));
        Assert.Throws<ArgumentNullException>(() => plain.GetToolTip(null!));
    }

    /// <summary>
    ///     Validates that a port definition's description is exposed on the port.
    /// </summary>
    [Fact]
    public void PortDefinition_Description_IsExposedOnPort()
    {
        // Arrange
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("t.Doc")
        {
            Inputs = { new PortDefinition("x", "f32") { Description = "The x value." } },
            Outputs = { new PortDefinition("y", "f32") },
        });

        // Act
        var node = new Diagram(types).Pages.Add("P").AddNode("t.Doc", 0, 0);

        // Assert
        Assert.Equal("The x value.", node.Input("x").Description);
        Assert.Null(node.Output("y").Description);
    }

    /// <summary>
    ///     Validates that a port type's display name defaults to its key.
    /// </summary>
    [Fact]
    public void PortType_DisplayName_DefaultsToKey()
    {
        // Arrange / Act
        var plain = new PortType("f32", Color.Red);
        var named = new PortType("bool", Color.Red) { DisplayName = "Boolean" };
        var empty = new PortType("i32", Color.Red) { DisplayName = string.Empty };

        // Assert
        Assert.Equal("f32", plain.DisplayName);
        Assert.Equal("Boolean", named.DisplayName);
        Assert.Equal("i32", empty.DisplayName);
    }

    /// <summary>
    ///     Validates that a declared port provider is exposed, supplies a new node's ports, and leaves the declared default ports unchanged.
    /// </summary>
    [Fact]
    public void NodeType_PortProvider_Declared_SuppliesPortsAndKeepsDefaults()
    {
        // Arrange
        var types = new NodeTypeRegistry();
        PortSet Provider(Node node) => new([new PortDefinition("x", "f32"), new PortDefinition("y", "f32")], []);
        var type = new NodeType("t.Dynamic") { Inputs = { new PortDefinition("x", "f32") }, PortProvider = Provider };
        types.Add(type);

        // Act
        var node = new Diagram(types).Pages.Add("P").AddNode("t.Dynamic", 0, 0);

        // Assert
        Assert.NotNull(type.PortProvider);
        Assert.Equal(["x", "y"], node.Inputs.Select(p => p.Name));
        Assert.Equal(["x"], type.Inputs.Select(p => p.Name));
        Assert.Null(new NodeType("t.Static").PortProvider);
    }

    /// <summary>
    ///     Validates that a port provider that returns no ports makes adding a node fail with the type named.
    /// </summary>
    [Fact]
    public void NodeType_PortProvider_ReturnsNull_ThrowsInvalidOperation()
    {
        // Arrange
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("t.Broken") { PortProvider = _ => null! });
        var page = new Diagram(types).Pages.Add("P");

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => page.AddNode("t.Broken", 0, 0));

        // Assert: nothing was added
        Assert.Contains("t.Broken", ex.Message, StringComparison.Ordinal);
        Assert.Empty(page.Nodes);
    }

    /// <summary>
    ///     Validates that an empty port set is a valid provider result that gives a node without ports.
    /// </summary>
    [Fact]
    public void NodeType_PortProvider_EmptyPortSet_AddsNodeWithoutPorts()
    {
        // Arrange: declared defaults, but a provider that returns nothing in either direction
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("t.Empty")
        {
            Inputs = { new PortDefinition("x", "f32") },
            Outputs = { new PortDefinition("y", "f32") },
            PortProvider = _ => new PortSet([], []),
        });
        var page = new Diagram(types).Pages.Add("P");

        // Act
        var node = page.AddNode("t.Empty", 0, 0);

        // Assert: the node was added without ports
        Assert.Same(node, Assert.Single(page.Nodes));
        Assert.Empty(node.Inputs);
        Assert.Empty(node.Outputs);
    }

    /// <summary>
    ///     Validates that a tooltip function returning null or an empty string means no tooltip, not the description.
    /// </summary>
    [Fact]
    public void NodeType_GetToolTip_FunctionReturnsNullOrEmpty_DoesNotUseDescription()
    {
        // Arrange: two types with a description and a tooltip function that yields nothing
        var types = new NodeTypeRegistry();
        var nullTip = new NodeType("t.NullTip") { Description = "described", ToolTip = _ => null };
        var emptyTip = new NodeType("t.EmptyTip") { Description = "described", ToolTip = _ => string.Empty };
        types.Add(nullTip);
        types.Add(emptyTip);
        var page = new Diagram(types).Pages.Add("P");

        // Act
        var fromNull = nullTip.GetToolTip(page.AddNode("t.NullTip", 0, 0));
        var fromEmpty = emptyTip.GetToolTip(page.AddNode("t.EmptyTip", 0, 0));

        // Assert: the function's result is returned unchanged
        Assert.Null(fromNull);
        Assert.Equal(string.Empty, fromEmpty);
    }

    /// <summary>
    ///     Validates that an exception thrown by the tooltip function reaches the caller unchanged.
    /// </summary>
    [Fact]
    public void NodeType_GetToolTip_FunctionThrows_PropagatesException()
    {
        // Arrange
        var failure = new InvalidOperationException("tooltip failed");
        var types = new NodeTypeRegistry();
        var type = new NodeType("t.Failing") { Description = "described", ToolTip = _ => throw failure };
        types.Add(type);
        var node = new Diagram(types).Pages.Add("P").AddNode("t.Failing", 0, 0);

        // Act
        var thrown = Assert.Throws<InvalidOperationException>(() => type.GetToolTip(node));

        // Assert: the same exception instance, not wrapped
        Assert.Same(failure, thrown);
    }
}
