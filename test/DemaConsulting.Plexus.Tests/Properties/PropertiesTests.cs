using System.ComponentModel;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Subsystem tests for the Properties subsystem, exercising property definitions and property bags together.
/// </summary>
/// <remarks>
///     A property bag always belongs to a node on a page of a diagram, so the tests build a small registry with one
///     node type, a diagram and a page; those model types are only the host for the property units under test.
/// </remarks>
public class PropertiesTests
{
    /// <summary>
    ///     Creates a diagram with a single node of a type that declares one property of every kind.
    /// </summary>
    /// <returns>The diagram and the node.</returns>
    private static (Diagram Diagram, Node Node) Create()
    {
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("demo.Sensor")
        {
            Properties =
            {
                new PropertyDefinition("Label", "S") { ShowInSummary = true },
                new PropertyDefinition("Gain", 1.0) { Unit = "dB", DisplayName = "Gain Factor", ShowInSummary = true },
                new PropertyDefinition("Enabled", true),
                new PropertyDefinition("Retries", 3),
                new PropertyDefinition("Samples", 100L),
                new PropertyDefinition("Tint", Color.Parse("#112233")),
                new PropertyDefinition("Mode", "A") { Choices = ["A", "B", "C"] },
            },
        });

        var diagram = new Diagram(types);
        return (diagram, diagram.Pages.Add("Main").AddNode("demo.Sensor", 0, 0));
    }

    /// <summary>
    ///     Validates that the declarations of a registered node type are exposed with kind, default, choices and hints.
    /// </summary>
    [Fact]
    public void Properties_Declaration_DeclaredTypes_ExposeKindsDefaultsAndChoices()
    {
        // Arrange
        var (_, node) = Create();
        var bag = node.Properties;

        // Act
        var kinds = new[] { "Label", "Gain", "Enabled", "Retries", "Samples", "Tint", "Mode" }
            .Select(name => bag.GetDefinition(name)!.ValueKind)
            .ToArray();

        // Assert
        Assert.Equal(
            new[]
            {
                PropertyValueKind.String, PropertyValueKind.Double, PropertyValueKind.Boolean, PropertyValueKind.Int32,
                PropertyValueKind.Int64, PropertyValueKind.Color, PropertyValueKind.Choice,
            },
            kinds);
        Assert.Equal(new[] { "A", "B", "C" }, bag.GetDefinition("Mode")!.Choices);
        Assert.Equal(Color.Parse("#112233"), bag.GetDefinition("Tint")!.DefaultValue);
        Assert.Equal("dB", bag.GetDefinition("Gain")!.Unit);
        Assert.Null(bag.GetDefinition("Label")!.Choices);
        Assert.Null(bag.GetDefinition("undeclared"));
    }

    /// <summary>
    ///     Validates that invalid declarations are rejected when they are created.
    /// </summary>
    [Fact]
    public void Properties_Declaration_InvalidDeclarations_AreRejected()
    {
        // Act / Assert: blank name, unsupported default, non-finite default, bad choice lists
        Assert.Throws<ArgumentException>(() => new PropertyDefinition(" ", 1));
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", new object()));
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", double.NaN));
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", "X") { Choices = ["A", "B"] });
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", "A") { Choices = ["A", "A"] });
    }

    /// <summary>
    ///     Validates that summary lines combine the display name, the formatted bag value and the unit.
    /// </summary>
    [Fact]
    public void Properties_Display_SummaryOfNodeValues_UsesNamesUnitsAndFormats()
    {
        // Arrange
        var (_, node) = Create();
        node.Properties["Gain"] = 2.5;
        var custom = new PropertyDefinition("Tint", Color.Red) { Format = v => $"<{v}>" };

        // Act
        var gain = node.Properties.GetDefinition("Gain")!.FormatSummary(node.Properties["Gain"]);
        var label = node.Properties.GetDefinition("Label")!.FormatSummary(node.Properties["Label"]);
        var tint = custom.FormatSummary(node.Properties["Tint"]);

        // Assert
        Assert.Equal("Gain Factor: 2.5 dB", gain);
        Assert.Equal("Label: S", label);
        Assert.Equal("Tint: <#112233>", tint);
    }

    /// <summary>
    ///     Validates that a new node holds the declared default of every property and nothing else.
    /// </summary>
    [Fact]
    public void Properties_Defaults_NewNode_HoldsEveryDeclaredDefault()
    {
        // Act
        var (_, node) = Create();

        // Assert
        Assert.Equal(7, node.Properties.Count);
        Assert.Equal("S", node.Properties["Label"]);
        Assert.Equal(1.0, node.Properties["Gain"]);
        Assert.True((bool)node.Properties["Enabled"]);
        Assert.Equal(3, node.Properties["Retries"]);
        Assert.Equal(100L, node.Properties["Samples"]);
        Assert.Equal(Color.Parse("#112233"), node.Properties["Tint"]);
        Assert.Equal("A", node.Properties["Mode"]);
    }

    /// <summary>
    ///     Validates that assignments convert compatible values, reject invalid ones and accept ad-hoc portable values.
    /// </summary>
    [Fact]
    public void Properties_Validation_Assignments_ConvertRejectAndAllowAdHoc()
    {
        // Arrange
        var (diagram, node) = Create();
        var entries = diagram.History.UndoCount;

        // Act: compatible values convert to the declared kind
        node.Properties["Gain"] = 3;
        node.Properties["Retries"] = 7L;
        node.Properties["Samples"] = 8;
        node.Properties["Mode"] = "C";

        // Assert
        Assert.IsType<double>(node.Properties["Gain"]);
        Assert.IsType<int>(node.Properties["Retries"]);
        Assert.IsType<long>(node.Properties["Samples"]);
        Assert.Equal("C", node.Properties["Mode"]);

        // Act / Assert: invalid values are rejected and leave the bag unchanged
        var afterValid = diagram.History.UndoCount;
        Assert.Throws<ArgumentException>(() => node.Properties["Gain"] = "text");
        Assert.Throws<ArgumentException>(() => node.Properties["Retries"] = 1.5);
        Assert.Throws<ArgumentException>(() => node.Properties["Mode"] = "Z");
        Assert.Throws<ArgumentException>(() => node.Properties["Tint"] = "#FFFFFF");
        Assert.Equal(3.0, node.Properties["Gain"]);
        Assert.Equal(afterValid, diagram.History.UndoCount);
        Assert.Equal(entries + 4, afterValid);

        // Act / Assert: undeclared names accept portable kinds only
        node.Properties["note"] = "hello";
        Assert.Equal("hello", node.Properties["note"]);
        Assert.Null(node.Properties.GetDefinition("note"));
        Assert.Throws<ArgumentException>(() => node.Properties["bad"] = new object());
        Assert.False(node.Properties.ContainsKey("bad"));
    }

    /// <summary>
    ///     Validates reading, typed conversion, enumeration order and reset of declared and ad-hoc properties.
    /// </summary>
    [Fact]
    public void Properties_Access_ReadEnumerateAndReset_BehaveAsDocumented()
    {
        // Arrange: change one declared property and add two ad-hoc ones
        var (_, node) = Create();
        node.Properties["Retries"] = 9;
        node.Properties["zeta"] = 1;
        node.Properties["alpha"] = "x";

        // Assert: typed reads convert numbers and reject the rest
        Assert.Equal(9L, node.Properties.Get<long>("Retries"));
        Assert.Equal(9.0, node.Properties.Get<double>("Retries"));
        Assert.Throws<InvalidCastException>(() => node.Properties.Get<string>("Retries"));
        Assert.Throws<KeyNotFoundException>(() => node.Properties.Get<int>("missing"));
        Assert.False(node.Properties.TryGetValue("missing", out _));

        // Assert: declared properties come first in declaration order, then ad-hoc ones in insertion order
        Assert.Equal(
            new[] { "Label", "Gain", "Enabled", "Retries", "Samples", "Tint", "Mode", "zeta", "alpha" },
            node.Properties.Select(p => p.Key));

        // Act: reset a declared property and an ad-hoc one
        node.Properties.Reset("Retries");
        node.Properties.Reset("zeta");

        // Assert: the declared one is back at its default, the ad-hoc one is gone
        Assert.Equal(3, node.Properties["Retries"]);
        Assert.False(node.Properties.ContainsKey("zeta"));
        Assert.Throws<KeyNotFoundException>(() => node.Properties.Reset("zeta"));
    }

    /// <summary>
    ///     Validates the edit session behavior: undo and redo, merged slider edits, no-op edits and notifications.
    /// </summary>
    [Fact]
    public void Properties_UndoNotification_EditSession_UndoesRedoesAndNotifies()
    {
        // Arrange
        var (diagram, node) = Create();
        var names = new List<string?>();
        var changes = new List<DiagramChangedEventArgs>();
        var baseline = diagram.History.UndoCount;
        ((INotifyPropertyChanged)node.Properties).PropertyChanged += (_, e) => names.Add(e.PropertyName);
        diagram.Changed += (_, e) => changes.Add(e);

        // Act: a plain edit, a no-op edit, an ad-hoc edit and a reset
        node.Properties["Retries"] = 5;
        node.Properties["Retries"] = 5;
        node.Properties["note"] = "n";
        node.Properties.Reset("note");

        // Assert: one entry per real change, with notifications for the bag and the diagram
        Assert.Equal(baseline + 3, diagram.History.UndoCount);
        Assert.Equal(new[] { "Retries", "Item[]", "note", "Item[]", "note", "Item[]" }, names);
        Assert.All(changes, c => Assert.Equal("Properties", c.PropertyName));
        Assert.Equal(3, changes.Count);

        // Act / Assert: undo and redo restore the values, including the ad-hoc one
        Assert.True(diagram.History.Undo());
        Assert.Equal("n", node.Properties["note"]);
        Assert.True(diagram.History.Undo());
        Assert.False(node.Properties.ContainsKey("note"));
        Assert.True(diagram.History.Undo());
        Assert.Equal(3, node.Properties["Retries"]);
        Assert.True(diagram.History.Redo());
        Assert.Equal(5, node.Properties["Retries"]);

        // Act: repeated edits of one property in a transaction (a slider drag)
        var before = diagram.History.UndoCount;
        using (diagram.History.BeginTransaction("Slide"))
        {
            for (var i = 1; i <= 10; i++)
            {
                node.Properties["Gain"] = (double)i;
            }
        }

        // Assert: one merged entry that undoes to the value before the drag
        Assert.Equal(before + 1, diagram.History.UndoCount);
        diagram.History.Undo();
        Assert.Equal(1.0, node.Properties["Gain"]);

        // Act / Assert: a removed node rejects further edits
        node.Remove();
        Assert.Throws<InvalidOperationException>(() => node.Properties["Gain"] = 2.0);
    }

    /// <summary>
    ///     Validates that property edits of every kind (set, reset, batch and ad-hoc) recompute the ports in the same undo entry.
    /// </summary>
    [Fact]
    public void Properties_PortRecompute_PropertyEdits_UpdatePortsInSameEntry()
    {
        // Arrange
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var bus = page.AddNode("acme.Bus", 0, 0);
        var collector = page.AddNode("acme.Collector", 300, 0);

        // Act / Assert: set, batch and reset are each one exact entry that changes the ports
        TestModel.AssertRoundTrip(diagram, () => bus.Properties["Channels"] = 4);
        Assert.Equal(4, bus.Inputs.Count);
        TestModel.AssertRoundTrip(diagram, () => bus.Properties.TrySetAll(new Dictionary<string, object?> { ["Channels"] = 3, ["Kind"] = "i32" }, out _));
        Assert.All(bus.Inputs, p => Assert.Equal("i32", p.TypeKey));
        TestModel.AssertRoundTrip(diagram, () => bus.Properties.Reset("Channels"));
        Assert.Equal(2, bus.Inputs.Count);

        // Act / Assert: adding and removing an ad-hoc value the provider reads are each one exact entry too
        TestModel.AssertRoundTrip(diagram, () => collector.Properties["FanIn"] = true);
        Assert.True(collector.Input("in").AllowsMultipleWires);
        TestModel.AssertRoundTrip(diagram, () => collector.Properties.Reset("FanIn"));
        Assert.False(collector.Input("in").AllowsMultipleWires);
    }
}
