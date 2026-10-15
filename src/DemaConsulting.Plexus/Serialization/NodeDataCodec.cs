using System.Text.Json;

namespace DemaConsulting.Plexus;

/// <summary>
///     Converts node payloads (<see cref="Node.Data"/>) to and from JSON text using the node type's declared mechanism.
/// </summary>
/// <remarks>
///     A type's <see cref="NodeType.DataSerializer"/> wins over <see cref="NodeType.DataType"/>. With
///     <see cref="NodeType.DataType"/> the exact declared type is used for both directions, so no type
///     name is ever read from the JSON and untrusted input cannot choose what gets instantiated.
///     Types declaring neither cannot be serialized; their payload is omitted from snapshots and the
///     clipboard.
/// </remarks>
internal static class NodeDataCodec
{
    /// <summary>
    ///     Options for <see cref="NodeType.DataType"/> payloads: System.Text.Json defaults plus the depth guard.
    /// </summary>
    private static readonly JsonSerializerOptions PayloadOptions = new() { MaxDepth = PlexusJson.MaxDepth };

    /// <summary>
    ///     Parse options for validating text returned by custom serializers.
    /// </summary>
    private static readonly JsonDocumentOptions ValidationOptions = new() { MaxDepth = PlexusJson.MaxDepth };

    /// <summary>
    ///     Determines whether a node type can serialize its payload.
    /// </summary>
    /// <param name="type">The node type.</param>
    /// <returns><see langword="true"/> when a serializer or data type is declared.</returns>
    public static bool CanSerialize(NodeType type) => type.DataSerializer is not null || type.DataType is not null;

    /// <summary>
    ///     Serializes a node's payload to JSON text.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The JSON text, or null when the node has no payload or its type cannot serialize one.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the payload does not match the declared data type or the serializer returns invalid JSON.</exception>
    public static string? Serialize(Node node)
    {
        var data = node.Data;
        if (data is null || !CanSerialize(node.Type))
        {
            return null;
        }

        string json;
        if (node.Type.DataSerializer is { } serializer)
        {
            json = serializer.Serialize(data)
                ?? throw new InvalidOperationException($"The data serializer of node type '{node.Type.Key}' returned null for node '{node.Name}'.");
        }
        else
        {
            // The declared type is authoritative; a mismatching payload is a host bug worth surfacing
            var dataType = node.Type.DataType!;
            if (!dataType.IsInstanceOfType(data))
            {
                throw new InvalidOperationException(
                    $"Node '{node.Name}' holds data of type '{data.GetType().Name}' but its node type '{node.Type.Key}' declares '{dataType.Name}'.");
            }

            json = JsonSerializer.Serialize(data, dataType, PayloadOptions);
        }

        EnsureValidJson(json, node);
        return json;
    }

    /// <summary>
    ///     Deserializes JSON text into a payload for a node type.
    /// </summary>
    /// <param name="type">The node type that declares how its payload is read.</param>
    /// <param name="json">The JSON text (untrusted).</param>
    /// <param name="data">Receives the payload on success.</param>
    /// <param name="problem">Receives a description of the failure.</param>
    /// <returns><see langword="true"/> when a payload was produced.</returns>
    /// <remarks>
    ///     Any exception of the host serializer or of System.Text.Json except <see cref="OutOfMemoryException"/>, and a
    ///     null result, make the payload unreadable; the exception message becomes <paramref name="problem"/>.
    /// </remarks>
    public static bool TryDeserialize(NodeType type, string json, out object? data, out string? problem)
    {
        data = null;
        problem = null;
        try
        {
            data = type.DataSerializer is { } serializer
                ? serializer.Deserialize(json)
                : JsonSerializer.Deserialize(json, type.DataType!, PayloadOptions);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The text is untrusted and the serializer is host code: every non-fatal failure makes the payload unreadable
            problem = ex.Message;
            return false;
        }

        if (data is null)
        {
            problem = "the payload text did not describe a valid value";
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Throws when text returned by a serializer is not valid JSON, so snapshots never contain unreadable payloads.
    /// </summary>
    /// <param name="json">The text to check.</param>
    /// <param name="node">The node it came from, for the message.</param>
    /// <exception cref="InvalidOperationException">Thrown when the text is not valid JSON.</exception>
    private static void EnsureValidJson(string json, Node node)
    {
        try
        {
            using var document = JsonDocument.Parse(json, ValidationOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"The data of node '{node.Name}' (type '{node.Type.Key}') did not serialize to valid JSON: {ex.Message}", ex);
        }
    }
}
