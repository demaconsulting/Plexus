using System.ComponentModel;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Unit tests of the PropertyModel unit: the System.ComponentModel annotations are mapped onto property items.
/// </summary>
public class PropertyAnnotationTests
{
    /// <summary>
    ///     An unannotated property is in "Misc" and a <c>[Category]</c> annotation is honored.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_Category_DefaultsToMiscAndHonorsAnnotation()
    {
        using var source = TestHelpers.PersonSource(out _);

        Assert.Equal("Misc", TestHelpers.Item(source, "Nickname").Category);
        Assert.Equal(PropertyItem.DefaultCategory, TestHelpers.Item(source, "Nickname").Category);
        Assert.Equal("Identity", TestHelpers.Item(source, "Name").Category);
    }

    /// <summary>
    ///     <c>[DisplayName]</c> and <c>[Description]</c> are honored and the name is the default display name.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_DisplayNameAndDescription_AreHonored()
    {
        using var source = TestHelpers.PersonSource(out _);
        var name = TestHelpers.Item(source, "Name");

        Assert.Equal("Name", name.Name);
        Assert.Equal("Full Name", name.DisplayName);
        Assert.Equal("The full name.", name.Description);
        Assert.Equal("Nickname", TestHelpers.Item(source, "Nickname").DisplayName);
        Assert.Equal(string.Empty, TestHelpers.Item(source, "Nickname").Description);
    }

    /// <summary>
    ///     <c>[Display]</c> supplies name, description and group when the component model attributes are absent.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_DisplayAttribute_SuppliesNameDescriptionAndGroup()
    {
        using var source = TestHelpers.PersonSource(out _);
        var postal = TestHelpers.Item(source, "PostalCode");

        Assert.Equal("Postal Code", postal.DisplayName);
        Assert.Equal("Code used for delivery.", postal.Description);
        Assert.Equal("Postal", postal.Category);
    }

    /// <summary>
    ///     <c>[ReadOnly]</c>, a missing setter and <c>[Editable(false)]</c> make an item read-only.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_ReadOnly_AnnotationGetterOnlyAndEditable()
    {
        using var source = TestHelpers.PersonSource(out _);

        Assert.True(TestHelpers.Item(source, "Id").IsReadOnly);
        Assert.True(TestHelpers.Item(source, "Summary").IsReadOnly);
        Assert.True(TestHelpers.Item(source, "Locked").IsReadOnly);
        Assert.False(TestHelpers.Item(source, "Name").IsReadOnly);
    }

    /// <summary>
    ///     <c>[PasswordPropertyText]</c> marks an item as masked and <c>[ParenthesizePropertyName]</c> adds parentheses
    ///     to its label.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_PasswordAndParenthesize_AreHonored()
    {
        using var source = TestHelpers.PersonSource(out _);

        Assert.True(TestHelpers.Item(source, "Password").IsPasswordText);
        Assert.False(TestHelpers.Item(source, "Name").IsPasswordText);
        var handle = TestHelpers.Item(source, "Handle");
        Assert.True(handle.IsParenthesized);
        Assert.Equal("(Handle)", handle.Label);
        Assert.Equal("Full Name", TestHelpers.Item(source, "Name").Label);
    }

    /// <summary>
    ///     <c>[RefreshProperties]</c> is exposed as the item's refresh mode.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_RefreshProperties_IsExposed()
    {
        using var source = TestHelpers.PersonSource(out _);

        Assert.Equal(RefreshProperties.All, TestHelpers.Item(source, "Units").RefreshMode);
        Assert.Equal(RefreshProperties.None, TestHelpers.Item(source, "Name").RefreshMode);
    }

    /// <summary>
    ///     An item with <c>[DefaultValue]</c> is changed exactly when its value differs from the default; an item
    ///     without any default is never changed.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_DefaultValue_IsChangedTracksTheCurrentValue()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var age = TestHelpers.Item(source, "Age");
        var nickname = TestHelpers.Item(source, "Nickname");

        Assert.True(age.HasDefault);
        Assert.True(age.IsDefaultValueKnown);
        Assert.Equal(0, age.DefaultValue);
        Assert.False(age.IsChanged);

        person.Age = 3;
        person.Nickname = "Ace";
        Assert.True(age.IsChanged);
        Assert.False(nickname.HasDefault);
        Assert.False(nickname.IsChanged);
    }

    /// <summary>
    ///     The <c>ShouldSerialize&lt;Name&gt;</c> convention makes an item changed without a known default value.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_ShouldSerializePattern_DrivesIsChanged()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var level = TestHelpers.Item(source, "Level");

        Assert.True(level.HasDefault);
        Assert.False(level.IsDefaultValueKnown);
        Assert.False(level.IsChanged);

        person.Level = 9;
        Assert.True(level.IsChanged);
    }

    /// <summary>
    ///     Reset restores the default (<c>[DefaultValue]</c> or <c>Reset&lt;Name&gt;</c>) and is unavailable for
    ///     items without a default.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_Reset_RestoresTheDefaultValue()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Age = 8;
        person.Level = 1;
        var age = TestHelpers.Item(source, "Age");
        var level = TestHelpers.Item(source, "Level");

        Assert.True(age.CanReset);
        Assert.True(age.TryReset().Succeeded);
        Assert.True(level.TryReset().Succeeded);

        Assert.Equal(0, person.Age);
        Assert.Equal(5, person.Level);
        Assert.False(TestHelpers.Item(source, "Nickname").CanReset);
        Assert.False(TestHelpers.Item(source, "Nickname").TryReset().Succeeded);
    }

    /// <summary>
    ///     A type converter given by annotation on the property is used for conversion.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_TypeConverterAnnotation_IsUsed()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Temperature = new Temperature(12.5);

        var item = TestHelpers.Item(source, "Temperature");

        Assert.IsType<TemperatureConverter>(item.Converter);
        Assert.Equal("12.5°", item.GetText(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///     <see cref="PropertyEditorAttribute"/> on a property or on its type sets the editor key; the property wins.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_PropertyEditorAnnotation_OnPropertyAndType()
    {
        using var persons = TestHelpers.PersonSource(out _);
        using var holder = new ReflectionPropertySource(new EditorHolder());

        Assert.Equal("color", TestHelpers.Item(persons, "Tint").EditorKey);
        Assert.Null(TestHelpers.Item(persons, "Name").EditorKey);
        Assert.Equal("swatch", TestHelpers.Item(holder, "Primary").EditorKey);
        Assert.Null(TestHelpers.Item(holder, "Primary").EditorType);
        Assert.Equal(typeof(EditorHolder), TestHelpers.Item(holder, "Secondary").EditorType);
    }

    /// <summary>
    ///     A <see cref="System.ComponentModel.DataAnnotations.UIHintAttribute"/> names the editor key when the property
    ///     has no <see cref="PropertyEditorAttribute"/>; the property-level editor annotation wins, a hint for another
    ///     presentation layer is ignored, and a property hint wins over a type-level editor annotation.
    /// </summary>
    [Fact]
    public void PropertyItem_Annotation_UIHint_SuppliesTheEditorKey()
    {
        using var holder = new ReflectionPropertySource(new EditorHolder());

        Assert.Equal("slider", TestHelpers.Item(holder, "Level").EditorKey);
        Assert.Null(TestHelpers.Item(holder, "Level").EditorType);
        Assert.Equal("hinted", TestHelpers.Item(holder, "Hinted").EditorKey);
        Assert.Equal("winner", TestHelpers.Item(holder, "Both").EditorKey);
        Assert.Equal("avalonia-hint", TestHelpers.Item(holder, "Layers").EditorKey);
        Assert.Null(TestHelpers.Item(holder, "OtherLayer").EditorKey);
    }
}
