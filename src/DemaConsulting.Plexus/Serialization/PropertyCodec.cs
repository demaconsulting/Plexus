using System.Globalization;
using System.Text.Json;

namespace DemaConsulting.Plexus;

/// <summary>
///     Converts property values to and from their snapshot representation.
/// </summary>
/// <remarks>
///     Only the portable kinds exist (bool, int, long, double, string, choice, color), so every value
///     has an exact JSON form: numbers are written with round-trip precision and colors as <c>"#AARRGGBB"</c>.
///     Reading is strict about the token type so a malformed document is reported instead of coerced.
/// </remarks>
internal static class PropertyCodec
{
    /// <summary>
    ///     Captures the properties of a node, declared ones first.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The snapshots by property name, in bag enumeration order.</returns>
    public static Dictionary<string, PropertySnapshot> Write(Node node)
    {
        var result = new Dictionary<string, PropertySnapshot>(StringComparer.Ordinal);
        foreach (var (name, value) in node.Properties)
        {
            // A choice is a string in the bag; only the definition knows it is constrained
            var kind = node.Properties.GetDefinition(name)?.ValueKind == PropertyValueKind.Choice
                ? PropertyValueKind.Choice
                : KindOf(value);
            result[name] = new PropertySnapshot { Kind = kind, Value = ToElement(value) };
        }

        return result;
    }

    /// <summary>
    ///     Reads a snapshot value into its canonical CLR representation.
    /// </summary>
    /// <param name="snapshot">The snapshot to read.</param>
    /// <param name="value">Receives the canonical value (bool, int, long, double, string or Color).</param>
    /// <param name="problem">Receives what is wrong when reading fails.</param>
    /// <returns><see langword="true"/> when the JSON value is valid for the stated kind.</returns>
    public static bool TryRead(PropertySnapshot snapshot, out object value, out string? problem)
    {
        value = string.Empty;
        problem = null;
        var json = snapshot.Value;
        switch (snapshot.Kind)
        {
            case PropertyValueKind.Boolean when json.ValueKind is JsonValueKind.True or JsonValueKind.False:
                value = json.GetBoolean();
                return true;
            case PropertyValueKind.Int32 when json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var i):
                value = i;
                return true;
            case PropertyValueKind.Int64 when json.ValueKind == JsonValueKind.Number && json.TryGetInt64(out var l):
                value = l;
                return true;
            case PropertyValueKind.Double when json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out var d) && double.IsFinite(d):
                value = d;
                return true;
            case PropertyValueKind.String or PropertyValueKind.Choice when json.ValueKind == JsonValueKind.String:
                value = json.GetString() ?? string.Empty;
                return true;
            case PropertyValueKind.Color when json.ValueKind == JsonValueKind.String && Color.TryParse(json.GetString(), out var c):
                value = c;
                return true;
            default:
                problem = $"the value is not a valid {snapshot.Kind} (JSON {json.ValueKind}).";
                return false;
        }
    }

    /// <summary>
    ///     Formats a color as <c>#AARRGGBB</c>; always eight digits so the form never depends on opacity.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The text.</returns>
    public static string FormatColor(Color color) =>
        string.Create(CultureInfo.InvariantCulture, $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");

    /// <summary>
    ///     Determines the snapshot kind of a canonical value.
    /// </summary>
    /// <param name="value">The canonical value.</param>
    /// <returns>The kind.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value is not one of the portable kinds.</exception>
    private static PropertyValueKind KindOf(object value) => value switch
    {
        bool => PropertyValueKind.Boolean,
        int => PropertyValueKind.Int32,
        long => PropertyValueKind.Int64,
        double => PropertyValueKind.Double,
        string => PropertyValueKind.String,
        Color => PropertyValueKind.Color,
        _ => throw new InvalidOperationException($"Property value of type '{value.GetType().Name}' is not portable."),
    };

    /// <summary>
    ///     Converts a canonical value to its JSON element.
    /// </summary>
    /// <param name="value">The canonical value.</param>
    /// <returns>The element.</returns>
    private static JsonElement ToElement(object value)
    {
        var context = PlexusJson.Compact;
        return value switch
        {
            bool b => JsonSerializer.SerializeToElement(b, context.Boolean),
            int i => JsonSerializer.SerializeToElement(i, context.Int32),
            long l => JsonSerializer.SerializeToElement(l, context.Int64),
            double d => JsonSerializer.SerializeToElement(d, context.Double),
            Color c => JsonSerializer.SerializeToElement(FormatColor(c), context.String),
            _ => JsonSerializer.SerializeToElement((string)value, context.String),
        };
    }
}
