using System.Text.Json;
using System.Text.Json.Nodes;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Demo.Gui.Automation;

/// <summary>
///     Reads command arguments from, and builds results as, <see cref="JsonNode"/> trees.
/// </summary>
/// <remarks>
///     Arguments come from a remote script, so every reader validates and reports a precise
///     <see cref="ArgumentException"/>; the dispatcher turns those into <c>{"ok":false}</c> replies.
/// </remarks>
internal static class AutomationJson
{
    /// <summary>
    ///     Reads a required string argument.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The string.</returns>
    /// <exception cref="ArgumentException">Thrown when the argument is missing or not a string.</exception>
    public static string RequireString(JsonObject request, string name) =>
        OptionalString(request, name) ?? throw new ArgumentException($"Missing argument '{name}'.");

    /// <summary>
    ///     Reads an optional string argument; numbers are accepted and converted to text.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The text, or null when absent.</returns>
    public static string? OptionalString(JsonObject request, string name)
    {
        if (!request.TryGetPropertyValue(name, out var node) || node is null)
        {
            return null;
        }

        return node is JsonValue value
            ? value.GetValueKind() switch
            {
                JsonValueKind.String => value.GetValue<string>(),
                JsonValueKind.Number => value.ToJsonString(),
                _ => throw new ArgumentException($"Argument '{name}' must be a string."),
            }
            : throw new ArgumentException($"Argument '{name}' must be a string.");
    }

    /// <summary>
    ///     Reads a required finite number argument.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The number.</returns>
    /// <exception cref="ArgumentException">Thrown when the argument is missing or not a number.</exception>
    public static double RequireNumber(JsonObject request, string name) =>
        OptionalNumber(request, name) ?? throw new ArgumentException($"Missing argument '{name}'.");

    /// <summary>
    ///     Reads an optional finite number argument.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The number, or null when absent.</returns>
    /// <exception cref="ArgumentException">Thrown when the argument is present but not a finite number.</exception>
    public static double? OptionalNumber(JsonObject request, string name)
    {
        if (!request.TryGetPropertyValue(name, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue(out double number) && double.IsFinite(number))
        {
            return number;
        }

        throw new ArgumentException($"Argument '{name}' must be a number.");
    }

    /// <summary>
    ///     Reads an optional boolean argument.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The value, or null when absent.</returns>
    /// <exception cref="ArgumentException">Thrown when the argument is present but not a boolean.</exception>
    public static bool? OptionalBool(JsonObject request, string name)
    {
        if (!request.TryGetPropertyValue(name, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetValue<bool>();
        }

        throw new ArgumentException($"Argument '{name}' must be true or false.");
    }

    /// <summary>
    ///     Reads an optional list of strings, from either an array or a single string.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The strings (empty when absent).</returns>
    /// <exception cref="ArgumentException">Thrown when the argument has another shape.</exception>
    public static IReadOnlyList<string> StringList(JsonObject request, string name)
    {
        if (!request.TryGetPropertyValue(name, out var node) || node is null)
        {
            return [];
        }

        if (node is JsonArray array)
        {
            return [.. array.Select(item => item is JsonValue v && v.GetValueKind() == JsonValueKind.String
                ? v.GetValue<string>()
                : throw new ArgumentException($"Argument '{name}' must be a list of strings."))];
        }

        return [OptionalString(request, name)!];
    }

    /// <summary>
    ///     Builds the JSON form of a property value.
    /// </summary>
    /// <param name="value">A bool, int, long, double, string or color.</param>
    /// <returns>The JSON node.</returns>
    public static JsonNode? FromValue(object? value) => value switch
    {
        null => null,
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d => JsonValue.Create(d),
        string s => JsonValue.Create(s),
        _ => JsonValue.Create(value.ToString()),
    };

    /// <summary>
    ///     Builds the JSON form of a rectangle: <c>{x,y,width,height}</c>.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <returns>The JSON object.</returns>
    public static JsonObject FromRect(Rect rect) => new()
    {
        ["x"] = rect.X,
        ["y"] = rect.Y,
        ["width"] = rect.Width,
        ["height"] = rect.Height,
    };

    /// <summary>
    ///     Builds the JSON form of a point: <c>{x,y}</c>.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The JSON object.</returns>
    public static JsonObject FromPoint(Point point) => new() { ["x"] = point.X, ["y"] = point.Y };

    /// <summary>
    ///     Builds a JSON array from a sequence.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="convert">Converts one item.</param>
    /// <returns>The JSON array.</returns>
    public static JsonArray ToArray<T>(IEnumerable<T> items, Func<T, JsonNode?> convert)
    {
        var array = new JsonArray();
        foreach (var item in items)
        {
            array.Add(convert(item));
        }

        return array;
    }
}
