using System.ComponentModel;
using System.Globalization;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <content>
///     Text conversion and standard values, both derived from the property's <see cref="TypeConverter"/>.
/// </content>
public abstract partial class PropertyItem
{
    /// <summary>
    ///     The text shown for a collection whose type converter has no text form of its own.
    /// </summary>
    internal const string CollectionText = "(Collection)";

    /// <summary>
    ///     Gets the type converter for the property (the <c>[TypeConverter]</c> annotation on the property, else on its
    ///     type, else the framework default for the type).
    /// </summary>
    public virtual TypeConverter Converter => TypeDescriptor.GetConverter(PropertyType);

    /// <summary>
    ///     Gets the converter to use for a given value: for properties declared as <see cref="object"/> the converter
    ///     of the value's runtime type (so an <c>object</c> property holding an expandable value still expands),
    ///     otherwise <see cref="Converter"/>.
    /// </summary>
    /// <param name="value">The current value.</param>
    /// <returns>The converter.</returns>
    private TypeConverter GetEffectiveConverter(object? value) =>
        PropertyType == typeof(object) && value is not null ? TypeDescriptor.GetConverter(value) : Converter;

    /// <summary>
    ///     Gets a value indicating whether the value can be typed as text: the converter converts from
    ///     <see cref="string"/> (or the property is an <see cref="object"/>), and the property is not a collection.
    /// </summary>
    public bool SupportsTextEditing =>
        !IsCollection && (PropertyType == typeof(object) || Converter.CanConvertFrom(new PropertyContext(this), typeof(string)));

    /// <summary>
    ///     Converts a value to the text a user sees and edits.
    /// </summary>
    /// <param name="value">The value to convert (may be <see langword="null"/>).</param>
    /// <param name="culture">
    ///     The culture to format with; <see langword="null"/> means <see cref="CultureInfo.CurrentCulture"/>.
    /// </param>
    /// <returns>
    ///     The text; empty for <see langword="null"/>; <c>(Collection)</c> for a collection whose converter has no
    ///     text form.
    /// </returns>
    /// <exception cref="NotSupportedException">The converter cannot convert the value (rare; see <see cref="TryConvertToString"/>).</exception>
    public string ConvertToString(object? value, CultureInfo? culture = null)
    {
        if (value is null)
        {
            return string.Empty;
        }

        culture ??= CultureInfo.CurrentCulture;
        var converter = GetEffectiveConverter(value);
        if (IsCollection && converter.GetType() == typeof(TypeConverter))
        {
            return CollectionText;
        }

        var context = new PropertyContext(this);
        return converter.CanConvertTo(context, typeof(string))
            ? converter.ConvertToString(context, culture, value) ?? string.Empty
            : Convert.ToString(value, culture) ?? string.Empty;
    }

    /// <summary>
    ///     Converts a value to text, capturing converter exceptions as an error and showing the error text instead.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="text">The text; empty when conversion failed.</param>
    /// <param name="culture">The culture to format with; <see langword="null"/> means the current culture.</param>
    /// <returns>The outcome of the conversion.</returns>
    public PropertyResult TryConvertToString(object? value, out string text, CultureInfo? culture = null)
    {
        try
        {
            text = ConvertToString(value, culture);
            return PropertyResult.Success;
        }
        catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
        {
            text = string.Empty;
            return PropertyResult.Failure(PropertyErrors.Message(ex));
        }
    }

    /// <summary>
    ///     Gets the current value as text, never throwing: a getter or converter failure yields an empty string.
    /// </summary>
    /// <param name="culture">The culture to format with; <see langword="null"/> means the current culture.</param>
    /// <returns>The text of the current value.</returns>
    public string GetText(CultureInfo? culture = null) =>
        TryGetValue(out var value).Succeeded && TryConvertToString(value, out var text, culture).Succeeded ? text : string.Empty;

    /// <summary>
    ///     Converts text typed by a user to a value of the property type.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="culture">The culture to parse with; <see langword="null"/> means <see cref="CultureInfo.CurrentCulture"/>.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="NotSupportedException">The property type cannot be converted from text.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="Exception">
    ///     The text is not valid for the type; the exception type is the converter's (the framework number converters
    ///     throw <see cref="ArgumentException"/> wrapping a <see cref="FormatException"/>). Use <see cref="TryParse"/>
    ///     to get a message instead.
    /// </exception>
    public object? ConvertFromString(string text, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        culture ??= CultureInfo.CurrentCulture;
        if (PropertyType == typeof(object))
        {
            return text;
        }

        var context = new PropertyContext(this);
        var converter = Converter;
        if (IsCollection || !converter.CanConvertFrom(context, typeof(string)))
        {
            throw new NotSupportedException($"'{DisplayName}' ({PropertyType.Name}) cannot be edited as text.");
        }

        return converter.ConvertFromString(context, culture, text);
    }

    /// <summary>
    ///     Converts text typed by a user to a value, capturing converter exceptions as an error, and validates it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="culture">The culture to parse with; <see langword="null"/> means the current culture.</param>
    /// <param name="value">The converted value; <see langword="null"/> when conversion failed.</param>
    /// <returns>The outcome of the conversion and validation.</returns>
    public PropertyResult TryParse(string text, CultureInfo? culture, out object? value)
    {
        try
        {
            value = ConvertFromString(text, culture);
        }
        catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
        {
            value = null;
            return PropertyResult.Failure(PropertyErrors.Message(ex));
        }

        return Validate(value);
    }

    /// <summary>
    ///     Converts text typed by a user and writes the result.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="culture">The culture to parse with; <see langword="null"/> means the current culture.</param>
    /// <returns>The outcome of the conversion, validation and write.</returns>
    public PropertyResult TrySetText(string text, CultureInfo? culture = null)
    {
        var parsed = TryParse(text, culture, out var value);
        return parsed.Succeeded ? TrySetValue(value) : parsed;
    }

    /// <summary>
    ///     Gets a value indicating whether the converter offers a list of standard values (enumeration members,
    ///     <see cref="bool"/>, or the converter's own list).
    /// </summary>
    public bool HasStandardValues
    {
        get
        {
            try
            {
                return Converter.GetStandardValuesSupported(new PropertyContext(this));
            }
            catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
            {
                return false;
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether only the standard values are allowed (a drop-down list rather than an
    ///     editable combo box). <see langword="false"/> when there are no standard values.
    /// </summary>
    public bool StandardValuesExclusive
    {
        get
        {
            try
            {
                var context = new PropertyContext(this);
                return Converter.GetStandardValuesSupported(context) && Converter.GetStandardValuesExclusive(context);
            }
            catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
            {
                return false;
            }
        }
    }

    /// <summary>
    ///     Gets the standard values with their display text. The list is computed on each call because converters may
    ///     depend on the edited instance.
    /// </summary>
    /// <param name="culture">The culture used to produce the text; <see langword="null"/> means the current culture.</param>
    /// <returns>The values; empty when the converter offers none or fails.</returns>
    public IReadOnlyList<StandardValue> GetStandardValues(CultureInfo? culture = null)
    {
        try
        {
            var context = new PropertyContext(this);
            var converter = Converter;
            if (!converter.GetStandardValuesSupported(context))
            {
                return [];
            }

            var values = converter.GetStandardValues(context);
            if (values is null)
            {
                return [];
            }

            var result = new List<StandardValue>(values.Count);
            foreach (var value in values)
            {
                result.Add(new StandardValue(value, ConvertToString(value, culture)));
            }

            return result;
        }
        catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
        {
            return [];
        }
    }
}
