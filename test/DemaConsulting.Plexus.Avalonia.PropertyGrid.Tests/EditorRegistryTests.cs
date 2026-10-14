namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Unit tests of the EditorRegistry unit: the editor annotation and the registry.
/// </summary>
public class EditorRegistryTests
{
    /// <summary>
    ///     An editor registered under a key is found regardless of case and can be removed.
    /// </summary>
    [Fact]
    public void PropertyEditorRegistry_ResolveByKey_IsCaseInsensitiveAndRemovable()
    {
        var registry = new PropertyEditorRegistry<string>();
        registry.Register("Color-Swatch", "swatch-editor");

        Assert.Equal("swatch-editor", registry.Resolve("color-swatch"));
        Assert.Null(registry.Resolve("other"));
        Assert.True(registry.Unregister("COLOR-SWATCH"));
        Assert.False(registry.Unregister("COLOR-SWATCH"));
        Assert.Null(registry.Resolve("color-swatch"));
        Assert.Throws<ArgumentException>(() => registry.Register(" ", "x"));
        Assert.Throws<ArgumentNullException>(() => registry.Register("k", null!));
    }

    /// <summary>
    ///     Types resolve by exact registration, nearest base type, interface, open generic definition and nullable
    ///     underlying type, and the exact registration wins.
    /// </summary>
    [Fact]
    public void PropertyEditorRegistry_ResolveByType_WalksBasesInterfacesAndGenerics()
    {
        var registry = new PropertyEditorRegistry<string>();
        registry.Register(typeof(Enum), "enum");
        registry.Register(typeof(Unit), "unit");
        registry.Register(typeof(IEnumerable<>), "sequence");
        registry.Register(typeof(int), "number");
        registry.Register(typeof(ICloneable), "cloneable");

        Assert.Equal("unit", registry.Resolve(typeof(Unit)));
        Assert.Equal("enum", registry.Resolve(typeof(Options)));
        Assert.Equal("sequence", registry.Resolve(typeof(List<string>)));
        Assert.Equal("number", registry.Resolve(typeof(int?)));
        Assert.Equal("cloneable", registry.Resolve(typeof(string)));
        Assert.Null(registry.Resolve(typeof(Address)));
        Assert.True(registry.Unregister(typeof(Unit)));
        Assert.False(registry.Unregister(typeof(Unit)));
        Assert.Equal("enum", registry.Resolve(typeof(Unit)));
        Assert.Throws<ArgumentNullException>(() => registry.Resolve((Type)null!));
        Assert.Throws<ArgumentNullException>(() => registry.Register((Type)null!, "x"));
    }

    /// <summary>
    ///     An item resolves by its editor key first, then its type, then the fallback.
    /// </summary>
    [Fact]
    public void PropertyEditorRegistry_ResolveItem_KeyThenTypeThenFallback()
    {
        using var source = TestHelpers.PersonSource(out _);
        var registry = new PropertyEditorRegistry<string>();
        registry.Register("color", "color-editor");
        registry.Register(typeof(string), "text-editor");
        var tint = TestHelpers.Item(source, "Tint");
        var name = TestHelpers.Item(source, "Name");
        var age = TestHelpers.Item(source, "Age");

        Assert.Equal("color-editor", registry.Resolve(tint));
        Assert.Equal("text-editor", registry.Resolve(name));
        Assert.Null(registry.Resolve(age));
        registry.Fallback = "fallback";
        Assert.Equal("fallback", registry.Resolve(age));
        Assert.Equal("text-editor", registry.Resolve(name));
        registry.Unregister("color");
        Assert.Equal("text-editor", registry.Resolve(tint));
        Assert.Throws<ArgumentNullException>(() => registry.Resolve((PropertyItem)null!));
    }

    /// <summary>
    ///     The editor annotation takes a key or an editor type and validates its arguments.
    /// </summary>
    [Fact]
    public void PropertyEditorAttribute_KeyOrType_SetsTheEditorKey()
    {
        var byKey = new PropertyEditorAttribute("knob");
        var byType = new PropertyEditorAttribute(typeof(EditorRegistryTests));

        Assert.Equal("knob", byKey.EditorKey);
        Assert.Null(byKey.EditorType);
        Assert.Equal(typeof(EditorRegistryTests), byType.EditorType);
        Assert.Equal(typeof(EditorRegistryTests).FullName, byType.EditorKey);
        Assert.Throws<ArgumentException>(() => new PropertyEditorAttribute(string.Empty));
        Assert.Throws<ArgumentNullException>(() => new PropertyEditorAttribute((Type)null!));
    }
}
