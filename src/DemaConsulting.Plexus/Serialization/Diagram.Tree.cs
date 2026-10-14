namespace DemaConsulting.Plexus;

/// <content>
///     The format-neutral tree: the snapshot as plain dictionaries, lists and primitive values for serializers other than JSON.
/// </content>
public sealed partial class Diagram
{
    /// <summary>
    ///     Captures the whole diagram as a tree of plain values that any serializer (TOML, YAML, MessagePack, ...) can write.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The tree has the same schema as the JSON format (see <see cref="ToJson(bool)"/>): a dictionary with
    ///         <c>formatVersion</c> and <c>pages</c>, and so on. It contains only <see cref="string"/>, <see cref="bool"/>,
    ///         <see cref="long"/>, <see cref="double"/>, <see langword="null"/>, <c>List&lt;object?&gt;</c> and
    ///         <c>Dictionary&lt;string, object?&gt;</c> values; no <c>System.Text.Json</c> types. Host data
    ///         (<see cref="Node.Data"/>) appears as nested dictionaries and lists, colors as <c>"#AARRGGBB"</c> strings and property
    ///         kinds as the plain strings <c>bool</c>, <c>int</c>, <c>long</c>, <c>double</c>, <c>string</c>, <c>choice</c> and <c>color</c>;
    ///         the value of a <c>double</c> property is always a <see cref="double"/>. Members that are absent (for example a node
    ///         without host data) are left out rather than written as null.
    ///     </para>
    ///     <para>
    ///         The result is a copy: changing it does not change the diagram. Restore with <see cref="FromTree"/>. See <see cref="ToSnapshot"/> for what is included
    ///         (ports are not: they are recomputed from the restored properties and data on load).
    ///     </para>
    /// </remarks>
    /// <example>
    ///     Store a diagram in another format by handing the tree to its serializer (here a hypothetical <c>Toml</c> class).
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var diagram = new Diagram(types);                      // types: the registry of the DemaConsulting.Plexus example
    ///     diagram.Pages.Add("Main");
    ///     IDictionary&lt;string, object?&gt; tree = diagram.ToTree();
    ///     // string toml = Toml.Write(new { Title = "My design", Diagram = tree });   // any serializer that writes dictionaries and lists
    ///     var back = Diagram.FromTree(tree, types);
    ///     </code>
    /// </example>
    /// <returns>The tree root.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a node payload does not match its declared data type or serializes to invalid JSON.</exception>
    public IDictionary<string, object?> ToTree() => DiagramTree.ToTree(ToSnapshot());

    /// <summary>
    ///     Rebuilds a diagram from a tree of plain values written by <see cref="ToTree"/> (or read back from another format).
    /// </summary>
    /// <remarks>
    ///     The parameter is typed as the sequence of key/value pairs that every dictionary type implements, so a
    ///     <see cref="Dictionary{TKey, TValue}"/>, an <see cref="IDictionary{TKey, TValue}"/> (what <see cref="ToTree"/> returns),
    ///     an <see cref="IReadOnlyDictionary{TKey, TValue}"/> or a serializer's table type can all be passed without a cast
    ///     (a single dictionary-typed parameter would make some of them ambiguous).
    ///     The input is tolerant about what a foreign serializer produces: any integer or floating-point CLR type for numbers
    ///     (a whole <see cref="double"/> is accepted where an integer is expected), <see cref="IReadOnlyDictionary{TKey, TValue}"/>,
    ///     <see cref="IDictionary{TKey, TValue}"/> or the non-generic <see cref="System.Collections.IDictionary"/> for nested objects, and
    ///     any <see cref="System.Collections.IEnumerable"/> for lists. Everything after that is exactly <see cref="FromJson(string, NodeTypeRegistry, DiagramOptions?)"/>,
    ///     including its validation. Unlike the JSON loaders, shape problems are reported as <see cref="SnapshotException"/> as well, so a host needs no JSON knowledge.
    ///     Node types the registry does not know fail the load, or are skipped with <see cref="UnknownNodeTypeHandling.Skip"/>
    ///     (see <see cref="UnknownNodeTypeHandling"/> and <see cref="LoadReport"/>).
    /// </remarks>
    /// <example>
    ///     Restore a tree, here the one just captured; a tree that a foreign serializer read back is passed the same way.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     // types: your NodeTypeRegistry
    ///     var source = new Diagram(types);
    ///     source.Pages.Add("Main");
    ///     IDictionary&lt;string, object?&gt; tree = source.ToTree();
    ///
    ///     var options = new DiagramOptions { UnknownNodeTypes = UnknownNodeTypeHandling.Skip };
    ///     Diagram restored = Diagram.FromTree(tree, types, options);
    ///     Console.WriteLine(restored.Pages.Count);                        // 1
    ///     Console.WriteLine(restored.LoadReport.HasSkipped);              // False: nothing unknown in this tree
    ///     </code>
    /// </example>
    /// <param name="tree">The tree root (the dictionary that <see cref="ToTree"/> returned, or its equivalent from another format).</param>
    /// <param name="types">The registry providing the node types the tree refers to.</param>
    /// <param name="options">The configuration to install on the loaded diagram and how to treat unknown node types; null uses every default.</param>
    /// <returns>The new diagram, with an empty undo history.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tree"/> or <paramref name="types"/> is null.</exception>
    /// <exception cref="SnapshotException">Thrown when the tree holds unsupported values, has the wrong shape, an unsupported format version, unknown node types (unless skipping), or invalid content.</exception>
    public static Diagram FromTree(IEnumerable<KeyValuePair<string, object?>> tree, NodeTypeRegistry types, DiagramOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(types);
        var json = DiagramTree.ToJson(tree);
        try
        {
            return FromJson(json, types, options);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new SnapshotException($"The tree is not a diagram: {ex.Message}");
        }
    }
}
