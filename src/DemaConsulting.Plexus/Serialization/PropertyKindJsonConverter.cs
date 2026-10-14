using System.Text.Json;
using System.Text.Json.Serialization;

namespace DemaConsulting.Plexus;

/// <summary>
///     Reads and writes a <see cref="PropertyValueKind"/> as its short portable name
///     (<c>bool</c>, <c>int</c>, <c>long</c>, <c>double</c>, <c>string</c>, <c>choice</c>, <c>color</c>).
/// </summary>
/// <remarks>
///     An explicit mapping keeps the persisted names independent of the C# enum member names, so
///     renaming an enum member can never silently change the file format.
/// </remarks>
internal sealed class PropertyKindJsonConverter : JsonConverter<PropertyValueKind>
{
    /// <inheritdoc />
    public override PropertyValueKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Only a string token can name a kind; anything else is a malformed document
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return text switch
        {
            "bool" => PropertyValueKind.Boolean,
            "int" => PropertyValueKind.Int32,
            "long" => PropertyValueKind.Int64,
            "double" => PropertyValueKind.Double,
            "string" => PropertyValueKind.String,
            "choice" => PropertyValueKind.Choice,
            "color" => PropertyValueKind.Color,
            _ => throw new JsonException($"'{text}' is not a known property kind."),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, PropertyValueKind value, JsonSerializerOptions options)
    {
        var text = value switch
        {
            PropertyValueKind.Boolean => "bool",
            PropertyValueKind.Int32 => "int",
            PropertyValueKind.Int64 => "long",
            PropertyValueKind.Double => "double",
            PropertyValueKind.String => "string",
            PropertyValueKind.Choice => "choice",
            PropertyValueKind.Color => "color",
            _ => throw new JsonException($"Property kind {value} cannot be written."),
        };
        writer.WriteStringValue(text);
    }
}
