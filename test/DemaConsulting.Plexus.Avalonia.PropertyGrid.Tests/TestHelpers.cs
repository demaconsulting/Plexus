using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Shared helpers of the property grid tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    ///     Finds the item of a source by name.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="name">The item name.</param>
    /// <returns>The item.</returns>
    internal static PropertyItem Item(IPropertySource source, string name) => source.GetItems().Single(i => i.Name == name);

    /// <summary>
    ///     Creates a reflection source over a new <see cref="Person"/>, with the person returned through the out parameter.
    /// </summary>
    /// <param name="person">The new person.</param>
    /// <returns>The source.</returns>
    internal static ReflectionPropertySource PersonSource(out Person person)
    {
        person = new Person();
        return new ReflectionPropertySource(person);
    }

    /// <summary>
    ///     Creates an integer item over a field, recording writes in a log.
    /// </summary>
    /// <param name="name">The item name.</param>
    /// <param name="store">The backing store (one-element array).</param>
    /// <param name="log">The write log.</param>
    /// <returns>The item.</returns>
    internal static CustomPropertyItem LoggedInt(string name, int[] store, List<string> log) =>
        new(name, typeof(int), () => store[0], v =>
        {
            store[0] = (int)v!;
            log.Add(name);
        });

    /// <summary>
    ///     Runs an action with a culture as the current culture and restores the previous one.
    /// </summary>
    /// <param name="culture">The culture name.</param>
    /// <param name="action">The action.</param>
    internal static void WithCulture(string culture, Action action)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(culture);
        try
        {
            action();
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}

/// <summary>
///     A class with a default property.
/// </summary>
[DefaultProperty("Title")]
public sealed class Document
{
    /// <summary>Gets or sets the body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;
}

/// <summary>
///     A type that requests a custom editor for every property of its type.
/// </summary>
[PropertyEditor("swatch")]
public sealed class Swatch
{
    /// <summary>Gets or sets the color name.</summary>
    public string Color { get; set; } = "red";
}

/// <summary>
///     Holds properties whose editor comes from the type or from the property.
/// </summary>
public sealed class EditorHolder
{
    /// <summary>Gets or sets a swatch (editor requested by its type).</summary>
    public Swatch Primary { get; set; } = new();

    /// <summary>Gets or sets a swatch whose property overrides the editor of its type.</summary>
    [PropertyEditor(typeof(EditorHolder))]
    public Swatch Secondary { get; set; } = new();

    /// <summary>Gets or sets a number whose editor key comes from a <c>UIHint</c> annotation.</summary>
    [UIHint("slider")]
    public int Level { get; set; }

    /// <summary>Gets or sets a swatch whose <c>UIHint</c> overrides the editor of its type.</summary>
    [UIHint("hinted")]
    public Swatch Hinted { get; set; } = new();

    /// <summary>Gets or sets a swatch with both annotations on the property; <see cref="PropertyEditorAttribute"/> wins.</summary>
    [UIHint("ignored"), PropertyEditor("winner")]
    public Swatch Both { get; set; } = new();

    /// <summary>Gets or sets a value with hints for several presentation layers.</summary>
    [UIHint("mvc-only", "MVC"), UIHint("no-layer"), UIHint("avalonia-hint", "Avalonia")]
    public int Layers { get; set; }

    /// <summary>Gets or sets a value whose only hint addresses another presentation layer.</summary>
    [UIHint("mvc-only", "MVC")]
    public int OtherLayer { get; set; }
}
