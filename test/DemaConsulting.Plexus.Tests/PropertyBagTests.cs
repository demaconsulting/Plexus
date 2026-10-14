using System.ComponentModel;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="PropertyDefinition"/> and <see cref="PropertyBag"/>.
/// </summary>
public class PropertyBagTests
{
    /// <summary>
    ///     Creates a diagram with one producer node.
    /// </summary>
    /// <returns>The diagram and the node.</returns>
    private static (Diagram Diagram, Node Node) Create()
    {
        var diagram = TestModel.CreateDiagram();
        return (diagram, diagram.Pages.Add("Main").AddNode("acme.Producer", 0, 0));
    }

    /// <summary>
    ///     Validates that the value kind is inferred from the default value.
    /// </summary>
    /// <param name="value">The default value.</param>
    /// <param name="expected">The expected kind.</param>
    [Theory]
    [InlineData(true, PropertyValueKind.Boolean)]
    [InlineData(3, PropertyValueKind.Int32)]
    [InlineData(3L, PropertyValueKind.Int64)]
    [InlineData(3.5, PropertyValueKind.Double)]
    [InlineData("x", PropertyValueKind.String)]
    public void PropertyDefinition_Constructor_InfersKindFromDefault(object value, PropertyValueKind expected)
    {
        // Act
        var definition = new PropertyDefinition("P", value);

        // Assert
        Assert.Equal(expected, definition.ValueKind);
        Assert.Equal(value, definition.DefaultValue);
        Assert.Equal("P", definition.DisplayName);
    }

    /// <summary>
    ///     Validates that a color default infers the Color kind and that small numeric types widen.
    /// </summary>
    [Fact]
    public void PropertyDefinition_Constructor_ColorAndSmallNumbers_NormalizeToCanonicalKinds()
    {
        // Act
        var color = new PropertyDefinition("C", Color.Red);
        var small = new PropertyDefinition("S", (short)4);
        var single = new PropertyDefinition("F", 1.5f);

        // Assert
        Assert.Equal(PropertyValueKind.Color, color.ValueKind);
        Assert.Equal(PropertyValueKind.Int32, small.ValueKind);
        Assert.IsType<int>(small.DefaultValue);
        Assert.Equal(PropertyValueKind.Double, single.ValueKind);
        Assert.IsType<double>(single.DefaultValue);
    }

    /// <summary>
    ///     Validates that unsupported default types and non-finite numbers are rejected.
    /// </summary>
    [Fact]
    public void PropertyDefinition_Constructor_UnsupportedDefault_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", new object()));
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", DateTime.Now));
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", double.NaN));
        Assert.Throws<ArgumentNullException>(() => new PropertyDefinition("P", null!));
        Assert.Throws<ArgumentException>(() => new PropertyDefinition(" ", 1));
    }

    /// <summary>
    ///     Validates the Choices initializer: kind, default and combination with other initializers.
    /// </summary>
    [Fact]
    public void PropertyDefinition_Choices_Initializer_MakesChoiceProperty()
    {
        // Act
        var choice = new PropertyDefinition("Mode", "B") { Choices = ["A", "B"], ShowInSummary = true, Unit = "x" };

        // Assert
        Assert.Equal(PropertyValueKind.Choice, choice.ValueKind);
        Assert.Equal(new[] { "A", "B" }, choice.Choices);
        Assert.Equal("B", choice.DefaultValue);
        Assert.True(choice.ShowInSummary);
        Assert.Equal("Mode: B x", choice.FormatSummary("B"));
    }

    /// <summary>
    ///     Validates that invalid choice lists and defaults are rejected.
    /// </summary>
    [Fact]
    public void PropertyDefinition_Choices_Invalid_Throws()
    {
        // Assert
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("Mode", "C") { Choices = ["A", "B"] });
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("Mode", "A") { Choices = [] });
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("Mode", "A") { Choices = ["A", "A"] });
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("Mode", 1) { Choices = ["1"] });
    }

    /// <summary>
    ///     Validates that a property without choices reports none.
    /// </summary>
    [Fact]
    public void PropertyDefinition_Choices_NotSet_IsNull()
    {
        // Act
        var plain = new PropertyDefinition("Name", "x");

        // Assert
        Assert.Null(plain.Choices);
        Assert.Equal(PropertyValueKind.String, plain.ValueKind);
    }

    /// <summary>
    ///     Validates the summary formatting with unit, custom format and display name.
    /// </summary>
    [Fact]
    public void PropertyDefinition_FormatSummary_AppliesDisplayNameUnitAndFormat()
    {
        // Arrange
        var plain = new PropertyDefinition("Gain", 1.5) { DisplayName = "Gain Factor", Unit = "dB" };
        var custom = new PropertyDefinition("Gain", 1.5) { Format = v => $"<{v}>" };

        // Assert
        Assert.Equal("Gain Factor: 1.5 dB", plain.FormatSummary(1.5));
        Assert.Equal("Gain: <1.5>", custom.FormatSummary(1.5));
        Assert.Equal("#112233", new PropertyDefinition("C", Color.Parse("#112233")).FormatValue(Color.Parse("#112233")));
    }

    /// <summary>
    ///     Validates that a new node's bag holds the declared defaults.
    /// </summary>
    [Fact]
    public void PropertyBag_Initial_ContainsDeclaredDefaults()
    {
        // Arrange / Act
        var (_, node) = Create();

        // Assert
        Assert.Equal("F32", node.Properties["Format"]);
        Assert.Equal(1.0, node.Properties["Gain"]);
        Assert.Equal(7, node.Properties.Count);
        Assert.True(node.Properties.ContainsKey("Enabled"));
        Assert.Equal("A", node.Properties.Get<string>("Mode"));
    }

    /// <summary>
    ///     Validates that enumeration lists declared properties in declaration order and then ad-hoc values in insertion order.
    /// </summary>
    [Fact]
    public void PropertyBag_Enumeration_DeclaredFirstThenAdHocInInsertionOrder()
    {
        // Arrange
        var (_, node) = Create();
        node.Properties["zeta"] = 1;
        node.Properties["alpha"] = "x";

        // Act
        var names = node.Properties.Select(p => p.Key).ToList();

        // Assert
        Assert.Equal(new[] { "Format", "Gain", "Enabled", "Count", "Big", "Tint", "Mode", "zeta", "alpha" }, names);
        Assert.Equal(names, node.Properties.Keys);
        Assert.Equal(9, node.Properties.Values.Count());
    }

    /// <summary>
    ///     Validates that every property kind can be set and read back.
    /// </summary>
    [Fact]
    public void PropertyBag_Set_AllKinds_StoreValues()
    {
        // Arrange
        var (_, node) = Create();

        // Act
        node.Properties["Format"] = "F64";
        node.Properties["Gain"] = 2.5;
        node.Properties["Enabled"] = false;
        node.Properties["Count"] = 9;
        node.Properties["Big"] = 1L << 40;
        node.Properties["Tint"] = Color.Blue;
        node.Properties["Mode"] = "B";

        // Assert
        Assert.Equal("F64", node.Properties.Get<string>("Format"));
        Assert.Equal(2.5, node.Properties.Get<double>("Gain"));
        Assert.False(node.Properties.Get<bool>("Enabled"));
        Assert.Equal(9, node.Properties.Get<int>("Count"));
        Assert.Equal(1L << 40, node.Properties.Get<long>("Big"));
        Assert.Equal(Color.Blue, node.Properties.Get<Color>("Tint"));
        Assert.Equal("B", node.Properties.Get<string>("Mode"));
    }

    /// <summary>
    ///     Validates that compatible numeric types convert to the declared kind.
    /// </summary>
    [Fact]
    public void PropertyBag_Set_CompatibleNumericTypes_Convert()
    {
        // Arrange
        var (_, node) = Create();

        // Act: int -> double, short -> int, int -> long, long (in range) -> int
        node.Properties["Gain"] = 3;
        node.Properties["Count"] = (short)4;
        node.Properties["Big"] = 6;
        node.Properties["Count"] = 7L;

        // Assert: stored in the declared kind
        Assert.IsType<double>(node.Properties["Gain"]);
        Assert.Equal(3.0, node.Properties["Gain"]);
        Assert.IsType<int>(node.Properties["Count"]);
        Assert.Equal(7, node.Properties["Count"]);
        Assert.IsType<long>(node.Properties["Big"]);
    }

    /// <summary>
    ///     Validates that incompatible values are rejected and leave the bag unchanged.
    /// </summary>
    [Fact]
    public void PropertyBag_Set_IncompatibleValues_ThrowArgumentException()
    {
        // Arrange
        var (diagram, node) = Create();
        var entries = diagram.History.UndoCount;

        // Act / Assert: wrong kinds, lossy conversions and out-of-range numbers
        Assert.Throws<ArgumentException>(() => node.Properties["Gain"] = "text");
        Assert.Throws<ArgumentException>(() => node.Properties["Enabled"] = 1);
        Assert.Throws<ArgumentException>(() => node.Properties["Count"] = 1.5);
        Assert.Throws<ArgumentException>(() => node.Properties["Count"] = long.MaxValue);
        Assert.Throws<ArgumentException>(() => node.Properties["Tint"] = "#FFFFFF");
        Assert.Throws<ArgumentException>(() => node.Properties["Gain"] = double.PositiveInfinity);
        Assert.Throws<ArgumentNullException>(() => node.Properties["Gain"] = null!);
        Assert.Equal(1.0, node.Properties["Gain"]);
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that choice properties accept only their options.
    /// </summary>
    [Fact]
    public void PropertyBag_Set_ChoiceOutsideOptions_ThrowsArgumentException()
    {
        // Arrange
        var (_, node) = Create();

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => node.Properties["Mode"] = "C");
        Assert.Contains("A, B", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that ad-hoc properties can be added with allowed kinds only.
    /// </summary>
    [Fact]
    public void PropertyBag_Set_AdHocProperty_AllowsPortableKindsOnly()
    {
        // Arrange
        var (_, node) = Create();

        // Act
        node.Properties["note"] = "hi";
        node.Properties["level"] = 3;

        // Assert
        Assert.Equal("hi", node.Properties["note"]);
        Assert.Null(node.Properties.GetDefinition("note"));
        Assert.NotNull(node.Properties.GetDefinition("Gain"));
        Assert.Throws<ArgumentException>(() => node.Properties["bad"] = new object());
        Assert.False(node.Properties.ContainsKey("bad"));
    }

    /// <summary>
    ///     Validates the read-side API: TryGetValue, missing keys and generic conversion.
    /// </summary>
    [Fact]
    public void PropertyBag_Read_MissingKeysAndConversions_BehaveAsDocumented()
    {
        // Arrange
        var (_, node) = Create();

        // Assert
        Assert.True(node.Properties.TryGetValue("Count", out var count));
        Assert.Equal(1, count);
        Assert.False(node.Properties.TryGetValue("missing", out _));
        Assert.Throws<KeyNotFoundException>(() => node.Properties["missing"]);
        Assert.Throws<KeyNotFoundException>(() => node.Properties.Get<int>("missing"));
        Assert.Equal(1.0, node.Properties.Get<double>("Count"));
        Assert.Equal(1L, node.Properties.Get<long>("Count"));
        Assert.Throws<InvalidCastException>(() => node.Properties.Get<string>("Count"));
        Assert.Throws<InvalidCastException>(() => node.Properties.Get<bool>("Count"));
    }

    /// <summary>
    ///     Validates that an overflowing numeric conversion on read is reported as an invalid cast.
    /// </summary>
    [Fact]
    public void PropertyBag_Get_OverflowingConversion_ThrowsInvalidCastException()
    {
        // Arrange
        var (_, node) = Create();
        node.Properties["Big"] = long.MaxValue;

        // Act / Assert
        Assert.Throws<InvalidCastException>(() => node.Properties.Get<int>("Big"));
    }

    /// <summary>
    ///     Validates that setting an unchanged value records nothing.
    /// </summary>
    [Fact]
    public void PropertyBag_Set_SameValue_RecordsNothing()
    {
        // Arrange
        var (diagram, node) = Create();
        var entries = diagram.History.UndoCount;

        // Act: assign the default again, also through a convertible type
        node.Properties["Gain"] = 1.0;
        node.Properties["Gain"] = 1;

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that Reset restores a declared default and is itself undoable.
    /// </summary>
    [Fact]
    public void PropertyBag_Reset_DeclaredProperty_RestoresDefaultAndRoundTrips()
    {
        // Arrange
        var (diagram, node) = Create();
        node.Properties["Gain"] = 9.0;

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => node.Properties.Reset("Gain"));
        Assert.Equal(1.0, node.Properties["Gain"]);
    }

    /// <summary>
    ///     Validates that Reset on a default value records nothing and on an unknown name throws.
    /// </summary>
    [Fact]
    public void PropertyBag_Reset_AlreadyDefaultOrUnknown_NoEntryOrThrows()
    {
        // Arrange
        var (diagram, node) = Create();
        var entries = diagram.History.UndoCount;

        // Act
        node.Properties.Reset("Gain");

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
        Assert.Throws<KeyNotFoundException>(() => node.Properties.Reset("missing"));
    }

    /// <summary>
    ///     Validates that Reset on an ad-hoc property removes it, and undo restores its value and position.
    /// </summary>
    [Fact]
    public void PropertyBag_Reset_AdHocProperty_RemovesAndRestoresPositionOnUndo()
    {
        // Arrange: two ad-hoc values; reset the first
        var (diagram, node) = Create();
        node.Properties["first"] = 1;
        node.Properties["second"] = 2;

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => node.Properties.Reset("first"));
        Assert.False(node.Properties.ContainsKey("first"));
        diagram.History.Undo();
        Assert.Equal(new[] { "first", "second" }, node.Properties.Select(p => p.Key).Skip(7));
    }

    /// <summary>
    ///     Validates that setting properties (declared and ad-hoc) round-trips through undo and redo.
    /// </summary>
    [Fact]
    public void PropertyBag_Set_DeclaredAndAdHoc_UndoRedoRoundTrip()
    {
        // Arrange
        var (diagram, node) = Create();

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => node.Properties["Gain"] = 4.5);
        TestModel.AssertRoundTrip(diagram, () => node.Properties["adhoc"] = "v");
        TestModel.AssertRoundTrip(diagram, () => node.Properties["adhoc"] = "w");
    }

    /// <summary>
    ///     Validates that PropertyChanged carries the property name and the indexer notification on set and undo.
    /// </summary>
    [Fact]
    public void PropertyBag_PropertyChanged_RaisedWithNameAndIndexerOnSetAndUndo()
    {
        // Arrange
        var (diagram, node) = Create();
        var names = new List<string?>();
        ((INotifyPropertyChanged)node.Properties).PropertyChanged += (_, e) => names.Add(e.PropertyName);

        // Act
        node.Properties["Gain"] = 2.0;
        diagram.History.Undo();

        // Assert
        Assert.Equal(new[] { "Gain", "Item[]", "Gain", "Item[]" }, names);
    }

    /// <summary>
    ///     Validates that a property change also raises the node's PropertyChanged and the diagram's Changed event.
    /// </summary>
    [Fact]
    public void PropertyBag_Set_RaisesNodeAndDiagramChangedEvents()
    {
        // Arrange
        var (diagram, node) = Create();
        var nodeEvents = new List<string?>();
        var diagramEvents = new List<DiagramChangedEventArgs>();
        node.PropertyChanged += (_, e) => nodeEvents.Add(e.PropertyName);
        diagram.Changed += (_, e) => diagramEvents.Add(e);

        // Act
        node.Properties["Count"] = 5;

        // Assert
        Assert.Contains("Properties", nodeEvents);
        var change = Assert.Single(diagramEvents);
        Assert.Equal(DiagramChangeKind.ElementChanged, change.Kind);
        Assert.Same(node, change.Element);
        Assert.Equal("Properties", change.PropertyName);
    }

    /// <summary>
    ///     Validates that repeated sets of one property inside a transaction merge into one entry.
    /// </summary>
    [Fact]
    public void PropertyBag_Set_RepeatedInTransaction_MergesIntoOneEntry()
    {
        // Arrange
        var (diagram, node) = Create();
        var entries = diagram.History.UndoCount;

        // Act: simulate a slider
        using (diagram.History.BeginTransaction("Slide"))
        {
            for (var i = 1; i <= 20; i++)
            {
                node.Properties["Gain"] = (double)i;
            }
        }

        // Assert: one entry, undo goes back to the original value
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        Assert.Equal(20.0, node.Properties["Gain"]);
        diagram.History.Undo();
        Assert.Equal(1.0, node.Properties["Gain"]);
    }

    /// <summary>
    ///     Validates that the node's default summary lists ShowInSummary properties with unit and that a custom summary wins.
    /// </summary>
    [Fact]
    public void NodeType_GetSummaryLines_DefaultAndCustom()
    {
        // Arrange
        var (_, node) = Create();
        node.Properties["Gain"] = 2.5;
        var custom = new NodeType("t.Custom") { Summary = n => [$"name={n.Name}"] };

        // Act / Assert
        Assert.Equal(new[] { "Format: F32", "Gain: 2.5 x" }, node.Type.GetSummaryLines(node));
        Assert.Equal(new[] { "name=Producer1" }, custom.GetSummaryLines(node));
    }

    /// <summary>
    ///     Validates that mutating a property of a removed node is rejected.
    /// </summary>
    [Fact]
    public void PropertyBag_Set_OnRemovedNode_ThrowsInvalidOperationException()
    {
        // Arrange
        var (_, node) = Create();
        node.Remove();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => node.Properties["Gain"] = 3.0);
    }
}
