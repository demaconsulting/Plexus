using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace DemaConsulting.Plexus;

/// <summary>
///     Converts between the JSON snapshot format and the format-neutral tree of plain values.
/// </summary>
/// <remarks>
///     The tree has exactly the shape of the JSON document, so there is only one schema. Producing it parses the JSON text
///     and keeps only strings, booleans, 64-bit integers, doubles, null, lists and string-keyed dictionaries; consuming it
///     writes JSON again and hands it to the normal reader, so every validation of the JSON path applies unchanged. Reading
///     is tolerant about the source: any integer or floating CLR number type, <see cref="JsonElement"/> values (what
///     System.Text.Json produces for untyped data), any <see cref="IDictionary{TKey, TValue}"/>, <see cref="IReadOnlyDictionary{TKey, TValue}"/>
///     or non-generic <see cref="IDictionary"/> map, and any list.
/// </remarks>
internal static class DiagramTree
{
    /// <summary>
    ///     Builds the tree for a snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The tree root.</returns>
    public static IDictionary<string, object?> ToTree(DiagramSnapshot snapshot)
    {
        var json = PlexusJson.Serialize(snapshot, indented: false);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = PlexusJson.MaxDepth });
        var root = (Dictionary<string, object?>)Convert(document.RootElement)!;
        NormalizeDoubleProperties(root);
        return root;
    }

    /// <summary>
    ///     Writes a tree as JSON text in the snapshot format.
    /// </summary>
    /// <param name="tree">The tree root.</param>
    /// <returns>The UTF-8 JSON text.</returns>
    /// <exception cref="SnapshotException">Thrown when the tree holds a value that has no JSON form, a non-string key, or nests too deeply.</exception>
    public static string ToJson(IEnumerable<KeyValuePair<string, object?>> tree)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteObject(writer, tree.Select(p => (p.Key, p.Value)), "$", 0);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    ///     Converts a JSON element to the tree vocabulary.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>A dictionary, list, string, bool, long, double or null.</returns>
    private static object? Convert(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var map = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var member in element.EnumerateObject())
                {
                    map[member.Name] = Convert(member.Value);
                }

                return map;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(Convert(item));
                }

                return list;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                // Both branches are boxed on purpose: a plain conditional would have type double and turn every integer into one
                return element.TryGetInt64(out var l) ? (object)l : element.GetDouble();
            default:
                return null;
        }
    }

    /// <summary>
    ///     Makes the value of every property of kind <c>double</c> a double, even when it is a whole number, so a writer such as TOML shows <c>2.0</c> rather than <c>2</c>.
    /// </summary>
    /// <param name="root">The tree root.</param>
    private static void NormalizeDoubleProperties(Dictionary<string, object?> root)
    {
        if (root.GetValueOrDefault("pages") is not List<object?> pages)
        {
            return;
        }

        foreach (var page in pages.OfType<Dictionary<string, object?>>())
        {
            foreach (var node in (page.GetValueOrDefault("nodes") as List<object?> ?? []).OfType<Dictionary<string, object?>>())
            {
                foreach (var property in (node.GetValueOrDefault("properties") as Dictionary<string, object?> ?? []).Values.OfType<Dictionary<string, object?>>())
                {
                    if (property.GetValueOrDefault("kind") as string == "double" && property.GetValueOrDefault("value") is long whole)
                    {
                        property["value"] = (double)whole;
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Writes one tree value as JSON.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The value.</param>
    /// <param name="path">Where the value is in the tree, for messages.</param>
    /// <param name="depth">Current nesting depth.</param>
    private static void Write(Utf8JsonWriter writer, object? value, string path, int depth)
    {
        if (depth > PlexusJson.MaxDepth)
        {
            throw new SnapshotException($"The tree nests deeper than {PlexusJson.MaxDepth} levels at '{path}'.");
        }

        switch (value)
        {
            case null:
            case DBNull:
                writer.WriteNullValue();
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case sbyte or byte or short or ushort or int or long:
                writer.WriteNumberValue(System.Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case uint or ulong:
                writer.WriteNumberValue(System.Convert.ToUInt64(value, CultureInfo.InvariantCulture));
                break;
            case float or double or decimal:
                WriteFloating(writer, System.Convert.ToDouble(value, CultureInfo.InvariantCulture), path);
                break;
            case IEnumerable<KeyValuePair<string, object?>> pairs:
                WriteObject(writer, pairs.Select(p => (p.Key, p.Value)), path, depth);
                break;
            case IDictionary dictionary:
                WriteObject(writer, dictionary.Cast<DictionaryEntry>().Select(e => (Key: e.Key as string ?? throw NotText(path, e.Key), Value: e.Value)), path, depth);
                break;
            case IEnumerable items:
                writer.WriteStartArray();
                var index = 0;
                foreach (var item in items)
                {
                    Write(writer, item, $"{path}[{index++}]", depth + 1);
                }

                writer.WriteEndArray();
                break;
            default:
                throw new SnapshotException($"The tree holds a value of type '{value.GetType().Name}' at '{path}'; only strings, booleans, numbers, null, lists and string-keyed dictionaries are supported.");
        }
    }

    /// <summary>
    ///     Writes a finite floating-point number.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="number">The number.</param>
    /// <param name="path">Where the value is in the tree, for messages.</param>
    private static void WriteFloating(Utf8JsonWriter writer, double number, string path)
    {
        if (!double.IsFinite(number))
        {
            throw new SnapshotException($"The tree holds the non-finite number {number.ToString(CultureInfo.InvariantCulture)} at '{path}'.");
        }

        writer.WriteNumberValue(number);
    }

    /// <summary>
    ///     Writes the members of a dictionary as a JSON object.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="members">The key/value pairs.</param>
    /// <param name="path">Where the object is in the tree, for messages.</param>
    /// <param name="depth">Current nesting depth.</param>
    private static void WriteObject(Utf8JsonWriter writer, IEnumerable<(string Key, object? Value)> members, string path, int depth)
    {
        writer.WriteStartObject();
        foreach (var (key, value) in members)
        {
            writer.WritePropertyName(key);
            Write(writer, value, $"{path}.{key}", depth + 1);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    ///     Creates the error for a dictionary key that is not a string.
    /// </summary>
    /// <param name="path">Where the dictionary is in the tree.</param>
    /// <param name="key">The offending key.</param>
    /// <returns>The exception to throw.</returns>
    private static SnapshotException NotText(string path, object key) =>
        new($"The tree holds a dictionary key of type '{key.GetType().Name}' at '{path}'; keys must be strings.");
}
