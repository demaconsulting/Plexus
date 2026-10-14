using System.Collections;

namespace DemaConsulting.Plexus;

/// <summary>
///     The set of <see cref="PortType"/> definitions known to a diagram.
/// </summary>
/// <remarks>
///     Lookup never fails for painting purposes: unknown keys resolve to a neutral gray type so a
///     diagram with a missing registration still renders. No port types are predefined. Not thread-safe.
/// </remarks>
/// <example>
///     Register and look up port types (reached through <see cref="NodeTypeRegistry.PortTypes"/>).
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     var registry = new PortTypeRegistry();
///     registry.Add(new PortType("number", Color.Orange));
///     registry.Add(new PortType("text", Color.Parse("#4FA3FF")));
///
///     Console.WriteLine(registry.Count);                          // 2
///     Console.WriteLine(registry.Contains("number"));             // True
///     if (registry.TryGet("text", out var text)) Console.WriteLine(text!.Color);
///     foreach (var type in registry) Console.WriteLine(type.Key); // registration order
///     Console.WriteLine(registry.Get("missing").Color);           // gray fallback; never throws for a non-empty key
///     </code>
/// </example>
public sealed class PortTypeRegistry : IEnumerable<PortType>
{
    /// <summary>
    ///     Color used for port types that were never registered.
    /// </summary>
    private static readonly Color NeutralColor = new(0x9A, 0x9A, 0x9A);

    /// <summary>
    ///     Registered types by key.
    /// </summary>
    private readonly Dictionary<string, PortType> _types = new(StringComparer.Ordinal);

    /// <summary>
    ///     Registered types in registration order.
    /// </summary>
    private readonly List<PortType> _ordered = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="PortTypeRegistry"/> class as an empty registry.
    /// </summary>
    public PortTypeRegistry()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Gets the number of registered port types.
    /// </summary>
    public int Count => _ordered.Count;

    /// <summary>
    ///     Registers a port type.
    /// </summary>
    /// <param name="type">The port type to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a type with the same key is already registered.</exception>
    public void Add(PortType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!_types.TryAdd(type.Key, type))
        {
            throw new ArgumentException($"A port type with key '{type.Key}' is already registered.", nameof(type));
        }

        _ordered.Add(type);
    }

    /// <summary>
    ///     Resolves a port type, falling back to a neutral gray type when the key is not registered.
    /// </summary>
    /// <param name="key">The port type key.</param>
    /// <returns>The registered type, or a neutral gray type carrying the requested key.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or empty.</exception>
    public PortType Get(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return _types.TryGetValue(key, out var type) ? type : new PortType(key, NeutralColor);
    }

    /// <summary>
    ///     Attempts to find a registered port type.
    /// </summary>
    /// <param name="key">The port type key.</param>
    /// <param name="type">Receives the registered type when found.</param>
    /// <returns><see langword="true"/> when the key is registered.</returns>
    public bool TryGet(string key, out PortType? type)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _types.TryGetValue(key, out type);
    }

    /// <summary>
    ///     Determines whether a key is registered.
    /// </summary>
    /// <param name="key">The port type key.</param>
    /// <returns><see langword="true"/> when registered.</returns>
    public bool Contains(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _types.ContainsKey(key);
    }

    /// <summary>
    ///     Returns an enumerator over the registered port types in registration order.
    /// </summary>
    /// <returns>An enumerator; do not add types while enumerating.</returns>
    public IEnumerator<PortType> GetEnumerator() => _ordered.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
