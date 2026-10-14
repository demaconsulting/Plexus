using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Keeps the <see cref="NodePrototype"/> of each node type so it is built once, not on every paint or drag movement.
/// </summary>
/// <remarks>
///     Prototypes depend on the registry, the sizing theme and the scene theme, so the whole cache
///     is emptied when any of them changes. The editor's drop ghost and the node palette each own one.
///     UI thread only.
/// </remarks>
internal sealed class NodePrototypeCache
{
    /// <summary>
    ///     The prototypes by node type.
    /// </summary>
    private readonly Dictionary<NodeType, NodePrototype> _prototypes = [];

    /// <summary>
    ///     The registry the prototypes were built for.
    /// </summary>
    private NodeTypeRegistry? _registry;

    /// <summary>
    ///     The sizing theme the prototypes were built for.
    /// </summary>
    private DiagramTheme? _sizingTheme;

    /// <summary>
    ///     The scene theme the prototypes were built for.
    /// </summary>
    private DiagramTheme? _sceneTheme;

    /// <summary>
    ///     Gets how many prototypes have been built since the cache was created; lets tests prove that scenes are cached.
    /// </summary>
    public int BuildCount { get; private set; }

    /// <summary>
    ///     Forgets every prototype, for example because the host changed the registry's types.
    /// </summary>
    public void Clear()
    {
        _prototypes.Clear();
        _registry = null;
        _sizingTheme = null;
        _sceneTheme = null;
    }

    /// <summary>
    ///     Gets the prototype of a node type, building it on first use.
    /// </summary>
    /// <param name="registry">A registry that contains <paramref name="type"/>.</param>
    /// <param name="type">The node type.</param>
    /// <param name="sizingTheme">The theme whose metrics size the node box.</param>
    /// <param name="sceneTheme">The theme the scene is drawn with.</param>
    /// <param name="measurer">The text measurer.</param>
    /// <returns>The prototype.</returns>
    public NodePrototype Get(NodeTypeRegistry registry, NodeType type, DiagramTheme sizingTheme, DiagramTheme sceneTheme, ITextMeasurer measurer)
    {
        // Anything the prototypes were built against changing makes all of them stale
        if (!ReferenceEquals(registry, _registry) || !Equals(sizingTheme, _sizingTheme) || !Equals(sceneTheme, _sceneTheme))
        {
            _prototypes.Clear();
            _registry = registry;
            _sizingTheme = sizingTheme;
            _sceneTheme = sceneTheme;
        }

        if (!_prototypes.TryGetValue(type, out var prototype))
        {
            prototype = NodePrototype.Create(registry, type, sizingTheme, sceneTheme, measurer);
            _prototypes[type] = prototype;
            BuildCount++;
        }

        return prototype;
    }
}
