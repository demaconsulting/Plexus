using System.Text.Json;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the <see cref="Diagram.ToJson(bool)"/> / <see cref="Diagram.FromJson(string, NodeTypeRegistry, DiagramOptions?)"/> convenience wrappers.
/// </summary>
public class DiagramJsonTests
{
    /// <summary>
    ///     Validates that a diagram survives a JSON round trip: pages, nodes, wires, properties.
    /// </summary>
    [Fact]
    public void Diagram_ToJsonFromJson_RoundTripsContent()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 15, 30, "Source");
        var consumer = page.AddNode("acme.Consumer", 300, 30, "Sink");
        producer.Properties["Gain"] = 2.5;
        page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(150, 45)]);

        // Act
        var json = diagram.ToJson();
        var loaded = Diagram.FromJson(json, TestModel.CreateTypes());

        // Assert
        Assert.Equal(TestModel.Dump(diagram), TestModel.Dump(loaded));
        Assert.False(loaded.History.CanUndo);
    }

    /// <summary>
    ///     Validates that the wrapper equals the documented two-step snapshot path and honors the indentation flag.
    /// </summary>
    [Fact]
    public void Diagram_ToJson_MatchesPlexusJsonAndIndentFlag()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        diagram.Pages.Add("Main").AddNode("acme.Producer", 0, 0);

        // Act
        var indented = diagram.ToJson();
        var compact = diagram.ToJson(indented: false);

        // Assert
        Assert.Equal(PlexusJson.Serialize(diagram.ToSnapshot()), indented);
        Assert.Contains('\n', indented);
        Assert.DoesNotContain('\n', compact);
        Assert.Equal(TestModel.Dump(Diagram.FromJson(indented, TestModel.CreateTypes())), TestModel.Dump(Diagram.FromJson(compact, TestModel.CreateTypes())));
    }

    /// <summary>
    ///     Validates the error behavior of FromJson: nulls, malformed text and unknown node types.
    /// </summary>
    [Fact]
    public void Diagram_FromJson_InvalidInput_Throws()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        diagram.Pages.Add("Main").AddNode("acme.Producer", 0, 0);
        var json = diagram.ToJson();

        // Assert
        Assert.Throws<ArgumentNullException>(() => Diagram.FromJson((string)null!, TestModel.CreateTypes()));
        Assert.Throws<ArgumentNullException>(() => Diagram.FromJson(json, null!));
        Assert.ThrowsAny<JsonException>(() => Diagram.FromJson("{ not json", TestModel.CreateTypes()));
        Assert.Throws<SnapshotException>(() => Diagram.FromJson(json, []));
    }
}
