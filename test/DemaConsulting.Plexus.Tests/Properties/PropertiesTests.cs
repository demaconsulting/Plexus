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
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", "A") { Choices = ["A", null!] });
    }

    /// <summary>
    ///     Validates that declared presentation hints are exposed with the declaration and never restrict the values
    ///     the property accepts.
    /// </summary>
    [Fact]
    public void Properties_PresentationHints_DeclaredHints_AreExposedWithoutRestrictingValues()
    {
        // Arrange: a type whose property carries every presentation hint, including the read-only hint
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("demo.Amp")
        {
            Properties =
            {
                new PropertyDefinition("Gain", 1.0)
                {
                    DisplayName = "Gain Factor",
                    Description = "Output gain",
                    Category = "Signal",
                    Unit = "dB",
                    EditorHint = "slider",
                    ShowInSummary = true,
                    IsReadOnly = true,
                },
            },
        });
        var diagram = new Diagram(types);
        var node = diagram.Pages.Add("Main").AddNode("demo.Amp", 0, 0);

        // Act: read the hints through the node and assign a value no hint mentions
        var definition = node.Properties.GetDefinition("Gain")!;
        node.Properties["Gain"] = -1000.0;

        // Assert: the hints are exposed and did not restrict the assignment
        Assert.Multiple(
            () => Assert.Equal("Gain Factor", definition.DisplayName),
            () => Assert.Equal("Output gain", definition.Description),
            () => Assert.Equal("Signal", definition.Category),
            () => Assert.Equal("dB", definition.Unit),
            () => Assert.Equal("slider", definition.EditorHint),
            () => Assert.True(definition.ShowInSummary),
            () => Assert.True(definition.IsReadOnly),
            () => Assert.Equal(-1000.0, node.Properties["Gain"]));
    }

    /// <summary>
    ///     Validates that display-order hints are exposed but change neither the declaration order nor the order in
    ///     which a node's properties are enumerated.
    /// </summary>
    [Fact]
    public void Properties_DisplayOrder_DeclaredOrders_KeepDeclarationOrder()
    {
        // Arrange: properties A, B and C declared with orders 3, 1 and 2, and D left at the default
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("demo.Ordered")
        {
            Properties =
            {
                new PropertyDefinition("A", 1) { Order = 3 },
                new PropertyDefinition("B", 1) { Order = 1 },
                new PropertyDefinition("C", 1) { Order = 2 },
                new PropertyDefinition("D", 1),
            },
        });
        var diagram = new Diagram(types);

        // Act
        var node = diagram.Pages.Add("Main").AddNode("demo.Ordered", 0, 0);

        // Assert: the hints are exposed, the declaration and enumeration orders are unchanged
        Assert.Equal(new[] { 3, 1, 2, 0 }, node.Type.Properties.Select(p => p.Order));
        Assert.Equal(new[] { "A", "B", "C", "D" }, node.Type.Properties.Select(p => p.Name));
        Assert.Equal(new[] { "A", "B", "C", "D" }, node.Properties.Select(p => p.Key));
    }

    /// <summary>
    ///     Validates that typed text converts to values of the property's kind (with a reason when invalid) and that
    ///     edit text converts back to the same value.
    /// </summary>
    [Fact]
    public void Properties_TextEditing_TypedText_ParsesAndRoundTrips()
    {
        // Arrange
        var (_, node) = Create();
        var gain = node.Properties.GetDefinition("Gain")!;
        var retries = node.Properties.GetDefinition("Retries")!;
        var tint = node.Properties.GetDefinition("Tint")!;
        var mode = node.Properties.GetDefinition("Mode")!;

        // Act / Assert: valid text produces values of exactly the declared kinds
        Assert.True(gain.TryParse("2.5 dB", out var gainValue, out _));
        Assert.Equal(2.5, gainValue);
        Assert.True(retries.TryParse(" 7 ", out var retriesValue, out _));
        Assert.Equal(7, retriesValue);
        Assert.True(tint.TryParse("#445566", out var tintValue, out _));
        Assert.Equal(Color.Parse("#445566"), tintValue);
        Assert.True(mode.TryParse("b", out var modeValue, out _));
        Assert.Equal("B", modeValue);

        // Act / Assert: invalid text is refused with a reason
        Assert.False(retries.TryParse("1.5", out var refused, out var reason));
        Assert.Null(refused);
        Assert.False(string.IsNullOrWhiteSpace(reason));

        // Act / Assert: the edit text of a stored value converts back to the same value
        node.Properties["Gain"] = 0.1 + 0.2;
        var text = PropertyDefinition.FormatForEdit(node.Properties["Gain"]);
        Assert.True(gain.TryParse(text, out var roundTrip, out _));
        Assert.Equal(node.Properties["Gain"], roundTrip);
    }

    /// <summary>
    ///     Validates that a batch edit with one invalid value changes nothing and records nothing, while a valid batch
    ///     applies every value as one undo entry.
    /// </summary>
    [Fact]
    public void Properties_BatchEdit_OneInvalidValue_ChangesNothing()
    {
        // Arrange
        var (diagram, node) = Create();
        var entries = diagram.History.UndoCount;

        // Act: one invalid value among valid ones
        var applied = node.Properties.TrySetAll(
            new Dictionary<string, object?> { ["Gain"] = 2.0, ["Retries"] = "many", ["Mode"] = "B" },
            out var errors);

        // Assert: nothing changed, nothing recorded, the invalid value is reported
        Assert.False(applied);
        Assert.Equal(1.0, node.Properties["Gain"]);
        Assert.Equal("A", node.Properties["Mode"]);
        Assert.Equal(entries, diagram.History.UndoCount);
        Assert.Contains("Retries", errors.Keys);

        // Act: the same batch with the value corrected
        applied = node.Properties.TrySetAll(
            new Dictionary<string, object?> { ["Gain"] = 2.0, ["Retries"] = "4", ["Mode"] = "B" },
            out errors);

        // Assert: every value applied as one undo entry
        Assert.True(applied);
        Assert.Empty(errors);
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        Assert.Equal(4, node.Properties["Retries"]);
        diagram.History.Undo();
        Assert.Equal(1.0, node.Properties["Gain"]);
        Assert.Equal(3, node.Properties["Retries"]);
        Assert.Equal("A", node.Properties["Mode"]);
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
