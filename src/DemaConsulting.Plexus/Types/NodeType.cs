namespace DemaConsulting.Plexus;

/// <summary>
///     Describes a kind of node a host application offers: its title, ports, properties and presentation hints.
/// </summary>
/// <remarks>
///     Create the type with an object initializer, then register it in a <see cref="NodeTypeRegistry"/>.
///     Registration validates the declaration and freezes the port and property lists, because live
///     nodes derive their ports from them. A frozen type is immutable, so the same instance may be shared
///     by several registries (and therefore diagrams). Not thread-safe.
/// </remarks>
/// <example>
///     A node type with two typed inputs, one output, editable properties, an icon and a summary shown on the node.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     var add = new NodeType("math.Add")                    // key: a unique "namespace.Name" string of your choosing
///     {
///         Title = "ADD",                                    // heading drawn in the node header (default: "Add", the key's last segment)
///         DisplayName = "Add two numbers",                  // name shown in palettes (default: Title)
///         Category = "Math",                                // palette grouping
///         Icon = IconSource.FromSvg("&lt;svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"&gt;&lt;path d=\"M12 5v14M5 12h14\" stroke=\"#fff\" stroke-width=\"2\"/&gt;&lt;/svg&gt;"),
///         Inputs = { new PortDefinition("a", "number"), new PortDefinition("b", "number") },
///         Outputs = { new PortDefinition("sum", "number") { Label = "a + b" } },
///         Properties = { new PropertyDefinition("Precision", 2) { ShowInSummary = true } },
///         Summary = node =&gt; [$"precision {node.Properties.Get&lt;int&gt;("Precision")}"],
///     };
///
///     var types = new NodeTypeRegistry();
///     types.PortTypes.Add(new PortType("number", Color.Parse("#FF8C00")));
///     types.Add(add);                                       // validates the declaration and freezes it
///     </code>
///     <para>
///         Attach your own typed payload to nodes with <see cref="DataType"/>. The record below is stored in <see cref="Node.Data"/>,
///         copied by the clipboard and saved in the diagram JSON:
///     </para>
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     var types = new NodeTypeRegistry();
///     types.Add(new NodeType("demo.Valve") { DataType = typeof(ValveData) });   // the exact CLR type of the payload
///
///     var diagram = new Diagram(types);
///     var page = diagram.Pages.Add("Main");
///     var valve = page.AddNode("demo.Valve", 0, 0);
///     valve.Data = new ValveData(Opening: 0.5, Label: "Inlet");                // undoable; replace, do not mutate
///     valve.Data = (ValveData)valve.Data with { Opening = 1.0 };               // derive a changed copy
///
///     string json = diagram.ToJson();                                          // the payload is saved with the node
///     var loaded = Diagram.FromJson(json, types);
///     var back = (ValveData?)loaded.FindNode(valve.Name)!.Data;               // Opening == 1.0
///
///     public sealed record ValveData(double Opening, string Label);
///     </code>
/// </example>
public sealed partial class NodeType
{
    /// <summary>
    ///     Optional explicit title; null means the key's last segment.
    /// </summary>
    private string? _title;

    /// <summary>
    ///     Optional explicit display name; null or empty means <see cref="Title"/>.
    /// </summary>
    private string? _displayName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeType"/> class.
    /// </summary>
    /// <param name="key">The opaque, unique key, namespaced by convention (for example <c>acme.Producer</c>).</param>
    /// <exception cref="ArgumentException">Thrown when the key is null, empty or whitespace.</exception>
    public NodeType(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        InputList = new FreezableList<PortDefinition>();
        OutputList = new FreezableList<PortDefinition>();
        PropertyList = new FreezableList<PropertyDefinition>();
    }

    /// <summary>
    ///     Gets the unique key that identifies the node type, for example <c>"acme.Producer"</c>.
    /// </summary>
    /// <remarks>
    ///     The key is an opaque, case-sensitive string chosen by the host. Plexus never parses it except for convenience:
    ///     by convention keys are namespaced (<c>"{vendor-or-domain}.{Name}"</c>), the last dot-separated segment is the
    ///     default <see cref="Title"/> and the base of generated node names (<c>Producer1</c>, <c>Producer2</c>, ...).
    ///     The key is what JSON and clipboard data store, so keep it stable across releases of your application.
    /// </remarks>
    public string Key { get; }

    /// <summary>
    ///     Gets or initializes the heading drawn in the node's header; defaults to the last segment of <see cref="Key"/>.
    /// </summary>
    /// <remarks>Set with an object initializer.</remarks>
    public string Title
    {
        get => _title ?? ShortName;
        init => _title = value;
    }

    /// <summary>
    ///     Gets or initializes the host-facing name for palettes, menus and accessibility names; defaults to <see cref="Title"/>. Set with an object initializer.
    /// </summary>
    /// <remarks>
    ///     Several types may share the same <see cref="Title"/> in the node header (for example two
    ///     producers that differ only in their data type); the display name lets hosts tell them apart
    ///     wherever types are listed. A null or empty value falls back to <see cref="Title"/>. The
    ///     library itself never draws it on a node.
    /// </remarks>
    public string DisplayName
    {
        get => string.IsNullOrEmpty(_displayName) ? Title : _displayName;
        init => _displayName = value;
    }

    /// <summary>
    ///     Gets or initializes the optional group name hosts use to organize types in palettes and menus; null (the default) means uncategorized. Set with an object initializer.
    /// </summary>
    /// <remarks>
    ///     The library never draws or interprets it on a node: it is a presentation hint, like
    ///     <see cref="DisplayName"/>. Types that share a category (compared ordinally) are listed
    ///     together, for example by the Avalonia <c>NodePalette</c>. A null, empty or whitespace
    ///     value means the type has no category.
    /// </remarks>
    public string? Category { get; init; }

    /// <summary>
    ///     Gets or initializes the optional icon drawn in the node's header; null (the default) draws no icon. Set with an object initializer.
    /// </summary>
    /// <remarks>
    ///     The icon is an SVG scaled into a small square slot of the node header. It is drawn as authored on the dark node fill
    ///     (unless a <see cref="Scene.NodeStyle.IconTint"/> recolors it), so use light colors; white strokes such as
    ///     <c>stroke="#fff"</c> work well. Give the SVG a <c>viewBox</c> (for example <c>0 0 24 24</c>) so it scales.
    ///     See <see cref="IconSource"/> for loading from text, a file or an embedded resource and for the SVG features to stick to.
    /// </remarks>
    public IconSource? Icon { get; init; }

    /// <summary>
    ///     Gets the input port declarations in top-to-bottom order; frozen after registration.
    /// </summary>
    public IList<PortDefinition> Inputs => InputList;

    /// <summary>
    ///     Gets the output port declarations in top-to-bottom order; frozen after registration.
    /// </summary>
    public IList<PortDefinition> Outputs => OutputList;

    /// <summary>
    ///     Gets the property declarations in display order; frozen after registration.
    /// </summary>
    public IList<PropertyDefinition> Properties => PropertyList;

    /// <summary>
    ///     Gets the size new nodes of this type start with; null (the default) means the theme's default node size, grown to fit the ports.
    /// </summary>
    /// <remarks>Set with an object initializer. Existing nodes are not affected when it changes.</remarks>
    public Size? DefaultSize { get; init; }

    /// <summary>
    ///     Gets or initializes a value indicating whether nodes of this type are meant to be resized by the user; the default is false.
    /// </summary>
    /// <remarks>
    ///     This is a hint for hosts that build their own resize UI. The model does not enforce it (code can always set
    ///     <see cref="Node.Bounds"/>) and the Avalonia <c>DiagramEditor</c> offers resize handles for annotations only, not for nodes.
    /// </remarks>
    public bool IsResizable { get; init; }

    /// <summary>
    ///     Gets or initializes a function producing the summary lines drawn below a node; null (the default) selects the default summary.
    /// </summary>
    /// <remarks>
    ///     The function is called with the node each time the scene is built, so the lines follow property changes. The default
    ///     summary lists the properties flagged <see cref="PropertyDefinition.ShowInSummary"/>; see <see cref="GetSummaryLines"/>.
    ///     Keep the function fast and free of side effects, and return one string per line (no line breaks inside a line).
    /// </remarks>
    public Func<Node, IEnumerable<string>>? Summary { get; init; }

    /// <summary>
    ///     Gets or initializes the exact CLR type of the host payload stored in <see cref="Node.Data"/>; null (the default) means the type has no serializable payload.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         With a <see cref="DataType"/> the payload is saved with the node in the diagram JSON and copied by the clipboard, using
    ///         <c>System.Text.Json</c> with its default options (property names as declared, public properties and positional
    ///         record constructors; no custom converters or naming policy) plus a maximum nesting depth. The declared type is
    ///         always the one deserialized: no type name is read from the JSON, so a document cannot choose what gets created.
    ///         A payload that is not an instance of this type (an instance of a derived type is accepted, but is written as the declared type) makes <see cref="Diagram.ToJson(bool)"/> throw
    ///         <see cref="InvalidOperationException"/>. When reading JSON back, text that cannot be turned into the type makes
    ///         <see cref="Diagram.FromJson(string, NodeTypeRegistry, DiagramOptions?)"/> throw <see cref="SnapshotException"/>; when pasting from the clipboard the node is pasted without data and the
    ///         <see cref="PasteResult"/> carries a warning.
    ///     </para>
    ///     <para>
    ///         Use <see cref="DataSerializer"/> instead when you need other JSON settings. Nodes whose type declares neither have
    ///         their <see cref="Node.Data"/> left out of JSON and of the clipboard.
    ///     </para>
    /// </remarks>
    public Type? DataType { get; init; }

    /// <summary>
    ///     Gets or initializes a custom serializer for the host payload; takes precedence over <see cref="DataType"/>. Null (the default) means none.
    /// </summary>
    /// <remarks>
    ///     Use it when the payload needs its own JSON settings or format. <see cref="INodeDataSerializer.Serialize"/> must return valid JSON
    ///     (anything else makes <see cref="Diagram.ToJson(bool)"/> throw <see cref="InvalidOperationException"/>); see
    ///     <see cref="INodeDataSerializer"/> for an example and for how failures on load are reported.
    /// </remarks>
    public INodeDataSerializer? DataSerializer { get; init; }

    /// <summary>
    ///     Gets the last dot-separated segment of the key, used for the default title and auto-naming.
    /// </summary>
    internal string ShortName
    {
        get
        {
            var index = Key.LastIndexOf('.');
            var segment = index >= 0 && index < Key.Length - 1 ? Key[(index + 1)..] : Key;
            return segment.Trim();
        }
    }

    /// <summary>
    ///     Gets the concrete input list.
    /// </summary>
    private FreezableList<PortDefinition> InputList { get; }

    /// <summary>
    ///     Gets the concrete output list.
    /// </summary>
    private FreezableList<PortDefinition> OutputList { get; }

    /// <summary>
    ///     Gets the concrete property list.
    /// </summary>
    private FreezableList<PropertyDefinition> PropertyList { get; }

    /// <summary>
    ///     Finds a declared property by name.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>The definition, or null when the type does not declare it.</returns>
    public PropertyDefinition? FindProperty(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return PropertyList.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Produces the summary lines for a node, using <see cref="Summary"/> when set.
    /// </summary>
    /// <remarks>
    ///     The default summary lists every property flagged <see cref="PropertyDefinition.ShowInSummary"/>
    ///     as <c>DisplayName: value [unit]</c> in declaration order.
    /// </remarks>
    /// <param name="node">The node to summarize.</param>
    /// <returns>The summary lines.</returns>
    public IReadOnlyList<string> GetSummaryLines(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (Summary is { } custom)
        {
            return [.. custom(node)];
        }

        return
        [
            .. PropertyList
                .Where(p => p.ShowInSummary)
                .Select(p => p.FormatSummary(node.Properties[p.Name])),
        ];
    }

    /// <summary>
    ///     Validates the declaration and freezes the lists; called when the type is registered.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when port or property names are duplicated.</exception>
    internal void ValidateAndFreeze()
    {
        // Names must be unique within a direction; an input and an output may share a name (Node.Port then needs Input/Output)
        var duplicatePort = InputList.GroupBy(p => p.Name, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1)
            ?? OutputList.GroupBy(p => p.Name, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicatePort is not null)
        {
            throw new ArgumentException($"Node type '{Key}' declares port '{duplicatePort.Key}' more than once in the same direction.");
        }

        var duplicateProperty = PropertyList.GroupBy(p => p.Name, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicateProperty is not null)
        {
            throw new ArgumentException($"Node type '{Key}' declares property '{duplicateProperty.Key}' more than once.");
        }

        InputList.Freeze();
        OutputList.Freeze();
        PropertyList.Freeze();
    }
}
