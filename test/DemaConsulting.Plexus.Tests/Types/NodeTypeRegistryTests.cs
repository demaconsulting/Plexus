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
}
