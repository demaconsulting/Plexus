using System.ComponentModel;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     A property item whose value is read and written through delegates instead of reflection, for hosts (and adapters
///     such as the Plexus node adapter) that supply items themselves.
/// </summary>
/// <remarks>
///     The mandatory members are given to the constructor; metadata comes from <see cref="CustomPropertyOptions"/>.
///     The item is read-only when no setter is supplied (or <see cref="CustomPropertyOptions.ForceReadOnly"/> is set).
///     Text conversion, standard values, expandable objects and collections come from the converter and the property
///     type exactly as for reflection items. The class is sealed: it overrides the abstract and virtual members of <see cref="PropertyItem"/>
///     from the constructor arguments. Derive from <see cref="PropertyItem"/> directly only when delegates are not enough.
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     var gain = new Gain();
///     var item = new CustomPropertyItem(
///         "Gain",
///         typeof(double),
///         () => gain.Value,
///         v => gain.Value = (double)v!,
///         new CustomPropertyOptions { DisplayName = "Gain (dB)", Category = "Audio", HasDefault = true, DefaultValue = 0.0 });
///
///     public sealed class Gain
///     {
///         public double Value { get; set; }
///     }
///     ]]></code>
/// </example>
public sealed class CustomPropertyItem : PropertyItem
{
    /// <summary>
    ///     The getter.
    /// </summary>
    private readonly Func<object?> _getter;

    /// <summary>
    ///     The setter, or <see langword="null"/> for a read-only item.
    /// </summary>
    private readonly Action<object?>? _setter;

    /// <summary>
    ///     The property name.
    /// </summary>
    private readonly string _name;

    /// <summary>
    ///     The property type.
    /// </summary>
    private readonly Type _propertyType;

    /// <summary>
    ///     The optional metadata.
    /// </summary>
    private readonly CustomPropertyOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CustomPropertyItem"/> class.
    /// </summary>
    /// <param name="name">The property name (stable identifier; must be unique within its source).</param>
    /// <param name="propertyType">The declared property type.</param>
    /// <param name="getter">Reads the current value.</param>
    /// <param name="setter">Writes a value; <see langword="null"/> makes the item read-only.</param>
    /// <param name="options">The optional metadata; <see langword="null"/> means all defaults.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="propertyType"/> or <paramref name="getter"/> is <see langword="null"/>.</exception>
    public CustomPropertyItem(
        string name,
        Type propertyType,
        Func<object?> getter,
        Action<object?>? setter = null,
        CustomPropertyOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(propertyType);
        ArgumentNullException.ThrowIfNull(getter);
        _name = name;
        _propertyType = propertyType;
        _getter = getter;
        _setter = setter;
        _options = options ?? new CustomPropertyOptions();
    }

    /// <summary>
    ///     Gets the property name.
    /// </summary>
    public override string Name => _name;

    /// <summary>
    ///     Gets the display name; the property name when none was given.
    /// </summary>
    public override string DisplayName => _options.DisplayName ?? _name;

    /// <summary>
    ///     Gets the declared property type.
    /// </summary>
    public override Type PropertyType => _propertyType;

    /// <summary>
    ///     Gets the category; <see cref="PropertyItem.DefaultCategory"/> when none was given.
    /// </summary>
    public override string Category => _options.Category ?? DefaultCategory;

    /// <summary>
    ///     Gets the category rank (see <see cref="PropertyItem.CategoryOrder"/>); 0 when none was given.
    /// </summary>
    public override int CategoryOrder => _options.CategoryOrder;

    /// <summary>
    ///     Gets the rank within the category (see <see cref="PropertyItem.Order"/>); 0 when none was given.
    /// </summary>
    public override int Order => _options.Order;

    /// <summary>
    ///     Gets the description; empty when none was given.
    /// </summary>
    public override string Description => _options.Description ?? string.Empty;

    /// <summary>
    ///     Gets a value indicating whether the item is read-only: no setter was supplied or <see cref="CustomPropertyOptions.ForceReadOnly"/> is set.
    /// </summary>
    public override bool IsReadOnly => _setter is null || _options.ForceReadOnly;

    /// <summary>
    ///     Gets a value indicating whether the item is shown in a grid.
    /// </summary>
    public override bool IsBrowsable => _options.IsBrowsable;

    /// <summary>
    ///     Gets a value indicating whether the value should be masked.
    /// </summary>
    public override bool IsPasswordText => _options.IsPasswordText;

    /// <summary>
    ///     Gets a value indicating whether the name is shown in parentheses and sorted first.
    /// </summary>
    public override bool IsParenthesized => _options.IsParenthesized;

    /// <summary>
    ///     Gets what must be refreshed after the value changes.
    /// </summary>
    public override RefreshProperties RefreshMode => _options.RefreshMode;

    /// <summary>
    ///     Gets the custom editor key.
    /// </summary>
    public override string? EditorKey => _options.EditorKey;

    /// <summary>
    ///     Gets a value indicating whether the item has a default value, which enables <see cref="PropertyItem.IsChanged"/>.
    /// </summary>
    public override bool HasDefault => _options.HasDefault;

    /// <summary>
    ///     Gets the default value, used when <see cref="HasDefault"/> is <see langword="true"/>.
    /// </summary>
    public override object? DefaultValue => _options.DefaultValue;

    /// <summary>
    ///     Gets the type converter; the converter of the property type when none was given.
    /// </summary>
    public override TypeConverter Converter => _options.Converter ?? base.Converter;

    /// <inheritdoc />
    public override object? GetValue() => _getter();

    /// <inheritdoc />
    public override void SetValue(object? value)
    {
        if (_setter is null)
        {
            throw new NotSupportedException($"'{_name}' has no setter.");
        }

        _setter(value);
    }
}
