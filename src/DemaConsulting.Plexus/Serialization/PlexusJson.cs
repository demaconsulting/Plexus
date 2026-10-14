using System.Text.Encodings.Web;
using System.Text.Json;

namespace DemaConsulting.Plexus;

/// <summary>
///     Converts <see cref="DiagramSnapshot"/> to and from JSON text.
/// </summary>
/// <remarks>
///     The JSON uses camelCase member names, string enum-like kinds and <c>"#AARRGGBB"</c> colors, and
///     contains no type names. Reading enforces a nesting depth of <see cref="MaxDepth"/> and ignores
///     members it does not know, so newer minor additions do not break older readers. Core performs no
///     file I/O: hosts pass the text to and from their own storage.
/// </remarks>
public static class PlexusJson
{
    /// <summary>
    ///     The maximum JSON nesting depth accepted when reading and when parsing embedded payloads.
    /// </summary>
    internal const int MaxDepth = 64;

    /// <summary>
    ///     Context producing indented (human-readable) JSON.
    /// </summary>
    private static readonly PlexusJsonContext IndentedContext = CreateContext(indented: true);

    /// <summary>
    ///     Context producing compact JSON; also used for reading and for the clipboard.
    /// </summary>
    private static readonly PlexusJsonContext CompactContext = CreateContext(indented: false);

    /// <summary>
    ///     Gets the compact context shared by clipboard and property-value code.
    /// </summary>
    internal static PlexusJsonContext Compact => CompactContext;

    /// <summary>
    ///     Serializes a snapshot to JSON.
    /// </summary>
    /// <param name="snapshot">The snapshot to write.</param>
    /// <param name="indented">True for human-readable output; false for the most compact text.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> is null.</exception>
    public static string Serialize(DiagramSnapshot snapshot, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var context = indented ? IndentedContext : CompactContext;
        return JsonSerializer.Serialize(snapshot, context.DiagramSnapshot);
    }

    /// <summary>
    ///     Parses JSON text into a snapshot.
    /// </summary>
    /// <remarks>
    ///     Only the shape is validated here (and the format version); whether the content can be
    ///     loaded is decided by <see cref="Diagram.FromSnapshot"/>.
    /// </remarks>
    /// <param name="json">The JSON text.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">Thrown when the text is not valid JSON of the expected shape, or nests deeper than 64 levels.</exception>
    /// <exception cref="SnapshotException">Thrown when the document declares an unsupported format version.</exception>
    public static DiagramSnapshot Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var snapshot = JsonSerializer.Deserialize(json, CompactContext.DiagramSnapshot);
        return CheckVersion(snapshot);
    }

    /// <summary>
    ///     Writes a snapshot as UTF-8 JSON to a stream, without closing it.
    /// </summary>
    /// <param name="snapshot">The snapshot to write.</param>
    /// <param name="stream">The destination.</param>
    /// <param name="indented">Whether to indent the output.</param>
    internal static void SerializeTo(DiagramSnapshot snapshot, Stream stream, bool indented)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var context = indented ? IndentedContext : CompactContext;
        JsonSerializer.Serialize(stream, snapshot, context.DiagramSnapshot);
    }

    /// <summary>
    ///     Reads a snapshot from UTF-8 JSON in a stream, without closing it.
    /// </summary>
    /// <param name="stream">The source.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="JsonException">Thrown when the content is not valid JSON of the expected shape.</exception>
    /// <exception cref="SnapshotException">Thrown when the document declares an unsupported format version.</exception>
    internal static DiagramSnapshot DeserializeFrom(Stream stream) =>
        CheckVersion(JsonSerializer.Deserialize(stream, CompactContext.DiagramSnapshot));

    /// <summary>
    ///     Rejects a missing document or an unsupported format version.
    /// </summary>
    /// <param name="snapshot">The parsed snapshot, or null for a JSON <c>null</c>.</param>
    /// <returns>The same snapshot.</returns>
    private static DiagramSnapshot CheckVersion(DiagramSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            throw new JsonException("The JSON document is null; a diagram snapshot object was expected.");
        }

        if (snapshot.FormatVersion != DiagramSnapshot.CurrentFormatVersion)
        {
            throw new SnapshotException(
                $"Unsupported snapshot format version {snapshot.FormatVersion}; this library reads version {DiagramSnapshot.CurrentFormatVersion}.");
        }

        return snapshot;
    }

    /// <summary>
    ///     Creates a context with the shared reader and writer settings.
    /// </summary>
    /// <param name="indented">Whether output is indented.</param>
    /// <returns>The context.</returns>
    private static PlexusJsonContext CreateContext(bool indented)
    {
        // Names must be repeated here: the generated metadata is built from the attribute, but the
        // options instance owns runtime behavior such as indentation and the depth guard
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = indented,
            MaxDepth = MaxDepth,

            // Not embedded in HTML, so keep non-ASCII text readable instead of \u-escaping it
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        return new PlexusJsonContext(options);
    }
}
