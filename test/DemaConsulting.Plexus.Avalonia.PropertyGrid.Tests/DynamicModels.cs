using System.ComponentModel;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     An object whose properties are defined at run time through <see cref="ICustomTypeDescriptor"/>.
/// </summary>
public sealed class DynamicBag : ICustomTypeDescriptor
{
    /// <summary>
    ///     The values by name.
    /// </summary>
    private readonly Dictionary<string, object?> _values = [];

    /// <summary>
    ///     Adds or replaces a dynamic property.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The initial value; its type is the property type.</param>
    public void Define(string name, object value) => _values[name] = value;

    /// <summary>
    ///     Reads a value.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>The value.</returns>
    public object? Read(string name) => _values[name];

    /// <inheritdoc />
    public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(this, true);

    /// <inheritdoc />
    public string? GetClassName() => TypeDescriptor.GetClassName(this, true);

    /// <inheritdoc />
    public string? GetComponentName() => TypeDescriptor.GetComponentName(this, true);

    /// <inheritdoc />
    public TypeConverter GetConverter() => TypeDescriptor.GetConverter(this, true);

    /// <inheritdoc />
    public EventDescriptor? GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);

    /// <inheritdoc />
    public PropertyDescriptor? GetDefaultProperty() => null;

    /// <inheritdoc />
    public object? GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType, true);

    /// <inheritdoc />
    public EventDescriptorCollection GetEvents() => TypeDescriptor.GetEvents(this, true);

    /// <inheritdoc />
    public EventDescriptorCollection GetEvents(Attribute[]? attributes) => TypeDescriptor.GetEvents(this, attributes, true);

    /// <inheritdoc />
    public PropertyDescriptorCollection GetProperties() => GetProperties(null);

    /// <inheritdoc />
    public PropertyDescriptorCollection GetProperties(Attribute[]? attributes) =>
        new(_values.Select(pair => (PropertyDescriptor)new BagPropertyDescriptor(pair.Key, pair.Value!.GetType())).ToArray());

    /// <inheritdoc />
    public object GetPropertyOwner(PropertyDescriptor? pd) => this;

    /// <summary>
    ///     Reads a value (used by the descriptor).
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>The value.</returns>
    internal object? Get(string name) => _values[name];

    /// <summary>
    ///     Writes a value (used by the descriptor).
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The value.</param>
    internal void Set(string name, object? value) => _values[name] = value;

    /// <summary>
    ///     A property descriptor that reads and writes the bag's dictionary.
    /// </summary>
    private sealed class BagPropertyDescriptor : PropertyDescriptor
    {
        /// <summary>
        ///     The property type.
        /// </summary>
        private readonly Type _type;

        /// <summary>
        ///     Initializes a new instance of the <see cref="BagPropertyDescriptor"/> class.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="type">The property type.</param>
        public BagPropertyDescriptor(string name, Type type)
            : base(name, [new CategoryAttribute("Dynamic"), new DescriptionAttribute("Defined at run time.")])
        {
            _type = type;
        }

        /// <inheritdoc />
        public override Type ComponentType => typeof(DynamicBag);

        /// <inheritdoc />
        public override bool IsReadOnly => false;

        /// <inheritdoc />
        public override Type PropertyType => _type;

        /// <inheritdoc />
        public override bool CanResetValue(object component) => false;

        /// <inheritdoc />
        public override object? GetValue(object? component) => ((DynamicBag)component!).Get(Name);

        /// <inheritdoc />
        public override void ResetValue(object component)
        {
            // Dynamic properties have no default
        }

        /// <inheritdoc />
        public override void SetValue(object? component, object? value) => ((DynamicBag)component!).Set(Name, value);

        /// <inheritdoc />
        public override bool ShouldSerializeValue(object component) => true;
    }
}

/// <summary>
///     A type whose properties are extended by a <see cref="TypeDescriptionProvider"/>.
/// </summary>
public sealed class Provided
{
    /// <summary>Gets or sets a normal property.</summary>
    public string Real { get; set; } = "real";
}

/// <summary>
///     A provider that adds a read-only <c>Extra</c> property to <see cref="Provided"/>.
/// </summary>
public sealed class ExtraPropertyProvider : TypeDescriptionProvider
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExtraPropertyProvider"/> class.
    /// </summary>
    public ExtraPropertyProvider()
        : base(TypeDescriptor.GetProvider(typeof(Provided)))
    {
    }

    /// <inheritdoc />
    public override ICustomTypeDescriptor? GetTypeDescriptor(Type objectType, object? instance) =>
        new ExtraDescriptor(base.GetTypeDescriptor(objectType, instance));

    /// <summary>
    ///     Adds the extra property to the base descriptor's properties.
    /// </summary>
    /// <param name="parent">The base descriptor.</param>
    private sealed class ExtraDescriptor(ICustomTypeDescriptor? parent) : CustomTypeDescriptor(parent)
    {
        /// <inheritdoc />
        public override PropertyDescriptorCollection GetProperties() => GetProperties(null);

        /// <inheritdoc />
        public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
        {
            var all = base.GetProperties(attributes).Cast<PropertyDescriptor>().ToList();
            all.Add(new ExtraPropertyDescriptor());
            return new PropertyDescriptorCollection(all.ToArray());
        }
    }

    /// <summary>
    ///     The read-only extra property.
    /// </summary>
    private sealed class ExtraPropertyDescriptor() : PropertyDescriptor("Extra", null)
    {
        /// <inheritdoc />
        public override Type ComponentType => typeof(Provided);

        /// <inheritdoc />
        public override bool IsReadOnly => true;

        /// <inheritdoc />
        public override Type PropertyType => typeof(string);

        /// <inheritdoc />
        public override bool CanResetValue(object component) => false;

        /// <inheritdoc />
        public override object? GetValue(object? component) => "extra";

        /// <inheritdoc />
        public override void ResetValue(object component)
        {
            // Read-only
        }

        /// <inheritdoc />
        public override void SetValue(object? component, object? value)
        {
            // Read-only
        }

        /// <inheritdoc />
        public override bool ShouldSerializeValue(object component) => false;
    }
}
