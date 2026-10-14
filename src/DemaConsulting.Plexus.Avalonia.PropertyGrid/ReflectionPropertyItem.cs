using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     A property item backed by a <see cref="PropertyDescriptor"/>, so that <see cref="ICustomTypeDescriptor"/>,
///     <see cref="TypeDescriptionProvider"/> and the <c>System.ComponentModel</c> annotations are honored.
/// </summary>
internal sealed class ReflectionPropertyItem : PropertyItem
{
    /// <summary>
    ///     The binding flags used to look for a <c>ShouldSerialize&lt;Name&gt;</c> method (WinForms allows non-public ones).
    /// </summary>
#pragma warning disable S3011 // Non-public ShouldSerialize<Name> methods are part of the WinForms convention this class follows
    private const BindingFlags ShouldSerializeFlags =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
#pragma warning restore S3011

    /// <summary>
    ///     The presentation layer name under which a <see cref="UIHintAttribute"/> is addressed to this grid.
    /// </summary>
    private const string UiHintPresentationLayer = "Avalonia";

    /// <summary>
    ///     The descriptor.
    /// </summary>
    private readonly PropertyDescriptor _descriptor;

    /// <summary>
    ///     Supplies the object whose property this is.
    /// </summary>
    private readonly Func<object?> _ownerProvider;

    /// <summary>
    ///     The cached default-value annotation.
    /// </summary>
    private readonly DefaultValueAttribute? _defaultValue;

    /// <summary>
    ///     The cached custom-editor annotation (property first, then property type); <see langword="null"/> when the
    ///     editor is named by a <see cref="UIHintAttribute"/> instead.
    /// </summary>
    private readonly PropertyEditorAttribute? _editor;

    /// <summary>
    ///     The cached editor key of a <see cref="UIHintAttribute"/> on the property, used when the property itself has
    ///     no <see cref="PropertyEditorAttribute"/>.
    /// </summary>
    private readonly string? _uiHintKey;

    /// <summary>
    ///     The cached display annotation (an alternative source of name, description and group).
    /// </summary>
    private readonly DisplayAttribute? _display;

    /// <summary>
    ///     Whether the owner type declares a <c>ShouldSerialize&lt;Name&gt;</c> method, once known.
    /// </summary>
    private bool? _hasShouldSerialize;

    /// <summary>
    ///     The text put before the name in the path of a top-level item.
    /// </summary>
    private readonly string _pathPrefix;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReflectionPropertyItem"/> class.
    /// </summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <param name="ownerProvider">Supplies the object whose property this is.</param>
    /// <param name="parent">The parent item, or <see langword="null"/> for a top-level item.</param>
    /// <param name="pathPrefix">The text put before the name in the path of a top-level item (for example <c>Data.</c>).</param>
    internal ReflectionPropertyItem(PropertyDescriptor descriptor, Func<object?> ownerProvider, PropertyItem? parent, string pathPrefix = "")
        : base(parent)
    {
        _descriptor = descriptor;
        _ownerProvider = ownerProvider;
        _pathPrefix = pathPrefix;
        _defaultValue = descriptor.Attributes.OfType<DefaultValueAttribute>().FirstOrDefault();
        _display = descriptor.Attributes.OfType<DisplayAttribute>().FirstOrDefault();
        var propertyEditor = descriptor.Attributes.OfType<PropertyEditorAttribute>().FirstOrDefault();
        var typeEditor = TypeDescriptor.GetAttributes(descriptor.PropertyType).OfType<PropertyEditorAttribute>().FirstOrDefault();

        // The descriptor's attributes include those of the property type, so an annotation equal to the type's own is
        // not one of the property (a [UIHint] on the property beats that one, but not a [PropertyEditor] on the property).
        var propertyLevelEditor = ReferenceEquals(propertyEditor, typeEditor) ? null : propertyEditor;
        _uiHintKey = propertyLevelEditor is null ? FindUiHintKey(descriptor) : null;
        _editor = _uiHintKey is null ? propertyEditor : null;
    }

    /// <inheritdoc />
    public override string Name => _descriptor.Name;

    /// <inheritdoc />
    internal override string RootPath => _pathPrefix + Name;

    /// <inheritdoc />
    public override string DisplayName
    {
        get
        {
            if (_descriptor.DisplayName != _descriptor.Name)
            {
                return _descriptor.DisplayName;
            }

            var name = SafeDisplay(() => _display?.GetName());
            return string.IsNullOrEmpty(name) ? _descriptor.Name : name;
        }
    }

    /// <inheritdoc />
    public override string Category
    {
        get
        {
            var category = _descriptor.Category;
            if (category == DefaultCategory && _descriptor.Attributes.OfType<CategoryAttribute>().All(c => c.IsDefaultAttribute()))
            {
                var group = SafeDisplay(() => _display?.GetGroupName());
                if (!string.IsNullOrEmpty(group))
                {
                    return group;
                }
            }

            return category;
        }
    }

    /// <inheritdoc />
    public override string Description
    {
        get
        {
            if (!string.IsNullOrEmpty(_descriptor.Description))
            {
                return _descriptor.Description;
            }

            return SafeDisplay(() => _display?.GetDescription()) ?? string.Empty;
        }
    }

    /// <inheritdoc />
    public override Type PropertyType => _descriptor.PropertyType;

    /// <inheritdoc />
    public override bool IsReadOnly =>
        _descriptor.IsReadOnly ||
        _descriptor.Attributes.OfType<EditableAttribute>().Any(e => !e.AllowEdit) ||
        (Parent is { IsReadOnly: true } && _descriptor.ComponentType.IsValueType);

    /// <inheritdoc />
    public override bool IsBrowsable => _descriptor.IsBrowsable;

    /// <inheritdoc />
    public override bool IsPasswordText =>
        _descriptor.Attributes.OfType<PasswordPropertyTextAttribute>().Any(p => p.Password);

    /// <inheritdoc />
    public override bool IsParenthesized =>
        _descriptor.Attributes.OfType<ParenthesizePropertyNameAttribute>().Any(p => p.NeedParenthesis);

    /// <inheritdoc />
    public override RefreshProperties RefreshMode =>
        _descriptor.Attributes.OfType<RefreshPropertiesAttribute>().FirstOrDefault()?.RefreshProperties ?? RefreshProperties.None;

    /// <inheritdoc />
    public override string? EditorKey => _editor?.EditorKey ?? _uiHintKey;

    /// <inheritdoc />
    public override Type? EditorType => _editor?.EditorType;

    /// <inheritdoc />
    public override TypeConverter Converter => _descriptor.Converter;

    /// <inheritdoc />
    public override bool HasDefault => _defaultValue is not null || DeclaresShouldSerialize();

    /// <inheritdoc />
    public override bool IsDefaultValueKnown => _defaultValue is not null;

    /// <inheritdoc />
    public override object? DefaultValue => _defaultValue?.Value;

    /// <inheritdoc />
    public override bool IsChanged
    {
        get
        {
            if (!HasDefault)
            {
                return false;
            }

            try
            {
                var owner = ResolveOwner();
                return owner is not null && _descriptor.ShouldSerializeValue(owner);
            }
            catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public override bool CanReset
    {
        get
        {
            if (!HasDefault || IsReadOnly)
            {
                return false;
            }

            try
            {
                var owner = ResolveOwner();
                return owner is not null && _descriptor.CanResetValue(owner);
            }
            catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    internal override object? ContextInstance => _ownerProvider();

    /// <inheritdoc />
    internal override PropertyDescriptor Descriptor => _descriptor;

    /// <inheritdoc />
    protected override IEnumerable<ValidationAttribute> GetValidationAttributes() =>
        _descriptor.Attributes.OfType<ValidationAttribute>();

    /// <inheritdoc />
    public override object? GetValue() =>
        _descriptor.GetValue(ResolveOwner() ?? throw new InvalidOperationException($"The object that owns '{Name}' is not available."));

    /// <inheritdoc />
    public override void SetValue(object? value)
    {
        var instance = _ownerProvider() ?? throw new InvalidOperationException($"The object that owns '{Name}' is not available.");
        _descriptor.SetValue(OwnerOf(instance), value);
        WriteBack(instance);
    }

    /// <inheritdoc />
    public override void Reset()
    {
        var instance = _ownerProvider() ?? throw new InvalidOperationException($"The object that owns '{Name}' is not available.");
        _descriptor.ResetValue(OwnerOf(instance));
        WriteBack(instance);
    }

    /// <summary>
    ///     Stores a modified value-type owner back into the property that holds it (the owner is a boxed copy).
    /// </summary>
    /// <param name="instance">The owner that was modified.</param>
    private void WriteBack(object instance)
    {
        if (Parent is not null && instance.GetType().IsValueType)
        {
            Parent.SetValue(instance);
        }
    }

    /// <summary>
    ///     Gets the object the descriptor must be applied to (custom type descriptors may name a different property owner).
    /// </summary>
    /// <returns>The owner, or <see langword="null"/> when there is none.</returns>
    private object? ResolveOwner()
    {
        var instance = _ownerProvider();
        return instance is null ? null : OwnerOf(instance);
    }

    /// <summary>
    ///     Gets the object the descriptor must be applied to for a given owner instance.
    /// </summary>
    /// <param name="instance">The owner instance.</param>
    /// <returns>The property owner.</returns>
    private object OwnerOf(object instance) =>
        TypeDescriptor.GetProvider(instance).GetTypeDescriptor(instance)?.GetPropertyOwner(_descriptor) ?? instance;

    /// <summary>
    ///     Reports whether the owner type declares a <c>ShouldSerialize&lt;Name&gt;</c> method returning
    ///     <see cref="bool"/> (the WinForms "has a default rule" convention).
    /// </summary>
    /// <returns><see langword="true"/> when the method exists.</returns>
    private bool DeclaresShouldSerialize()
    {
        if (_hasShouldSerialize is { } known)
        {
            return known;
        }

        var instance = _ownerProvider();
        if (instance is null)
        {
            return false;
        }

        var method = instance.GetType().GetMethod("ShouldSerialize" + _descriptor.Name, ShouldSerializeFlags, null, Type.EmptyTypes, null);
        _hasShouldSerialize = method is not null && method.ReturnType == typeof(bool);
        return _hasShouldSerialize.Value;
    }

    /// <summary>
    ///     Finds the editor key named by a <see cref="UIHintAttribute"/> on the property. A hint for the
    ///     <c>Avalonia</c> presentation layer is preferred over one without a presentation layer; hints for other
    ///     presentation layers (for example <c>MVC</c>) are ignored.
    /// </summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns>The key, or <see langword="null"/> when there is no applicable hint.</returns>
    private static string? FindUiHintKey(PropertyDescriptor descriptor)
    {
        var hints = descriptor.Attributes.OfType<UIHintAttribute>().Where(h => !string.IsNullOrWhiteSpace(h.UIHint)).ToList();
        var hint = hints.FirstOrDefault(h => string.Equals(h.PresentationLayer, UiHintPresentationLayer, StringComparison.OrdinalIgnoreCase)) ??
                   hints.FirstOrDefault(h => string.IsNullOrEmpty(h.PresentationLayer));
        return hint?.UIHint;
    }

    /// <summary>
    ///     Reads a display annotation value, tolerating resource lookup failures.
    /// </summary>
    /// <param name="read">The read.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    private static string? SafeDisplay(Func<string?> read)
    {
        try
        {
            return read();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
