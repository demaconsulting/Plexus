namespace DemaConsulting.Plexus;

/// <content>
///     Snapshots: capturing the diagram as plain records and rebuilding a diagram from them.
/// </content>
public sealed partial class Diagram
{
    /// <summary>
    ///     Captures the whole diagram as a snapshot.
    /// </summary>
    /// <remarks>
    ///     The snapshot holds pages, elements, routes, groups (with collapsed state), properties and node
    ///     payloads (for node types that declare <see cref="NodeType.DataType"/> or
    ///     <see cref="NodeType.DataSerializer"/>; other payloads are omitted). History, theme, router and
    ///     connection rules are not part of a snapshot. Capturing does not change the diagram.
    /// </remarks>
    /// <returns>The snapshot.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a node payload does not match its declared data type or serializes to invalid JSON.</exception>
    public DiagramSnapshot ToSnapshot() => new()
    {
        FormatVersion = DiagramSnapshot.CurrentFormatVersion,
        Pages = [.. Pages.Select(p => p.ToSnapshot())],
    };

    /// <summary>
    ///     Serializes the whole diagram to JSON text: shorthand for <see cref="PlexusJson.Serialize"/> of <see cref="ToSnapshot"/>.
    /// </summary>
    /// <remarks>
    ///     The text is the interchange format of Plexus; hosts write it to their own files. Load it
    ///     again with <see cref="FromJson(string, NodeTypeRegistry, DiagramOptions?)"/>. See <see cref="ToSnapshot"/> for what is included.
    /// </remarks>
    /// <example>
    ///     Write the diagram to a file or to compact text.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var diagram = new Diagram(types);                                          // types: the registry of the DemaConsulting.Plexus example
    ///     diagram.Pages.Add("Main");
    ///     File.WriteAllText("diagram.json", diagram.ToJson());                       // indented, human-readable
    ///     string compact = diagram.ToJson(indented: false);                          // smallest text, e.g. for a database
    ///     </code>
    /// </example>
    /// <param name="indented">True for human-readable output; false for the most compact text.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a node payload does not match its declared data type or serializes to invalid JSON.</exception>
    public string ToJson(bool indented = true) => PlexusJson.Serialize(ToSnapshot(), indented);

    /// <summary>
    ///     Writes the whole diagram as UTF-8 JSON to a stream, which is left open.
    /// </summary>
    /// <remarks>
    ///     Platform file pickers hand out streams rather than paths; this writes the same document as <see cref="ToJson(bool)"/>
    ///     (without a byte-order mark). The stream is neither flushed nor closed by this method, so the caller decides when to
    ///     dispose it. Read the document back with <see cref="FromJson(Stream, NodeTypeRegistry, DiagramOptions?)"/>.
    /// </remarks>
    /// <example>
    ///     Save to a stream and load from it again.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var diagram = new Diagram(types);                      // types: the registry of the DemaConsulting.Plexus example
    ///     diagram.Pages.Add("Main");
    ///     using var stream = new MemoryStream();                 // or the stream of a file picker
    ///     diagram.ToJson(stream);                                // UTF-8, the stream stays open
    ///     stream.Position = 0;
    ///     var loaded = Diagram.FromJson(stream, types);
    ///     </code>
    /// </example>
    /// <param name="stream">A writable stream; it is not closed.</param>
    /// <param name="indented">True for human-readable output; false for the most compact text.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a node payload does not match its declared data type or serializes to invalid JSON.</exception>
    public void ToJson(Stream stream, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        PlexusJson.SerializeTo(ToSnapshot(), stream, indented);
    }

    /// <summary>
    ///     Rebuilds a diagram from JSON text written by <see cref="ToJson(bool)"/>: shorthand for <see cref="PlexusJson.Deserialize"/> followed by <see cref="FromSnapshot"/>.
    /// </summary>
    /// <remarks>
    ///     A file that mentions node types the registry does not know is refused with a <see cref="SnapshotException"/> that lists them, unless
    ///     <see cref="DiagramOptions.UnknownNodeTypes"/> is <see cref="UnknownNodeTypeHandling.Skip"/>: then those nodes, their wires and group memberships are left out
    ///     and <see cref="LoadReport"/> says what (see <see cref="UnknownNodeTypeHandling"/> for how to produce such a file for a test).
    /// </remarks>
    /// <example>
    ///     The registry is not stored in the JSON, so pass one that contains every node type the file uses. Stylers, the
    ///     theme and other configuration are not stored either; pass them in a <see cref="DiagramOptions"/>.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///     using DemaConsulting.Plexus.Scene;
    ///
    ///     // types: the registry of the DemaConsulting.Plexus example; "diagram.json" was written by Diagram.ToJson
    ///     var options = new DiagramOptions { WireStyler = WireStylers.ByPortType() };
    ///     try
    ///     {
    ///         var loaded = Diagram.FromJson(File.ReadAllText("diagram.json"), types, options);
    ///     }
    ///     catch (SnapshotException ex)
    ///     {
    ///         Console.WriteLine($"Cannot restore the diagram: {ex.Message}");   // unknown node type, bad version, ...
    ///     }
    ///     catch (System.Text.Json.JsonException ex)
    ///     {
    ///         Console.WriteLine($"Not a diagram file: {ex.Message}");           // malformed JSON (System.Text.Json namespace)
    ///     }
    ///     </code>
    /// </example>
    /// <param name="json">The JSON text.</param>
    /// <param name="types">The registry providing the node types the JSON refers to.</param>
    /// <param name="options">The configuration to install on the loaded diagram and how to treat unknown node types; null uses every default.</param>
    /// <returns>The new diagram, with an empty undo history.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> or <paramref name="types"/> is null.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the text is not valid JSON of the expected shape.</exception>
    /// <exception cref="SnapshotException">Thrown when the format version is unsupported, node types are missing (unless skipping), or the content is invalid.</exception>
    public static Diagram FromJson(string json, NodeTypeRegistry types, DiagramOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(types);
        return FromSnapshot(PlexusJson.Deserialize(json), types, options);
    }

    /// <summary>
    ///     Rebuilds a diagram from UTF-8 JSON read from a stream written by <see cref="ToJson(Stream, bool)"/> (or any UTF-8 text from <see cref="ToJson(bool)"/>).
    /// </summary>
    /// <remarks>
    ///     Reads to the end of the JSON document and leaves the stream open. A UTF-8 byte-order mark is accepted. Otherwise
    ///     identical to <see cref="FromJson(string, NodeTypeRegistry, DiagramOptions?)"/>.
    /// </remarks>
    /// <param name="stream">A readable stream positioned at the start of the document; it is not closed.</param>
    /// <param name="types">The registry providing the node types the JSON refers to.</param>
    /// <param name="options">The configuration to install on the loaded diagram and how to treat unknown node types; null uses every default.</param>
    /// <returns>The new diagram, with an empty undo history.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> or <paramref name="types"/> is null.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the content is not valid JSON of the expected shape.</exception>
    /// <exception cref="SnapshotException">Thrown when the format version is unsupported, node types are missing (unless skipping), or the content is invalid.</exception>
    public static Diagram FromJson(Stream stream, NodeTypeRegistry types, DiagramOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(types);
        return FromSnapshot(PlexusJson.DeserializeFrom(stream), types, options);
    }

    /// <summary>
    ///     Rebuilds a diagram from a snapshot.
    /// </summary>
    /// <remarks>
    ///     Ids, names, bounds, stored wire routes (the router is never consulted), z-order, groups and
    ///     their collapsed state, property values and node payloads are restored exactly. The new
    ///     diagram's undo history is empty and clean. Node names are restored as stored; uniqueness is
    ///     not re-checked so snapshots of diagrams that allowed duplicate names still load. Node payloads are restored as stored:
    ///     <see cref="NodeType.CreateData"/> is not consulted, so a node saved without data loads without data.
    ///     <see cref="DiagramOptions.Initialize"/> is invoked once the diagram is completely loaded. Node types the registry does not know fail the load with a
    ///     <see cref="SnapshotException"/> unless <see cref="DiagramOptions.UnknownNodeTypes"/> is <see cref="UnknownNodeTypeHandling.Skip"/> (see <see cref="LoadReport"/>).
    /// </remarks>
    /// <param name="snapshot">The snapshot to load.</param>
    /// <param name="types">The registry providing the node types the snapshot refers to.</param>
    /// <param name="options">The configuration to install on the new diagram and how to treat unknown node types; null uses every default.</param>
    /// <returns>The new diagram.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> or <paramref name="types"/> is null.</exception>
    /// <exception cref="SnapshotException">Thrown when the format version is unsupported, node types are missing (all listed in the exception; unless skipping), or the content is invalid.</exception>
    public static Diagram FromSnapshot(DiagramSnapshot snapshot, NodeTypeRegistry types, DiagramOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(types);
        if (snapshot.FormatVersion != DiagramSnapshot.CurrentFormatVersion)
        {
            throw new SnapshotException(
                $"Unsupported snapshot format version {snapshot.FormatVersion}; this library reads version {DiagramSnapshot.CurrentFormatVersion}.");
        }

        // Report every unknown node type across all pages at once, before building anything
        var pages = snapshot.Pages ?? [];
        var report = LoadReport.Empty;
        var missing = ElementImporter.FindMissingTypes(pages.Where(p => p is not null).SelectMany(p => p.Nodes ?? []), types);
        if (missing.Count > 0)
        {
            if (options?.UnknownNodeTypes != UnknownNodeTypeHandling.Skip)
            {
                throw new SnapshotException($"The snapshot uses node types that are not registered: {string.Join(", ", missing)}.", missing);
            }

            // Skipping removes the unknown nodes and whatever depended on them, then loads the rest normally
            pages = SnapshotFilter.RemoveUnknownTypes(pages, types, missing, out report);
        }

        // Loading is not a user edit, so apply everything without recording history
        // The initialize callback runs after the load (below), so handlers never observe a half-built diagram
        var diagram = new Diagram(types, options is null ? null : options with { Initialize = null }) { LoadReport = report };
        using (diagram.History.Suspend())
        {
            foreach (var pageSnapshot in pages)
            {
                diagram.LoadPage(pageSnapshot);
            }
        }

        // Suspended changes leave the history marked dirty; a fresh load must be empty and clean
        diagram.History.Clear();
        options?.Initialize?.Invoke(diagram);
        return diagram;
    }

    /// <summary>
    ///     Creates a page from its snapshot while history is suspended.
    /// </summary>
    /// <param name="snapshot">The page snapshot.</param>
    /// <exception cref="SnapshotException">Thrown when the snapshot is invalid.</exception>
    private void LoadPage(PageSnapshot snapshot)
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.Name))
        {
            throw new SnapshotException("A page in the snapshot has no name.");
        }

        var page = Pages.Add(snapshot.Name);
        var content = new ElementContent(snapshot.Nodes, snapshot.Wires, snapshot.Annotations, snapshot.Groups, snapshot.ZOrder);
        var prepared = ElementImporter.Prepare(page, content, new ImportOptions { Restore = true });
        if (!prepared.Success)
        {
            throw new SnapshotException($"Page '{snapshot.Name}': {prepared.Error}", prepared.MissingNodeTypes);
        }

        Perform("Load page", plan => prepared.Apply(page, plan, exactZOrder: true));
    }
}
