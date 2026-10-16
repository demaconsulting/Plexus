namespace DemaConsulting.Plexus;

/// <summary>
///     Describes a kind of data carried by ports (for example "f32"), including the color ports of that kind are drawn with.
/// </summary>
/// <remarks>
///     Port type keys are opaque host strings, and no port types are predefined: <c>"number"</c>, <c>"bool"</c> and
///     the like exist only because your application registers them. Registering a port type is optional: ports may refer
///     to unregistered keys, in which case <see cref="PortTypeRegistry.Get"/> supplies a neutral gray type. A port type has
///     no behavior of its own; it supplies the color of the port triangles, and of the wires when you install
///     <c>WireStylers.ByPortType()</c> as <see cref="Diagram.WireStyler"/>; it takes part in connection rules only when you add
///     <see cref="ConnectionRules.SameType"/> or your own rule. Port types are not stored in JSON or on the clipboard.
/// </remarks>
/// <example>
///     Register the port types of your domain on the registry that node types use.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     var types = new NodeTypeRegistry();
///     types.PortTypes.Add(new PortType("number", Color.Parse("#FF8C00")));   // keys are yours to choose
///     types.PortTypes.Add(new PortType("bool", Color.FromRgb(0x2E, 0xBF, 0x5B)) { DisplayName = "Boolean" });   // shown in port tooltips
///     types.Add(new NodeType("logic.Compare")
///     {
///         Inputs = { new PortDefinition("a", "number"), new PortDefinition("b", "number") },
///         Outputs = { new PortDefinition("result", "bool") },
///     });
///
///     Console.WriteLine(types.PortTypes.Get("bool").Color);       // #2EBF5B
///     Console.WriteLine(types.PortTypes.Get("unknown").Color);    // #9A9A9A: unregistered keys are neutral gray
///     </code>
/// </example>
public sealed class PortType
{
    /// <summary>
    ///     Optional explicit display name; null or empty means <see cref="Key"/>.
    /// </summary>
    private string? _displayName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PortType"/> class.
    /// </summary>
    /// <param name="key">The unique, non-empty type key.</param>
    /// <param name="color">The color ports of this type are drawn with.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty or whitespace.</exception>
    public PortType(string key, Color color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        Color = color;
    }

    /// <summary>
    ///     Gets the opaque type key referenced by <see cref="PortDefinition.TypeKey"/>.
    /// </summary>
    public string Key { get; }

    /// <summary>
    ///     Gets the color ports of this type are drawn with.
    /// </summary>
    public Color Color { get; }

    /// <summary>
    ///     Gets or initializes the human-readable name of the type, for example "Boolean" for the key <c>"bool"</c>; defaults to <see cref="Key"/>.
    /// </summary>
    /// <remarks>
    ///     A presentation hint, like <see cref="NodeType.DisplayName"/>: editors show it in port tooltips. A null or empty
    ///     value falls back to <see cref="Key"/>.
    /// </remarks>
    public string DisplayName
    {
        get => string.IsNullOrEmpty(_displayName) ? Key : _displayName;
        init => _displayName = value;
    }
}
