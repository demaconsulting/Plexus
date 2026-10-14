using System.Globalization;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests.Properties;

/// <summary>
///     Unit tests for the editing helpers: <see cref="PropertyDefinition.TryParse"/>, <see cref="PropertyDefinition.FormatForEdit"/>,
///     <see cref="PropertyBag.TrySetAll"/> and <see cref="Node.CloneData"/>.
/// </summary>
public class PropertyEditingTests
{
    /// <summary>
    ///     Validates that every kind parses valid text into the exact CLR type.
    /// </summary>
    [Fact]
    public void PropertyDefinition_TryParse_ValidText_ProducesTypedValues()
    {
        // Arrange / Act / Assert
        Assert.True(new PropertyDefinition("a", true).TryParse(" TRUE ", out var b, out var error));
        Assert.Null(error);
        Assert.True(b is true);
        Assert.True(new PropertyDefinition("a", 1).TryParse("42", out var i, out _));
        Assert.IsType<int>(i);
        Assert.Equal(42, i);
        Assert.True(new PropertyDefinition("a", 1L).TryParse("-5000000000", out var l, out _));
        Assert.Equal(-5_000_000_000L, l);
        Assert.True(new PropertyDefinition("a", 1.0).TryParse("2.5", out var d, out _));
        Assert.Equal(2.5, d);
        Assert.True(new PropertyDefinition("a", 1.0).TryParse("1e3", out var e, out _));
        Assert.Equal(1000.0, e);
        Assert.True(new PropertyDefinition("a", "x").TryParse("  any text ", out var s, out _));
        Assert.Equal("  any text ", s);
        Assert.True(new PropertyDefinition("a", Color.Black).TryParse("#ff0000", out var c, out _));
        Assert.Equal(Color.Parse("#FF0000"), c);
        Assert.True(new PropertyDefinition("a", Color.Black).TryParse("80112233", out var translucent, out _));
        Assert.Equal(Color.FromArgb(0x80, 0x11, 0x22, 0x33), translucent);
        Assert.True(new PropertyDefinition("a", "one") { Choices = ["one", "Two"] }.TryParse("Two", out var choice, out _));
        Assert.Equal("Two", choice);
    }

    /// <summary>
    ///     Validates the tolerated spellings: alternative booleans, case-insensitive unique choices and a trailing unit.
    /// </summary>
    [Fact]
    public void PropertyDefinition_TryParse_TolerantForms_AreAccepted()
    {
        // Arrange
        var flag = new PropertyDefinition("a", false);
        var choice = new PropertyDefinition("a", "Fast") { Choices = ["Fast", "Slow"] };
        var interval = new PropertyDefinition("a", 100) { Unit = "ms" };
        var gain = new PropertyDefinition("a", 1.0) { Unit = "dB" };

        // Act / Assert
        foreach (var yes in new[] { "yes", "On", "1" })
        {
            Assert.True(flag.TryParse(yes, out var on, out _));
            Assert.True(on is true);
        }

        foreach (var no in new[] { "no", "OFF", "0" })
        {
            Assert.True(flag.TryParse(no, out var off, out _));
            Assert.True(off is false);
        }

        Assert.True(choice.TryParse("slow", out var slow, out _));
        Assert.Equal("Slow", slow);
        Assert.True(interval.TryParse("250 ms", out var ms, out _));
        Assert.Equal(250, ms);
        Assert.True(interval.TryParse("250MS", out var ms2, out _));
        Assert.Equal(250, ms2);
        Assert.True(gain.TryParse("-3.5dB", out var db, out _));
        Assert.Equal(-3.5, db);
    }

    /// <summary>
    ///     Validates that invalid text is refused with a reason that names the text.
    /// </summary>
    /// <param name="kind">Which default value (and so kind) to test with.</param>
    /// <param name="text">The invalid text.</param>
    /// <param name="expected">A fragment the message must contain.</param>
    [Theory]
    [InlineData("bool", "maybe", "true or false")]
    [InlineData("int", "1.5", "whole number")]
    [InlineData("int", "99999999999", "out of range")]
    [InlineData("int", "-99999999999999999999", "out of range")]
    [InlineData("int", "", "whole number")]
    [InlineData("long", "x", "whole number")]
    [InlineData("double", "NaN", "number")]
    [InlineData("double", "Infinity", "number")]
    [InlineData("double", "", "not a number")]
    [InlineData("double", "2,5", "not a number")]
    [InlineData("color", "red", "#RRGGBB")]
    [InlineData("color", "#12345", "#RRGGBB")]
    [InlineData("choice", "three", "allowed choices")]
    public void PropertyDefinition_TryParse_InvalidText_IsRefused(string kind, string text, string expected)
    {
        // Arrange
        PropertyDefinition definition = kind switch
        {
            "bool" => new("a", true),
            "int" => new("a", 1),
            "long" => new("a", 1L),
            "double" => new("a", 1.0),
            "color" => new("a", Color.Black),
            _ => new("a", "one") { Choices = ["one", "two"] },
        };

        // Act
        var ok = definition.TryParse(text, out var value, out var error);

        // Assert
        Assert.False(ok);
        Assert.Null(value);
        Assert.Contains(expected, error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an ambiguous case-insensitive choice is refused, while an exact match wins.
    /// </summary>
    [Fact]
    public void PropertyDefinition_TryParse_Choice_ExactWinsAndAmbiguousIsRefused()
    {
        // Arrange
        var definition = new PropertyDefinition("a", "on") { Choices = ["on", "ON", "off"] };

        // Act / Assert
        Assert.True(definition.TryParse("ON", out var exact, out _));
        Assert.Equal("ON", exact);
        Assert.False(definition.TryParse("On", out _, out var error));
        Assert.Contains("allowed choices", error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that parsing ignores the thread culture.
    /// </summary>
    [Fact]
    public void PropertyDefinition_TryParse_IsCultureInvariant()
    {
        // Arrange
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            // Act
            var ok = new PropertyDefinition("a", 1.0).TryParse("2.5", out var value, out _);
            var comma = new PropertyDefinition("a", 1.0).TryParse("2,5", out _, out _);

            // Assert
            Assert.True(ok);
            Assert.Equal(2.5, value);
            Assert.False(comma);
            Assert.Equal("2.5", PropertyDefinition.FormatForEdit(2.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    ///     Validates that formatting for edit round-trips through TryParse for every kind, including awkward doubles and translucent colors.
    /// </summary>
    [Fact]
    public void PropertyDefinition_FormatForEdit_RoundTripsThroughTryParse()
    {
        // Arrange
        var cases = new (PropertyDefinition Definition, object Value)[]
        {
            (new PropertyDefinition("a", true), false),
            (new PropertyDefinition("a", 1), int.MinValue),
            (new PropertyDefinition("a", 1L), long.MaxValue),
            (new PropertyDefinition("a", 1.0), 0.1 + 0.2),
            (new PropertyDefinition("a", 1.0), 1e-300),
            (new PropertyDefinition("a", 1.0), -2.0),
            (new PropertyDefinition("a", "x"), "text with, punctuation and ünïcode"),
            (new PropertyDefinition("a", Color.Black), Color.FromArgb(0x7F, 1, 2, 3)),
            (new PropertyDefinition("a", Color.Black), Color.Orange),
            (new PropertyDefinition("a", "one") { Choices = ["one", "two"] }, "two"),
        };

        // Act / Assert
        foreach (var (definition, value) in cases)
        {
            var text = PropertyDefinition.FormatForEdit(value);
            Assert.True(definition.TryParse(text, out var parsed, out var error), error);
            Assert.Equal(value, parsed);
        }

        Assert.Equal("true", PropertyDefinition.FormatForEdit(true));
        Assert.Equal("#FF8C00", PropertyDefinition.FormatForEdit(Color.Parse("#FF8C00")));
        Assert.Equal(string.Empty, PropertyDefinition.FormatForEdit(null));
    }

    /// <summary>
    ///     Validates that all valid changes are applied as one undo entry.
    /// </summary>
    [Fact]
    public void PropertyBag_TrySetAll_AllValid_AppliesAsOneUndoEntry()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var node = diagram.Pages.Add("Main").AddNode("acme.Producer", 0, 0);
        var undoCount = diagram.History.UndoCount;
        var changes = new Dictionary<string, object?>
        {
            ["Gain"] = "2.5",
            ["Count"] = 7,
            ["Enabled"] = "no",
            ["Mode"] = "b",
            ["Tint"] = "#445566",
            ["Note"] = "ad hoc",
        };

        // Act
        var ok = node.Properties.TrySetAll(changes, out var errors);

        // Assert
        Assert.True(ok);
        Assert.Empty(errors);
        Assert.Equal(2.5, node.Properties.Get<double>("Gain"));
        Assert.Equal(7, node.Properties.Get<int>("Count"));
        Assert.False(node.Properties.Get<bool>("Enabled"));
        Assert.Equal("B", node.Properties.Get<string>("Mode"));
        Assert.Equal(Color.Parse("#445566"), node.Properties.Get<Color>("Tint"));
        Assert.Equal("ad hoc", node.Properties.Get<string>("Note"));
        Assert.Equal(undoCount + 1, diagram.History.UndoCount);
        Assert.Equal("Edit properties", diagram.History.UndoDescription);
        diagram.History.Undo();
        Assert.Equal(1.0, node.Properties.Get<double>("Gain"));
        Assert.False(node.Properties.ContainsKey("Note"));
    }

    /// <summary>
    ///     Validates that one invalid entry rejects the whole batch, reports every problem and changes nothing.
    /// </summary>
    [Fact]
    public void PropertyBag_TrySetAll_AnyInvalid_ChangesNothingAndReportsAll()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var node = diagram.Pages.Add("Main").AddNode("acme.Producer", 0, 0);
        diagram.History.MarkClean();
        var before = TestModel.Dump(diagram);
        var undoCount = diagram.History.UndoCount;
        var changes = new Dictionary<string, object?>
        {
            ["Gain"] = 9.0,
            ["Count"] = "abc",
            ["Mode"] = "C",
            ["Big"] = null,
            ["Enabled"] = new object(),
        };

        // Act
        var ok = node.Properties.TrySetAll(changes, out var errors);

        // Assert
        Assert.False(ok);
        Assert.Equal(["Big", "Count", "Enabled", "Mode"], errors.Keys.Order());
        Assert.Contains("whole number", errors["Count"], StringComparison.Ordinal);
        Assert.Contains("allowed choices", errors["Mode"], StringComparison.Ordinal);
        Assert.Contains("required", errors["Big"], StringComparison.Ordinal);
        Assert.DoesNotContain("Parameter", errors["Enabled"], StringComparison.Ordinal);
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(undoCount, diagram.History.UndoCount);
        Assert.False(diagram.History.IsDirty);
    }

    /// <summary>
    ///     Validates that unchanged values and an empty batch record no undo entry, and that bad arguments are refused.
    /// </summary>
    [Fact]
    public void PropertyBag_TrySetAll_NoEffectiveChange_RecordsNothing()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0);
        var undoCount = diagram.History.UndoCount;

        // Act
        var same = node.Properties.TrySetAll(new Dictionary<string, object?> { ["Gain"] = "1" }, out _);
        var empty = node.Properties.TrySetAll(new Dictionary<string, object?>(), out var errors);

        // Assert
        Assert.True(same);
        Assert.True(empty);
        Assert.Empty(errors);
        Assert.Equal(undoCount, diagram.History.UndoCount);
        Assert.Throws<ArgumentNullException>(() => node.Properties.TrySetAll(null!, out _));
        node.Remove();
        Assert.Throws<InvalidOperationException>(() => node.Properties.TrySetAll(new Dictionary<string, object?>(), out _));
    }

    /// <summary>
    ///     Validates that an empty property name is reported as an error.
    /// </summary>
    [Fact]
    public void PropertyBag_TrySetAll_EmptyName_IsReported()
    {
        // Arrange
        var node = TestModel.CreateDiagram().Pages.Add("Main").AddNode("acme.Producer", 0, 0);

        // Act
        var ok = node.Properties.TrySetAll(new Dictionary<string, object?> { [""] = 1 }, out var errors);

        // Assert
        Assert.False(ok);
        Assert.Contains("name", Assert.Single(errors).Value, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that CloneData returns an independent deep copy and null when there is no data.
    /// </summary>
    [Fact]
    public void Node_CloneData_ReturnsIndependentCopy()
    {
        // Arrange
        var diagram = Serialization.SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var sensor = page.AddNode("acme.Sensor", 0, 0);
        var custom = page.AddNode("acme.Custom", 200, 0);
        sensor.Data = new Serialization.SensorData("a", 1);
        custom.Data = new Serialization.CustomData("hello");

        // Act
        var sensorCopy = sensor.CloneData();
        var customCopy = custom.CloneData();

        // Assert
        Assert.Equal(sensor.Data, sensorCopy);
        Assert.NotSame(sensor.Data, sensorCopy);
        Assert.Equal(custom.Data, customCopy);
        Assert.NotSame(custom.Data, customCopy);
        Assert.Null(page.AddNode("acme.Producer", 400, 0).CloneData());
        Assert.Null(page.AddNode("acme.Sensor", 600, 0).CloneData());
    }

    /// <summary>
    ///     Validates that data without a way to copy it is reported clearly instead of being shared.
    /// </summary>
    [Fact]
    public void Node_CloneData_TypeWithoutSerializer_Throws()
    {
        // Arrange
        var node = Serialization.SerializationFixtures.CreateDiagram().Pages.Add("Main").AddNode("acme.Opaque", 0, 0);
        node.Data = new object();

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => node.CloneData());
        Assert.Contains("acme.Opaque", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the documented pattern: edited data and property changes commit as one undo entry.
    /// </summary>
    [Fact]
    public void Node_CloneData_CommittedWithPropertiesInOneTransaction_IsOneUndoEntry()
    {
        // Arrange
        var diagram = Serialization.SerializationFixtures.CreateDiagram();
        var node = diagram.Pages.Add("Main").AddNode("acme.Sensor", 0, 0);
        node.Data = new Serialization.SensorData("old", 1);
        node.Properties["Note"] = "x";
        var undoCount = diagram.History.UndoCount;
        var original = node.Data;

        // Act: a dialog edits the copy, then OK commits data and properties together
        var edited = (Serialization.SensorData)node.CloneData()! with { Label = "new" };
        diagram.History.Transact("Edit node", () =>
        {
            node.Properties.TrySetAll(new Dictionary<string, object?> { ["Note"] = "y" }, out _);
            node.Data = edited;
        });

        // Assert
        Assert.Equal(undoCount + 1, diagram.History.UndoCount);
        Assert.Equal(new Serialization.SensorData("new", 1), node.Data);
        diagram.History.Undo();
        Assert.Equal(original, node.Data);
        Assert.Equal("x", node.Properties.Get<string>("Note"));
    }
}
