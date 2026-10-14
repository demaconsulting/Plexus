using System.Text.Json;
using System.Text.Json.Serialization;

namespace DemaConsulting.Plexus;

/// <summary>
///     Embeds a string that already contains JSON text as a JSON value, and reads any JSON value back as its raw text.
/// </summary>
/// <remarks>
///     Node payloads are produced by host serializers as JSON text. Writing that text as an escaped
///     string would make files unreadable, so it is parsed (which also proves it is valid JSON) and
///     written as a real nested value, re-indented with the rest of the document.
/// </remarks>
internal sealed class RawJsonStringConverter : JsonConverter<string>
{
    /// <summary>
    ///     Parse options that cap nesting at the same depth as the rest of the document.
    /// </summary>
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = PlexusJson.MaxDepth };

    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Capture whatever value follows (object, array or scalar) as its original text
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        // Parsing first rejects invalid JSON with a clear error instead of corrupting the document
        using var document = JsonDocument.Parse(value, DocumentOptions);
        document.RootElement.WriteTo(writer);
    }
}
