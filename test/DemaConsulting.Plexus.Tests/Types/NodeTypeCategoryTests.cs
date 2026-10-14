namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests of the <see cref="NodeType.Category"/> presentation hint.
/// </summary>
public class NodeTypeCategoryTests
{
    /// <summary>
    ///     Validates that a node type has no category unless the host declares one.
    /// </summary>
    [Fact]
    public void NodeType_Category_NotDeclared_IsNull()
    {
        // Act
        var type = new NodeType("acme.Producer");

        // Assert
        Assert.Null(type.Category);
    }

    /// <summary>
    ///     Validates that a declared category is exposed unchanged and survives registration.
    /// </summary>
    [Fact]
    public void NodeType_Category_Declared_IsExposedAndKeptByRegistry()
    {
        // Arrange
        var types = new NodeTypeRegistry();
        var type = new NodeType("acme.Producer") { Category = "Sources" };

        // Act
        types.Add(type);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Sources", type.Category),
            () => Assert.Equal("Sources", types.Get("acme.Producer").Category));
    }
}
