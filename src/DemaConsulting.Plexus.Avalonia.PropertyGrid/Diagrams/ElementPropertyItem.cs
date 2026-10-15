using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

/// <summary>
///     A property item of a Plexus diagram element (the node name, a property of the bag, an annotation text, ...): reads the
///     live value from the element until the grid writes a new value, which is kept in <see cref="WorkingValues"/> until the
///     source publishes it.
/// </summary>
internal sealed class ElementPropertyItem : PropertyItem
{
    /// <summary>
    ///     The item name.
    /// </summary>
    private readonly string _name;

    /// <summary>
    ///     The value type.
    /// </summary>
    private readonly Type _type;

    /// <summary>
    ///     Where written values are kept.
    /// </summary>
    private readonly WorkingValues _work;

    /// <summary>
    ///     Reads the element's current value.
    /// </summary>
    private readonly Func<object?> _current;

    /// <summary>
    ///     The presentation options.
    /// </summary>
    private readonly CustomPropertyOptions _options;

    /// <summary>
    ///     The extra rule a value must satisfy, returning the reason it does not.
    /// </summary>
    private readonly Func<object?, string?>? _check;

    /// <summary>
    ///     The text put before the name in the item's path (for example <c>Property.</c>), keeping groups of rows apart.
    /// </summary>
    private readonly string _pathPrefix;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ElementPropertyItem"/> class.
    /// </summary>
    /// <param name="name">The item name.</param>
    /// <param name="type">The value type.</param>
    /// <param name="work">Where written values are kept.</param>
    /// <param name="current">Reads the element's current value.</param>
    /// <param name="options">The presentation options.</param>
    /// <param name="check">The extra validation rule, or <see langword="null"/>.</param>
    /// <param name="pathPrefix">The text put before the name in the item's path; empty for none.</param>
    internal ElementPropertyItem(
        string name,
        Type type,
        WorkingValues work,
        Func<object?> current,
        CustomPropertyOptions options,
        Func<object?, string?>? check = null,
        string pathPrefix = "")
    {
        _pathPrefix = pathPrefix;
        _name = name;
        _type = type;
        _work = work;
        _current = current;
        _options = options;
        _check = check;
    }

    /// <inheritdoc />
    public override string Name => _name;

    /// <inheritdoc />
    internal override string RootPath => _pathPrefix + _name;

    /// <inheritdoc />
    public override string DisplayName => _options.DisplayName ?? _name;

    /// <inheritdoc />
    public override string Category => _options.Category ?? DefaultCategory;

    /// <inheritdoc />
    public override int CategoryOrder => _options.CategoryOrder;

    /// <inheritdoc />
    public override int Order => _options.Order;

    /// <inheritdoc />
    public override string Description => _options.Description ?? string.Empty;

    /// <inheritdoc />
    public override Type PropertyType => _type;

    /// <inheritdoc />
    public override bool IsReadOnly => _options.ForceReadOnly;

    /// <inheritdoc />
    public override bool IsParenthesized => _options.IsParenthesized;

    /// <inheritdoc />
    public override string? EditorKey => _options.EditorKey;

    /// <inheritdoc />
    public override bool HasDefault => _options.HasDefault;

    /// <inheritdoc />
    public override object? DefaultValue => _options.DefaultValue;

    /// <inheritdoc />
    public override TypeConverter Converter => _options.Converter ?? base.Converter;

    /// <inheritdoc />
    public override object? GetValue() => _work.TryGet(Path, out var written) ? written : _current();

    /// <inheritdoc />
    public override void SetValue(object? value) => _work.Set(Path, value);

    /// <inheritdoc />
    protected override IEnumerable<ValidationAttribute> GetValidationAttributes() =>
        _check is null ? [] : [new RuleAttribute(_check)];

    /// <summary>
    ///     Adapts a validation delegate to a validation annotation, so the rule runs wherever the item is validated.
    /// </summary>
    /// <param name="rule">Returns the reason a value is invalid, or <see langword="null"/> when it is valid.</param>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    private sealed class RuleAttribute(Func<object?, string?> rule) : ValidationAttribute
    {
        /// <inheritdoc />
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
            rule(value) is { } reason ? new ValidationResult(reason) : ValidationResult.Success;
    }
}
