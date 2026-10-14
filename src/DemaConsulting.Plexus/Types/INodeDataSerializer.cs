namespace DemaConsulting.Plexus;

/// <summary>
///     Converts a node's host payload (<see cref="Node.Data"/>) to and from JSON text.
/// </summary>
/// <remarks>
///     Provide an implementation on <see cref="NodeType.DataSerializer"/> when the payload cannot
///     be handled by plain System.Text.Json serialization of <see cref="NodeType.DataType"/> (which uses the default
///     <c>System.Text.Json</c> options), for example when you need camel-case names, custom converters or another
///     JSON library. Implementations are called by snapshot and clipboard code, so they must treat the JSON
///     passed to <see cref="Deserialize"/> as untrusted input.
///     <para>
///         The text returned by <see cref="Serialize"/> must be valid JSON; it is embedded as the <c>data</c> member of the node in
///         the diagram JSON and in clipboard text. If <see cref="Deserialize"/> returns null or throws a
///         <c>JsonException</c>, <c>FormatException</c>, <c>InvalidOperationException</c>, <c>ArgumentException</c>,
///         <c>NotSupportedException</c> or <c>InvalidCastException</c>, the payload counts as unreadable: <see cref="Diagram.FromJson(string, NodeTypeRegistry, DiagramOptions?)"/> throws
///         <see cref="SnapshotException"/>, and a clipboard paste inserts the node without data and reports a warning in
///         the <see cref="PasteResult"/>. Other exceptions are treated as bugs and propagate. An exception thrown from
///         <see cref="Serialize"/> propagates unchanged out of <see cref="Diagram.ToJson(bool)"/> and the clipboard copy.
///     </para>
/// </remarks>
/// <example>
///     A serializer that stores a record as camel-case JSON.
///     <code>
///     using System.Text.Json;
///     using DemaConsulting.Plexus;
///
///     var types = new NodeTypeRegistry();
///     types.Add(new NodeType("demo.Valve") { DataSerializer = new ValveSerializer() });
///
///     var diagram = new Diagram(types);
///     var valve = diagram.Pages.Add("Main").AddNode("demo.Valve", 0, 0);
///     valve.Data = new ValveData(0.5, "Inlet");
///     Console.WriteLine(diagram.ToJson());                 // the node carries "data": {"opening":0.5,"label":"Inlet"}
///
///     public sealed record ValveData(double Opening, string Label);
///
///     public sealed class ValveSerializer : INodeDataSerializer
///     {
///         private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
///
///         public string Serialize(object data) => JsonSerializer.Serialize((ValveData)data, Options);
///
///         public object? Deserialize(string json) => JsonSerializer.Deserialize&lt;ValveData&gt;(json, Options);   // null: the text was not a valid payload
///     }
///     </code>
/// </example>
public interface INodeDataSerializer
{
    /// <summary>
    ///     Serializes a payload to JSON text.
    /// </summary>
    /// <param name="data">The payload; never null.</param>
    /// <returns>The JSON text.</returns>
    string Serialize(object data);

    /// <summary>
    ///     Deserializes a payload from JSON text.
    /// </summary>
    /// <param name="json">The JSON text produced by <see cref="Serialize"/>.</param>
    /// <returns>The payload, or null when the text does not describe a valid payload.</returns>
    object? Deserialize(string json);
}
