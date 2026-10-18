using System.Collections;

namespace DemaConsulting.Plexus;

/// <summary>
///     The set of <see cref="NodeType"/> definitions a diagram can instantiate, plus the port types they use.
/// </summary>
/// <remarks>
///     Registration validates a node type and freezes it. A registry is normally filled once at
///     start-up and then shared by diagrams; adding types later is allowed but not undoable because
///     the registry is configuration, not document content. A frozen <see cref="NodeType"/> instance can be registered in several
///     registries. The registry stores no port types of its own beyond <see cref="PortTypes"/>, which are optional: JSON and
///     the clipboard store only port type keys, so a registry that lacks a port type simply draws those ports gray. Port types
///     are not predefined; register your own. Not thread-safe.
/// </remarks>
/// <example>
///     Register port types first, then the node types that use them; a diagram is created from the registry.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     var types = new NodeTypeRegistry();
///     types.PortTypes.Add(new PortType("number", Color.Parse("#FF8C00")));
///     types.PortTypes.Add(new PortType("bool", Color.Parse("#2EBF5B")));
///     types.Add(new NodeType("logic.Compare")
///     {
///         Category = "Logic",
///         Inputs = { new PortDefinition("a", "number"), new PortDefinition("b", "number") },
///         Outputs = { new PortDefinition("result", "bool") },
///     });
///
///     NodeType compare = types.Get("logic.Compare");       // throws when the key is unknown
///     bool known = types.TryGet("logic.Missing", out _);   // false
///     foreach (var type in types)                           // registration order, e.g. to fill a palette
///     {
///         Console.WriteLine($"{type.Category}: {type.Title}");
///     }
///
///     var diagram = new Diagram(types);
///     </code>
/// </example>
public sealed class NodeTypeRegistry : IEnumerable<NodeType>
{
    /// <summary>
    ///     Registered types by key.
    /// </summary>
    private readonly Dictionary<string, NodeType> _types = new(StringComparer.Ordinal);

    /// <summary>
    ///     Registered types in registration order.
    /// </summary>
    private readonly List<NodeType> _ordered = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeTypeRegistry"/> class as an empty registry.
    /// </summary>
    public NodeTypeRegistry()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Gets the port types referenced by node type ports.
    /// </summary>
    public PortTypeRegistry PortTypes { get; } = [];

    /// <summary>
    ///     Gets the number of registered node types.
    /// </summary>
    public int Count => _ordered.Count;

    /// <summary>
    ///     Registers a node type, validating it and freezing its port and property lists.
    /// </summary>
    /// <param name="type">The node type.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the key is already registered or the type declares duplicate port or property names.</exception>
    public void Add(NodeType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (_types.ContainsKey(type.Key))
        {
            throw new ArgumentException($"A node type with key '{type.Key}' is already registered.", nameof(type));
        }

        // Validate before inserting so a rejected type leaves the registry untouched
        type.ValidateAndFreeze();
        _types.Add(type.Key, type);
        _ordered.Add(type);
    }

    /// <summary>
    ///     Unregisters a node type.
    /// </summary>
    /// <remarks>
    ///     Like <see cref="Add"/> this is configuration, not document content, and is not undoable. Nodes
    ///     that already exist keep their type object, but no new node of the type can be added (and
    ///     snapshots mentioning the key fail to load) until a type with that key is registered again.
    ///     Remove types before building the diagram that uses the registry, for example to start from a
    ///     shared registry and drop the types a document should not offer.
    /// </remarks>
    /// <param name="key">The node type key.</param>
    /// <returns><see langword="true"/> when the type was registered and has been removed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    public bool Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_types.Remove(key, out var type))
        {
            return false;
        }

        _ordered.Remove(type);
        return true;
    }

    /// <summary>
    ///     Gets a registered node type.
    /// </summary>
    /// <param name="key">The node type key.</param>
    /// <returns>The node type.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not registered.</exception>
    public NodeType Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _types.TryGetValue(key, out var type)
            ? type
            : throw new KeyNotFoundException($"No node type with key '{key}' is registered.");
    }

    /// <summary>
    ///     Attempts to get a registered node type.
    /// </summary>
    /// <param name="key">The node type key.</param>
    /// <param name="type">Receives the node type when found.</param>
    /// <returns><see langword="true"/> when the key is registered.</returns>
    public bool TryGet(string key, out NodeType? type)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _types.TryGetValue(key, out type);
    }

    /// <summary>
    ///     Determines whether a key is registered.
    /// </summary>
    /// <param name="key">The node type key.</param>
    /// <returns><see langword="true"/> when registered.</returns>
    public bool Contains(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _types.ContainsKey(key);
    }

    /// <summary>
    ///     Returns an enumerator over the node types in registration order, for example to fill a palette or a menu.
    /// </summary>
    /// <returns>An enumerator; do not add or remove types while enumerating.</returns>
    public IEnumerator<NodeType> GetEnumerator() => _ordered.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
