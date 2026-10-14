namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Requests a custom editor for a property, or for every property of a type, by key or by editor type.
/// </summary>
/// <remarks>
///     <para>
///         This is the Avalonia grid's replacement for the WinForms <c>[Editor(typeof(...), typeof(UITypeEditor))]</c>
///         annotation. WinForms <c>UITypeEditor</c> implementations cannot be reused: they are built on Windows
///         Forms services (<c>IWindowsFormsEditorService</c>) and GDI drawing, none of which exist in Avalonia.
///         The attribute therefore carries only an opaque key (or an editor type); the grid's UI layer registers the
///         Avalonia editors that answer to those keys in a <see cref="PropertyEditorRegistry{TEditor}"/>.
///         A model class that must not reference this assembly can name the key with
///         <c>[System.ComponentModel.DataAnnotations.UIHint("key")]</c> instead; the grid treats it as the editor key
///         of a property that has no <see cref="PropertyEditorAttribute"/> (this attribute wins when both are present).
///     </para>
///     <para>
///         Applied to a property it affects that property; applied to a class, struct or enumeration it affects every
///         property of that type (a property-level annotation wins).
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     public class Appearance
///     {
///         [PropertyEditor("color-swatch")]
///         public string Tint { get; set; } = "#FF8800";
///     }
///     ]]></code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface)]
public sealed class PropertyEditorAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyEditorAttribute"/> class that names the editor by key.
    /// </summary>
    /// <param name="editorKey">The editor key, matched case-insensitively against the registry.</param>
    /// <exception cref="ArgumentException"><paramref name="editorKey"/> is <see langword="null"/> or white space.</exception>
    public PropertyEditorAttribute(string editorKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editorKey);
        EditorKey = editorKey;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyEditorAttribute"/> class that names the editor by type.
    ///     The key is the full name of the type.
    /// </summary>
    /// <param name="editorType">The editor type; the UI layer decides how it is instantiated.</param>
    /// <exception cref="ArgumentNullException"><paramref name="editorType"/> is <see langword="null"/>.</exception>
    public PropertyEditorAttribute(Type editorType)
    {
        ArgumentNullException.ThrowIfNull(editorType);
        EditorType = editorType;
        EditorKey = editorType.FullName ?? editorType.Name;
    }

    /// <summary>
    ///     Gets the editor key.
    /// </summary>
    public string EditorKey { get; }

    /// <summary>
    ///     Gets the editor type when the annotation named one; otherwise <see langword="null"/>.
    /// </summary>
    public Type? EditorType { get; }
}
