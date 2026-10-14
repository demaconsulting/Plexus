namespace DemaConsulting.Plexus.Tests.Serialization;

/// <summary>
///     Unit tests for loading with <see cref="UnknownNodeTypeHandling.Skip"/> and for <see cref="LoadReport"/>.
/// </summary>
public class UnknownNodeTypeTests
{
    /// <summary>
    ///     The options that skip unknown node types.
    /// </summary>
    private static readonly DiagramOptions Skip = new() { UnknownNodeTypes = UnknownNodeTypeHandling.Skip };

    /// <summary>
    ///     Creates a registry that lacks the node types with payloads, like an older application version.
    /// </summary>
    /// <returns>The registry.</returns>
    private static NodeTypeRegistry CreateOlderTypes()
    {
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Opaque"));
        return types;
    }

    /// <summary>
    ///     Validates that the default is strict and still lists every missing type.
    /// </summary>
    [Fact]
    public void Diagram_FromJson_UnknownTypes_DefaultIsStrict()
    {
        // Arrange
        var json = SerializationFixtures.CreateRichDiagram().ToJson();

        // Act / Assert
        var exception = Assert.Throws<SnapshotException>(() => Diagram.FromJson(json, CreateOlderTypes()));
        Assert.Contains("acme.Sensor", exception.MissingNodeTypes);
        Assert.Contains("acme.Custom", exception.MissingNodeTypes);
        Assert.Throws<SnapshotException>(() => Diagram.FromJson(json, CreateOlderTypes(), new DiagramOptions()));
    }

    /// <summary>
    ///     Validates that skipping drops the unknown nodes, their wires and their group memberships, and reports them.
    /// </summary>
    [Fact]
    public void Diagram_FromJson_SkipUnknownTypes_DropsNodesWiresAndMemberships()
    {
        // Arrange: the rich diagram has a Sensor and a Custom node on the first page, wired to each other
        var source = SerializationFixtures.CreateRichDiagram();
        var main = source.Pages[0];
        main.CreateGroup([source.FindNode("Sensor1")!, source.FindNode("Custom1")!], "Payloads");

        // Act
        var loaded = Diagram.FromJson(source.ToJson(), CreateOlderTypes(), Skip);

        // Assert
        var page = loaded.Pages[0];
        Assert.DoesNotContain(page.Nodes, n => n.Type.Key is "acme.Sensor" or "acme.Custom");
        Assert.Equal(2, page.Nodes.Count);
        Assert.Single(page.Wires);
        Assert.DoesNotContain(page.Groups, g => g.Title == "Payloads");
        Assert.True(loaded.LoadReport.HasSkipped);
        Assert.Equal(["acme.Sensor", "acme.Custom"], loaded.LoadReport.SkippedNodeTypes);
        Assert.Equal(2, loaded.LoadReport.SkippedNodeCount);
        Assert.Equal(1, loaded.LoadReport.SkippedWireCount);
        Assert.Equal(1, loaded.LoadReport.SkippedGroupCount);
        Assert.Contains("2 node(s)", loaded.LoadReport.Message, StringComparison.Ordinal);
        Assert.Contains("acme.Sensor, acme.Custom", loaded.LoadReport.Message, StringComparison.Ordinal);
        Assert.False(loaded.History.CanUndo);
    }

    /// <summary>
    ///     Validates that a group that keeps some members stays, and loses only the skipped ones.
    /// </summary>
    [Fact]
    public void Diagram_FromSnapshot_SkipUnknownTypes_KeepsGroupsWithRemainingMembers()
    {
        // Arrange
        var source = SerializationFixtures.CreateDiagram();
        var page = source.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var sensor = page.AddNode("acme.Sensor", 200, 0, "S");
        page.CreateGroup([producer, sensor], "Mixed");

        // Act
        var loaded = Diagram.FromSnapshot(source.ToSnapshot(), CreateOlderTypes(), Skip);

        // Assert
        var group = Assert.Single(loaded.Pages[0].Groups);
        Assert.Equal(["P"], group.Members.OfType<Node>().Select(n => n.Name));
        Assert.Equal(0, loaded.LoadReport.SkippedGroupCount);
        Assert.Equal(1, loaded.LoadReport.SkippedNodeCount);
    }

    /// <summary>
    ///     Validates that a group nested only in another group disappears together with it.
    /// </summary>
    [Fact]
    public void Diagram_FromSnapshot_SkipUnknownTypes_DropsGroupsThatBecomeEmptyRepeatedly()
    {
        // Arrange: Outer contains only Inner, which contains only an unknown node
        var source = SerializationFixtures.CreateDiagram();
        var page = source.Pages.Add("Main");
        page.AddNode("acme.Producer", 0, 0, "P");
        var sensor = page.AddNode("acme.Sensor", 200, 0, "S");
        var inner = page.CreateGroup([sensor], "Inner");
        page.CreateGroup([inner], "Outer");

        // Act
        var loaded = Diagram.FromSnapshot(source.ToSnapshot(), CreateOlderTypes(), Skip);

        // Assert
        Assert.Empty(loaded.Pages[0].Groups);
        Assert.Equal(2, loaded.LoadReport.SkippedGroupCount);
        Assert.Equal(loaded.Pages[0].Elements.Count, loaded.Pages[0].Nodes.Count);
    }

    /// <summary>
    ///     Validates that skipping with nothing to skip leaves an empty report and the diagram unchanged.
    /// </summary>
    [Fact]
    public void Diagram_FromJson_SkipUnknownTypes_NothingMissing_ReportIsEmpty()
    {
        // Arrange
        var source = SerializationFixtures.CreateRichDiagram();

        // Act
        var loaded = Diagram.FromJson(source.ToJson(), SerializationFixtures.CreateTypes(), Skip);

        // Assert
        Assert.False(loaded.LoadReport.HasSkipped);
        Assert.Equal(string.Empty, loaded.LoadReport.Message);
        Assert.Same(LoadReport.Empty, loaded.LoadReport);
        Assert.Equal(TestModel.Dump(source), TestModel.Dump(loaded));
        Assert.Same(LoadReport.Empty, new Diagram(TestModel.CreateTypes()).LoadReport);
    }

    /// <summary>
    ///     Validates that skipping also works when the unknown nodes sit on a later page and in the tree loader.
    /// </summary>
    [Fact]
    public void Diagram_FromTree_SkipUnknownTypes_WorksAcrossPages()
    {
        // Arrange
        var source = SerializationFixtures.CreateDiagram();
        source.Pages.Add("One").AddNode("acme.Producer", 0, 0);
        source.Pages.Add("Two").AddNode("acme.Sensor", 0, 0);

        // Act
        var loaded = Diagram.FromTree(source.ToTree(), CreateOlderTypes(), Skip);

        // Assert
        Assert.Equal(2, loaded.Pages.Count);
        Assert.Single(loaded.Pages[0].Nodes);
        Assert.Empty(loaded.Pages[1].Nodes);
        Assert.Equal(["acme.Sensor"], loaded.LoadReport.SkippedNodeTypes);
    }
}
