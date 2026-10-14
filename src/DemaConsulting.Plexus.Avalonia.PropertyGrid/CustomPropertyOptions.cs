using System.ComponentModel;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     The optional metadata of a <see cref="CustomPropertyItem"/>. Every member is optional; unset members have the
///     same defaults as an unannotated reflection property.
/// </summary>
/// <example>
///     <code><![CDATA[
///     var options = new CustomPropertyOptions
///     {
///         DisplayName = "Gain (dB)",
///         Category = "Audio",
///         HasDefault = true,
///         DefaultValue = 0.0,
///     };
///     ]]></code>
/// </example>
public sealed record CustomPropertyOptions
{
    /// <summary>
    ///     Gets the display name; <see langword="null"/> means the property name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    ///     Gets the category; <see langword="null"/> means <see cref="PropertyItem.DefaultCategory"/>.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    ///     Gets the rank of the item's category when a grid sorts by category (see <see cref="PropertyItem.CategoryOrder"/>); the
    ///     default 0 means alphabetical order only. A negative rank puts a category before the others.
    /// </summary>
    public int CategoryOrder { get; init; }

    /// <summary>
    ///     Gets the description; <see langword="null"/> means none.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the item is read-only even though a setter was supplied.
    /// </summary>
    public bool ForceReadOnly { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the item is shown in a grid; defaults to <see langword="true"/>.
    /// </summary>
    public bool IsBrowsable { get; init; } = true;

    /// <summary>
    ///     Gets a value indicating whether the value should be masked.
    /// </summary>
    public bool IsPasswordText { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the name is shown in parentheses and sorted first.
    /// </summary>
    public bool IsParenthesized { get; init; }

    /// <summary>
    ///     Gets what must be refreshed after the value changes.
    /// </summary>
    public RefreshProperties RefreshMode { get; init; } = RefreshProperties.None;

    /// <summary>
    ///     Gets the custom editor key (see <see cref="PropertyEditorAttribute"/>).
    /// </summary>
    public string? EditorKey { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the item has a default value, which enables <see cref="PropertyItem.IsChanged"/>.
    /// </summary>
    public bool HasDefault { get; init; }

    /// <summary>
    ///     Gets the default value, used when <see cref="HasDefault"/> is <see langword="true"/>.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    ///     Gets the type converter; <see langword="null"/> means the converter of the property type.
    /// </summary>
    public TypeConverter? Converter { get; init; }
}
