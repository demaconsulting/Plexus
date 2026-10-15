using System.Globalization;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Additional unit tests for <see cref="PropertyDefinition"/> (value normalization, presentation hints and formatting).
///     The kind inference, choice and summary tests of the same unit live in <see cref="PropertyBagTests"/>.
/// </summary>
public class PropertyDefinitionTests
{
    /// <summary>
    ///     Validates that integral and decimal numeric defaults widen to the canonical int, long and double kinds.
    /// </summary>
    [Fact]
    public void PropertyDefinition_Constructor_WideNumericTypes_NormalizeToCanonicalKinds()
    {
        // Act
        var fromByte = new PropertyDefinition("A", (byte)3);
        var fromUInt = new PropertyDefinition("B", 3u);
        var fromULong = new PropertyDefinition("C", 3UL);
        var fromDecimal = new PropertyDefinition("D", 2.5m);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(PropertyValueKind.Int32, fromByte.ValueKind),
            () => Assert.IsType<int>(fromByte.DefaultValue),
            () => Assert.Equal(PropertyValueKind.Int64, fromUInt.ValueKind),
            () => Assert.IsType<long>(fromUInt.DefaultValue),
            () => Assert.Equal(PropertyValueKind.Int64, fromULong.ValueKind),
            () => Assert.Equal(3L, fromULong.DefaultValue),
            () => Assert.Equal(PropertyValueKind.Double, fromDecimal.ValueKind),
            () => Assert.Equal(2.5, fromDecimal.DefaultValue));
    }

    /// <summary>
    ///     Validates that numbers outside the portable range or not finite are rejected as defaults.
    /// </summary>
    [Fact]
    public void PropertyDefinition_Constructor_OutOfRangeOrNonFiniteNumbers_ThrowArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", ulong.MaxValue));
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", double.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", float.NaN));
        Assert.Throws<ArgumentException>(() => new PropertyDefinition("P", 'c'));
    }

    /// <summary>
    ///     Validates that a definition created with only a name and default carries empty presentation hints.
    /// </summary>
    [Fact]
    public void PropertyDefinition_Presentation_Defaults_AreEmpty()
    {
        // Act
        var definition = new PropertyDefinition("Gain", 1.0);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Gain", definition.Name),
            () => Assert.Equal("Gain", definition.DisplayName),
            () => Assert.Null(definition.Description),
            () => Assert.Null(definition.Category),
            () => Assert.Equal(0, definition.Order),
            () => Assert.Null(definition.Unit),
            () => Assert.Null(definition.EditorHint),
            () => Assert.Null(definition.Format),
            () => Assert.False(definition.ShowInSummary),
            () => Assert.False(definition.IsReadOnly));
    }

    /// <summary>
    ///     Validates that explicit presentation hints are stored unchanged.
    /// </summary>
    [Fact]
    public void PropertyDefinition_Presentation_ExplicitHints_AreStored()
    {
        // Act
        var definition = new PropertyDefinition("Gain", 1.0)
        {
            DisplayName = "Gain Factor",
            Description = "Output gain",
            Category = "Signal",
            Order = 3,
            Unit = "dB",
            EditorHint = "slider",
            ShowInSummary = true,
            IsReadOnly = true,
        };

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Gain Factor", definition.DisplayName),
            () => Assert.Equal("Output gain", definition.Description),
            () => Assert.Equal("Signal", definition.Category),
            () => Assert.Equal(3, definition.Order),
            () => Assert.Equal("dB", definition.Unit),
            () => Assert.Equal("slider", definition.EditorHint),
            () => Assert.True(definition.ShowInSummary),
            () => Assert.True(definition.IsReadOnly),
            () => Assert.Equal("Gain", definition.Name));
    }

    /// <summary>
    ///     Validates that the choice list is copied so later changes to the caller's list do not alter the definition.
    /// </summary>
    [Fact]
    public void PropertyDefinition_Choices_SourceListChangedLater_DefinitionKeepsCopy()
    {
        // Arrange
        var source = new List<string> { "A", "B" };
        var definition = new PropertyDefinition("Mode", "A") { Choices = source };

        // Act
        source.Add("C");

        // Assert
        Assert.Equal(new[] { "A", "B" }, definition.Choices);
    }

    /// <summary>
    ///     Validates that values are formatted with the invariant culture and that missing values format as empty text.
    /// </summary>
    [Fact]
    public void PropertyDefinition_FormatValue_UsesInvariantCulture()
    {
        // Arrange: a culture that would format 1.5 as "1,5"
        var original = CultureInfo.CurrentCulture;
        var definition = new PropertyDefinition("Gain", 1.5);
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            // Act
            var number = definition.FormatValue(1.5);
            var flag = definition.FormatValue(true);
            var color = definition.FormatValue(Color.Parse("#112233"));
            var none = definition.FormatValue(null);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("1.5", number),
                () => Assert.Equal("True", flag),
                () => Assert.Equal("#112233", color),
                () => Assert.Equal(string.Empty, none));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    ///     Validates that a summary line without a unit has no trailing text and that the unit is appended when set.
    /// </summary>
    [Fact]
    public void PropertyDefinition_FormatSummary_WithAndWithoutUnit_AppendsUnitOnlyWhenSet()
    {
        // Arrange
        var plain = new PropertyDefinition("Gain", 1.5);
        var withUnit = new PropertyDefinition("Gain", 1.5) { Unit = "dB" };

        // Act / Assert
        Assert.Equal("Gain: 1.5", plain.FormatSummary(1.5));
        Assert.Equal("Gain: 1.5 dB", withUnit.FormatSummary(1.5));
    }
}
