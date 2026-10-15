using System.Globalization;

namespace DemaConsulting.Plexus;

/// <summary>
///     Normalizes, validates and formats property values for <see cref="PropertyDefinition"/> and <see cref="PropertyBag"/>.
/// </summary>
/// <remarks>
///     Centralizing the rules guarantees that the bag, the definitions and later serialization all
///     agree on which values are legal and how compatible numeric types convert. Canonical stored
///     values are exactly <see cref="bool"/>, <see cref="int"/>, <see cref="long"/>, <see cref="double"/>,
///     <see cref="string"/> and <see cref="Color"/>.
/// </remarks>
internal static class PropertyValues
{
    /// <summary>
    ///     Reduces any supported value to its canonical type and infers the matching kind.
    /// </summary>
    /// <remarks>
    ///     Small integral types widen to <see cref="int"/>, larger ones to <see cref="long"/>, and
    ///     floating types to <see cref="double"/>, so callers may pass whatever numeric type they hold.
    /// </remarks>
    /// <param name="value">The value to normalize.</param>
    /// <param name="kind">Receives the inferred kind (never <see cref="PropertyValueKind.Choice"/>).</param>
    /// <returns>The canonical value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the type is unsupported or a number is not finite or out of range.</exception>
    public static object Normalize(object value, out PropertyValueKind kind)
    {
        ArgumentNullException.ThrowIfNull(value);
        switch (value)
        {
            case bool:
                kind = PropertyValueKind.Boolean;
                return value;
            case int:
                kind = PropertyValueKind.Int32;
                return value;
            case sbyte or byte or short or ushort:
                kind = PropertyValueKind.Int32;
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            case long:
                kind = PropertyValueKind.Int64;
                return value;
            case uint u:
                kind = PropertyValueKind.Int64;
                return (long)u;
            case ulong ul when ul <= long.MaxValue:
                kind = PropertyValueKind.Int64;
                return (long)ul;
            case double d when double.IsFinite(d):
                kind = PropertyValueKind.Double;
                return value;
            case float f when float.IsFinite(f):
                kind = PropertyValueKind.Double;
                return (double)f;
            case decimal m:
                kind = PropertyValueKind.Double;
                return (double)m;
            case string:
                kind = PropertyValueKind.String;
                return value;
            case Color:
                kind = PropertyValueKind.Color;
                return value;
            default:
                throw new ArgumentException(
                    $"Value of type '{value.GetType().Name}' is not an allowed property value " +
                    "(bool, int, long, double, string or Color; numbers must be finite).",
                    nameof(value));
        }
    }

    /// <summary>
    ///     Coerces a value to the canonical representation required by a declared kind.
    /// </summary>
    /// <param name="value">The value to coerce.</param>
    /// <param name="kind">The declared kind.</param>
    /// <param name="options">The allowed options when <paramref name="kind"/> is <see cref="PropertyValueKind.Choice"/>.</param>
    /// <returns>The canonical value for the kind.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the value is incompatible with the kind (including a number the kind cannot hold exactly) or not
    ///     among the options.
    /// </exception>
    public static object Coerce(object value, PropertyValueKind kind, IReadOnlyList<string>? options)
    {
        var canonical = Normalize(value, out var actual);

        // Numbers convert only when the value is preserved exactly; everything else must match exactly
        object? result = kind switch
        {
            PropertyValueKind.Int32 => CoerceInt32(canonical, actual),
            PropertyValueKind.Int64 => actual is PropertyValueKind.Int32 or PropertyValueKind.Int64 ? Convert.ToInt64(canonical, CultureInfo.InvariantCulture) : null,
            PropertyValueKind.Double => CoerceDouble(canonical, actual),
            PropertyValueKind.Choice => actual == PropertyValueKind.String && options is not null && options.Contains((string)canonical, StringComparer.Ordinal) ? canonical : null,
            _ => actual == kind ? canonical : null,
        };

        return result ?? throw new ArgumentException(DescribeMismatch(value, kind, options), nameof(value));
    }

    /// <summary>
    ///     Formats a value for display using invariant culture.
    /// </summary>
    /// <param name="value">The value to format; null formats as an empty string.</param>
    /// <returns>The display text.</returns>
    public static string Format(object? value) => value switch
    {
        null => string.Empty,
        Color c => c.ToString(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>
    ///     Formats a value as editable text that reads back to the same value, using invariant culture.
    /// </summary>
    /// <param name="value">The value to format; null formats as an empty string.</param>
    /// <returns>The text.</returns>
    public static string FormatForEdit(object? value) => value switch
    {
        null => string.Empty,
        bool b => b ? "true" : "false",
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        _ => Format(value),
    };

    /// <summary>
    ///     Coerces a canonical value to <see cref="int"/> when it is an in-range integer.
    /// </summary>
    /// <param name="canonical">The canonical value.</param>
    /// <param name="actual">The kind of <paramref name="canonical"/>.</param>
    /// <returns>The boxed int, or null when the value cannot be represented exactly.</returns>
    private static object? CoerceInt32(object canonical, PropertyValueKind actual)
    {
        if (actual == PropertyValueKind.Int32)
        {
            return canonical;
        }

        if (actual == PropertyValueKind.Int64 && (long)canonical is >= int.MinValue and <= int.MaxValue)
        {
            return (int)(long)canonical;
        }

        return null;
    }

    /// <summary>
    ///     Coerces a canonical value to <see cref="double"/> when the conversion preserves it exactly.
    /// </summary>
    /// <remarks>
    ///     Every <see cref="int"/> is exactly representable as a double. A <see cref="long"/> converts only when the
    ///     double converts back to the very same <see cref="long"/> (for example 2^53 + 1 and
    ///     <see cref="long.MaxValue"/> are rejected because the double would hold a different number).
    /// </remarks>
    /// <param name="canonical">The canonical value.</param>
    /// <param name="actual">The kind of <paramref name="canonical"/>.</param>
    /// <returns>The boxed double, or null when the value cannot be represented exactly.</returns>
    private static object? CoerceDouble(object canonical, PropertyValueKind actual)
    {
        switch (actual)
        {
            case PropertyValueKind.Double:
                return canonical;

            case PropertyValueKind.Int32:
                return (double)(int)canonical;

            case PropertyValueKind.Int64:
                // 2^63 is the first double above long.MaxValue; below it the cast back is defined and exact iff lossless
                var l = (long)canonical;
                var d = (double)l;
                return d < 9223372036854775808.0 && (long)d == l ? d : null;

            default:
                return null;
        }
    }

    /// <summary>
    ///     Builds the error message for a value that cannot be stored in a declared kind.
    /// </summary>
    /// <param name="value">The rejected value.</param>
    /// <param name="kind">The declared kind.</param>
    /// <param name="options">The allowed options for choice kinds.</param>
    /// <returns>The message text.</returns>
    private static string DescribeMismatch(object value, PropertyValueKind kind, IReadOnlyList<string>? options) =>
        kind == PropertyValueKind.Choice
            ? $"'{Format(value)}' is not one of the allowed choices ({string.Join(", ", options ?? [])})."
            : $"Value '{Format(value)}' of type '{value.GetType().Name}' is not compatible with property kind {kind}.";
}
