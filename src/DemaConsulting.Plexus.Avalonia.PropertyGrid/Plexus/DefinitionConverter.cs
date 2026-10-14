using System.ComponentModel;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus;

/// <summary>
///     The text conversion of a declared Plexus property: it parses what a user typed with
///     <see cref="PropertyDefinition.TryParse"/> (culture-invariant, a trailing unit is tolerated, the reason for a
///     failure is the message) and formats values for editing with <see cref="PropertyDefinition.FormatForEdit"/>. A
///     choice property offers its choices as an exclusive list of standard values.
/// </summary>
/// <param name="definition">The declaration of the property.</param>
internal sealed class DefinitionConverter(PropertyDefinition definition) : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
    {
        if (value is not string text)
        {
            return base.ConvertFrom(context, culture, value);
        }

        return definition.TryParse(text, out var parsed, out var error)
            ? parsed
            : throw new FormatException(error);
    }

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string)
            ? PropertyDefinition.FormatForEdit(value)
            : base.ConvertTo(context, culture, value, destinationType);

    /// <inheritdoc />
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => definition.Choices is not null;

    /// <inheritdoc />
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    /// <inheritdoc />
    public override StandardValuesCollection? GetStandardValues(ITypeDescriptorContext? context) =>
        definition.Choices is { } choices ? new StandardValuesCollection(choices.ToList()) : null;
}
