using System.Globalization;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Unit tests of the PropertyModel unit: values, text conversion, standard values, expandable objects, collections
///     and validation.
/// </summary>
public class PropertyModelTests
{
    /// <summary>
    ///     Reading and writing through an item reaches the underlying property.
    /// </summary>
    [Fact]
    public void PropertyItem_GetValueSetValue_ReadAndWriteTheTarget()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var name = TestHelpers.Item(source, "Name");

        name.SetValue("Ada");

        Assert.Equal("Ada", person.Name);
        Assert.Equal("Ada", name.GetValue());
        Assert.True(name.TryGetValue(out var value).Succeeded);
        Assert.Equal("Ada", value);
        Assert.Equal("Name", name.Path);
        Assert.Equal(typeof(string), name.PropertyType);
    }

    /// <summary>
    ///     Numbers are formatted and parsed with the culture passed in, and the current culture is the default.
    /// </summary>
    [Fact]
    public void PropertyItem_TextConversion_UsesTheRequestedCultureAndDefaultsToCurrent()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var ratio = TestHelpers.Item(source, "Ratio");
        var german = new CultureInfo("de-DE");

        Assert.True(ratio.TrySetText("1,5", german).Succeeded);
        Assert.Equal(1.5, person.Ratio);
        Assert.Equal("1,5", ratio.GetText(german));
        Assert.Equal("1.5", ratio.GetText(CultureInfo.InvariantCulture));
        Assert.Equal("1.5", ratio.ConvertToString(1.5, CultureInfo.InvariantCulture));
        Assert.Equal(2.25, ratio.ConvertFromString("2.25", CultureInfo.InvariantCulture));

        TestHelpers.WithCulture("de-DE", () =>
        {
            Assert.Equal("1,5", ratio.GetText());
            Assert.Equal(3.5, ratio.ConvertFromString("3,5"));
        });
        TestHelpers.WithCulture("en-US", () => Assert.Equal("1.5", ratio.GetText()));
    }

    /// <summary>
    ///     A custom converter receives the culture and a round trip preserves the value.
    /// </summary>
    [Fact]
    public void PropertyItem_CustomConverter_RoundTripsWithCulture()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var item = TestHelpers.Item(source, "Temperature");
        var german = new CultureInfo("de-DE");

        Assert.True(item.TrySetText("21,5°", german).Succeeded);

        Assert.Equal(new Temperature(21.5), person.Temperature);
        Assert.Equal("21,5°", item.GetText(german));
        Assert.True(item.SupportsTextEditing);
    }

    /// <summary>
    ///     Text that the converter rejects is reported as an error, leaves the value unchanged and never throws.
    /// </summary>
    [Fact]
    public void PropertyItem_TrySetText_InvalidTextFailsWithoutChangingTheValue()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Age = 7;
        var age = TestHelpers.Item(source, "Age");

        var result = age.TrySetText("abc", CultureInfo.InvariantCulture);
        var parsed = age.TryParse("12x", CultureInfo.InvariantCulture, out var value);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.False(parsed.Succeeded);
        Assert.Null(value);
        Assert.Equal(7, person.Age);
        Assert.ThrowsAny<Exception>(() => age.ConvertFromString("abc", CultureInfo.InvariantCulture));
        Assert.Throws<ArgumentNullException>(() => age.ConvertFromString(null!));
    }

    /// <summary>
    ///     Null values are empty text, an empty nullable is null, and an object-typed property takes the text as is.
    /// </summary>
    [Fact]
    public void PropertyItem_TextConversion_HandlesNullNullableAndObject()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Limit = 4;
        var limit = TestHelpers.Item(source, "Limit");
        var anything = TestHelpers.Item(source, "Anything");

        Assert.Equal("4", limit.GetText(CultureInfo.InvariantCulture));
        Assert.True(limit.TrySetText(string.Empty, CultureInfo.InvariantCulture).Succeeded);
        Assert.Null(person.Limit);
        Assert.Equal(string.Empty, limit.GetText());
        Assert.Equal(string.Empty, limit.ConvertToString(null));
        Assert.True(anything.TrySetText("free text").Succeeded);
        Assert.Equal("free text", person.Anything);
        Assert.True(anything.SupportsTextEditing);
    }

    /// <summary>
    ///     A type whose converter has no text form cannot be edited as text and reports it.
    /// </summary>
    [Fact]
    public void PropertyItem_TextConversion_UnsupportedTypeReportsAnError()
    {
        using var source = TestHelpers.PersonSource(out _);
        var address = TestHelpers.Item(source, "Address");
        var tags = TestHelpers.Item(source, "Tags");

        Assert.False(address.SupportsTextEditing);
        Assert.False(tags.SupportsTextEditing);
        Assert.Throws<NotSupportedException>(() => address.ConvertFromString("x"));
        Assert.False(address.TrySetText("x").Succeeded);
        Assert.Equal(", Paris", address.GetText());
    }

    /// <summary>
    ///     Enumerations offer their members as an exclusive list; flags enumerations are not exclusive; booleans offer
    ///     True and False.
    /// </summary>
    [Fact]
    public void PropertyItem_StandardValues_EnumFlagsAndBoolean()
    {
        using var source = TestHelpers.PersonSource(out _);
        var units = TestHelpers.Item(source, "Units");
        var options = TestHelpers.Item(source, "Options");
        var active = TestHelpers.Item(source, "Active");
        var name = TestHelpers.Item(source, "Name");

        Assert.True(units.HasStandardValues);
        Assert.True(units.StandardValuesExclusive);
        Assert.Equal(["Meters", "Feet", "Miles"], units.GetStandardValues().Select(v => v.Text));
        Assert.Equal(Unit.Feet, units.GetStandardValues()[1].Value);
        Assert.True(options.HasStandardValues);
        Assert.False(options.StandardValuesExclusive);
        Assert.Equal(["True", "False"], active.GetStandardValues(CultureInfo.InvariantCulture).Select(v => v.Text));
        Assert.True(active.StandardValuesExclusive);
        Assert.False(name.HasStandardValues);
        Assert.False(name.StandardValuesExclusive);
        Assert.Empty(name.GetStandardValues());
    }

    /// <summary>
    ///     A converter can offer standard values that depend on the edited instance, and they stay editable text.
    /// </summary>
    [Fact]
    public void PropertyItem_StandardValues_CanDependOnTheInstance()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Name = "Ada";
        var choice = TestHelpers.Item(source, "Choice");

        Assert.Equal(["first-Ada", "second"], choice.GetStandardValues().Select(v => v.Text));
        Assert.False(choice.StandardValuesExclusive);
        Assert.True(choice.TrySetText("custom").Succeeded);
        Assert.Equal("custom", person.Choice);
    }

    /// <summary>
    ///     An enumeration is edited by text (member name) and rejects unknown names.
    /// </summary>
    [Fact]
    public void PropertyItem_Enum_ParsesMemberNames()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var units = TestHelpers.Item(source, "Units");

        Assert.True(units.TrySetText("Miles").Succeeded);
        Assert.Equal(Unit.Miles, person.Units);
        Assert.False(units.TrySetText("Furlongs").Succeeded);
        Assert.Equal(Unit.Miles, person.Units);
    }

    /// <summary>
    ///     An expandable reference object exposes its properties as children with nested paths, and edits reach the object.
    /// </summary>
    [Fact]
    public void PropertyItem_Children_ExpandableObjectExposesNestedProperties()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var address = TestHelpers.Item(source, "Address");

        Assert.True(address.IsExpandable);
        Assert.Equal(["City", "Street"], address.Children.Select(c => c.Name).Order());
        var city = address.Children.Single(c => c.Name == "City");
        Assert.Equal("Address.City", city.Path);
        Assert.Same(address, city.Parent);
        Assert.True(city.TrySetText("Rome").Succeeded);
        Assert.Equal("Rome", person.Address.City);
        Assert.Same(city, address.Children.Single(c => c.Name == "City"));
        Assert.True(city.HasDefault);
        Assert.True(city.IsChanged);
        Assert.False(TestHelpers.Item(source, "Name").IsExpandable);
        Assert.Empty(TestHelpers.Item(source, "Name").Children);
    }

    /// <summary>
    ///     Children are re-created when the value is replaced and empty when the value is null.
    /// </summary>
    [Fact]
    public void PropertyItem_Children_FollowTheCurrentValue()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var address = TestHelpers.Item(source, "Address");
        var before = address.Children;

        person.Address = new Address { City = "Oslo" };
        var after = address.Children;
        var city = after.Single(c => c.Name == "City").GetValue();
        person.Address = null!;

        Assert.NotSame(before, after);
        Assert.Equal("Oslo", city);
        Assert.Empty(address.Children);
    }

    /// <summary>
    ///     Editing a child of an expandable value type writes the modified struct back to the property.
    /// </summary>
    [Fact]
    public void PropertyItem_Children_ValueTypeParentReceivesTheModifiedStruct()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var size = TestHelpers.Item(source, "Size");
        var width = size.Children.Single(c => c.Name == "Width");

        Assert.True(width.TrySetText("4.5", CultureInfo.InvariantCulture).Succeeded);

        Assert.Equal(4.5, person.Size.Width);
        Assert.Equal(0, person.Size.Height);
        Assert.Equal("Size.Width", width.Path);
    }

    /// <summary>
    ///     The children of a read-only value-type property are read-only because they cannot be written back.
    /// </summary>
    [Fact]
    public void PropertyItem_Children_ReadOnlyValueTypeHasReadOnlyChildren()
    {
        var holder = new ReadOnlySizeHolder();
        using var source = new ReflectionPropertySource(holder);
        var size = TestHelpers.Item(source, "Size");

        Assert.True(size.IsReadOnly);
        Assert.All(size.Children, c => Assert.True(c.IsReadOnly));
        Assert.False(size.Children[0].TrySetText("1").Succeeded);
    }

    /// <summary>
    ///     An object-typed property expands according to the converter of the value it holds.
    /// </summary>
    [Fact]
    public void PropertyItem_Children_ObjectTypedPropertyUsesTheValueConverter()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var anything = TestHelpers.Item(source, "Anything");
        Assert.False(anything.IsExpandable);

        person.Anything = new Address { City = "Bern" };

        Assert.True(anything.IsExpandable);
        Assert.Equal("Bern", anything.Children.Single(c => c.Name == "City").GetValue());
    }

    /// <summary>
    ///     Lists, arrays, sets and read-only lists are collections with an element type; strings and dictionaries are not.
    /// </summary>
    [Fact]
    public void PropertyItem_IsCollection_RecognizesCollectionTypes()
    {
        using var source = TestHelpers.PersonSource(out _);

        Assert.True(TestHelpers.Item(source, "Notes").IsCollection);
        Assert.Equal(typeof(string), TestHelpers.Item(source, "Notes").CollectionItemType);
        Assert.Equal(typeof(Tag), TestHelpers.Item(source, "Tags").CollectionItemType);
        Assert.Equal(typeof(int), TestHelpers.Item(source, "Scores").CollectionItemType);
        Assert.Equal(typeof(string), TestHelpers.Item(source, "Labels").CollectionItemType);
        Assert.Equal(typeof(string), TestHelpers.Item(source, "History").CollectionItemType);
        Assert.False(TestHelpers.Item(source, "Name").IsCollection);
        Assert.Null(TestHelpers.Item(source, "Name").CollectionItemType);
        Assert.False(TestHelpers.Item(source, "Map").IsCollection);
        Assert.Empty(TestHelpers.Item(source, "Name").GetCollectionItems());
        Assert.Empty(TestHelpers.Item(source, "Name").GetCollectionValues());
    }

    /// <summary>
    ///     Collection element items read and write their element and have indexed paths.
    /// </summary>
    [Fact]
    public void PropertyItem_GetCollectionItems_ReadAndWriteElements()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Scores = [10, 20];
        person.Notes.AddRange(["a", "b"]);

        var scores = TestHelpers.Item(source, "Scores").GetCollectionItems();
        var notes = TestHelpers.Item(source, "Notes").GetCollectionItems();

        Assert.Equal(["Scores[0]", "Scores[1]"], scores.Select(i => i.Path));
        Assert.Equal(20, scores[1].GetValue());
        Assert.True(scores[1].TrySetText("25").Succeeded);
        Assert.Equal(25, person.Scores[1]);
        Assert.True(notes[0].TrySetText("z").Succeeded);
        Assert.Equal(["z", "b"], person.Notes);
        Assert.Equal(typeof(int), scores[0].PropertyType);
    }

    /// <summary>
    ///     The elements of a read-only list and of a set cannot be replaced in place.
    /// </summary>
    [Fact]
    public void PropertyItem_GetCollectionItems_ReadOnlyCollectionElementsAreReadOnly()
    {
        using var source = TestHelpers.PersonSource(out _);

        var history = TestHelpers.Item(source, "History").GetCollectionItems();

        Assert.Equal(2, history.Count);
        Assert.True(history[0].IsReadOnly);
        Assert.Equal("x", history[0].GetValue());
        Assert.False(history[0].TrySetText("q").Succeeded);
    }

    /// <summary>
    ///     An expandable collection element exposes its properties under an indexed path.
    /// </summary>
    [Fact]
    public void PropertyItem_GetCollectionItems_ExpandableElementHasChildren()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Tags.Add(new Tag { Label = "red" });

        var element = TestHelpers.Item(source, "Tags").GetCollectionItems().Single();
        var label = element.Children.Single();

        Assert.True(element.IsExpandable);
        Assert.Equal("Tags[0].Label", label.Path);
        Assert.True(label.TrySetText("blue").Succeeded);
        Assert.Equal("blue", person.Tags[0].Label);
        Assert.Equal("blue", element.GetText());
    }

    /// <summary>
    ///     A collection shows the collection text unless its converter supplies one.
    /// </summary>
    [Fact]
    public void PropertyItem_ConvertToString_CollectionShowsCollectionText()
    {
        using var source = TestHelpers.PersonSource(out _);

        Assert.Equal("(Collection)", TestHelpers.Item(source, "Notes").GetText());
        Assert.Equal("(Collection)", TestHelpers.Item(source, "Labels").GetText());
    }

    /// <summary>
    ///     A null value, a wrongly typed value, a failed validation annotation and a throwing setter are errors, not exceptions.
    /// </summary>
    [Fact]
    public void PropertyItem_TrySetValue_ReportsValidationAndSetterFailures()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var age = TestHelpers.Item(source, "Age");
        var percent = TestHelpers.Item(source, "Percent");
        var fragile = TestHelpers.Item(source, "Fragile");

        Assert.Equal("'Age' cannot be empty.", age.TrySetValue(null).Error);
        Assert.False(age.TrySetValue("text").Succeeded);
        Assert.Equal("Percent must be between 0 and 100.", percent.TrySetValue(101).Error);
        Assert.Equal("Percent must be between 0 and 100.", percent.TrySetText("-1").Error);
        Assert.True(percent.TrySetValue(50).Succeeded);
        Assert.Equal(50, person.Percent);
        Assert.Equal("Fragile does not accept values.", fragile.TrySetValue(3).Error);
        Assert.Equal("Fragile does not accept values.", fragile.TrySetText("3").Error);
        Assert.True(TestHelpers.Item(source, "Limit").TrySetValue(null).Succeeded);
        Assert.False(age.Validate("text").Succeeded);
        Assert.True(age.Validate(3).Succeeded);
    }

    /// <summary>
    ///     Writing a read-only item fails with a message.
    /// </summary>
    [Fact]
    public void PropertyItem_TrySetValue_ReadOnlyItemFails()
    {
        using var source = TestHelpers.PersonSource(out var person);

        var result = TestHelpers.Item(source, "Id").TrySetValue("changed");

        Assert.False(result.Succeeded);
        Assert.Equal("'Id' is read-only.", result.Error);
        Assert.Equal("P-1", person.Id);
    }

    /// <summary>
    ///     A getter that throws is reported as an error and reads as empty text.
    /// </summary>
    [Fact]
    public void PropertyItem_TryGetValue_GetterFailureIsCaptured()
    {
        var item = new CustomPropertyItem("Broken", typeof(int), () => throw new InvalidOperationException("no value"));

        var result = item.TryGetValue(out var value);

        Assert.False(result.Succeeded);
        Assert.Equal("no value", result.Error);
        Assert.Null(value);
        Assert.Equal(string.Empty, item.GetText());
        Assert.False(item.IsChanged);
        Assert.Empty(item.GetCollectionValues());
    }

    /// <summary>
    ///     A property result exposes success and failure.
    /// </summary>
    [Fact]
    public void PropertyResult_SuccessAndFailure_CarryTheOutcome()
    {
        Assert.True(PropertyResult.Success.Succeeded);
        Assert.Null(PropertyResult.Success.Error);
        var failure = PropertyResult.Failure("bad");
        Assert.False(failure.Succeeded);
        Assert.Equal("bad", failure.Error);
    }
}

/// <summary>
///     Holds a struct property without a setter.
/// </summary>
public sealed class ReadOnlySizeHolder
{
    /// <summary>Gets the size.</summary>
    public Dimensions Size { get; } = new() { Width = 1, Height = 2 };
}
