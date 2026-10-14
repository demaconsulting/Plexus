using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <summary>
///     What a loader does with a node whose type is not in the <see cref="NodeTypeRegistry"/>.
/// </summary>
public enum UnknownNodeTypeHandling
{
    /// <summary>Refuse the whole document with a <see cref="SnapshotException"/> that lists every missing type (the default).</summary>
    Fail,

    /// <summary>Leave out the nodes of unknown types, the wires that touch them and their group memberships, and describe what was left out in <see cref="Diagram.LoadReport"/>.</summary>
    Skip,
}

/// <summary>
///     The per-diagram configuration a host installs on every diagram it creates, opens or imports, bundled in one object.
/// </summary>
/// <remarks>
///     <para>
///         Themes, stylers, connection rules and the router are configuration rather than content: they are not
///         saved in a diagram file, so a host has to install them again after every New and Open. Build one
///         <see cref="DiagramOptions"/> for the application and pass it to every place that creates a diagram:
///         <c>new Diagram(types, options)</c>, <see cref="Diagram.FromJson(string, NodeTypeRegistry, DiagramOptions?)"/>,
///         <see cref="Diagram.FromSnapshot"/> and <see cref="Diagram.FromTree"/>; use <see cref="Diagram.Apply"/> for a diagram that
///         already exists. A member left at null means "keep the default" (or, for <see cref="Diagram.Apply"/>, "leave the current value").
///     </para>
///     <para>
///         The record is immutable; derive variants with <c>options with { ... }</c>. The delegates are shared by every diagram
///         created from the options, so a styler that caches data per diagram should key it on the page or diagram it receives
///         (see <see cref="WireStylers.PerPage"/>).
///     </para>
/// </remarks>
/// <example>
///     Define the configuration once and use it for new, opened and imported diagrams.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Scene;
///
///     var options = new DiagramOptions
///     {
///         Theme = DiagramTheme.Classic with { GridSize = 20 },
///         ConnectionRules = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType),
///         WireStyler = WireStylers.ByPortType(),
///         RequireUniqueNodeNames = false,
///     };
///
///     var fresh = new Diagram(types, options);                          // types: your NodeTypeRegistry
///     var opened = Diagram.FromJson(File.ReadAllText("diagram.json"), types, options);
///     fresh.Apply(options);                                             // or re-apply to a diagram that exists
///     </code>
/// </example>
public sealed record DiagramOptions
{
    /// <summary>
    ///     Gets the theme, or null to keep <see cref="DiagramTheme.Classic"/> (or the current theme for <see cref="Diagram.Apply"/>).
    /// </summary>
    public DiagramTheme? Theme { get; init; }

    /// <summary>
    ///     Gets the connection rule, or null to keep <see cref="DemaConsulting.Plexus.ConnectionRules.Default"/> (or the current rule for <see cref="Diagram.Apply"/>).
    /// </summary>
    public ConnectionValidator? ConnectionRules { get; init; }

    /// <summary>
    ///     Gets the wire router, or null to keep the obstacle-avoiding <see cref="OrthogonalRouter"/> (or the current router for <see cref="Diagram.Apply"/>).
    /// </summary>
    public IWireRouter? Router { get; init; }

    /// <summary>
    ///     Gets the wire styler (see <see cref="Diagram.WireStyler"/>), or null to leave wires unstyled (or the current styler for <see cref="Diagram.Apply"/>).
    /// </summary>
    public Func<Wire, WireStyle?>? WireStyler { get; init; }

    /// <summary>
    ///     Gets the node styler (see <see cref="Diagram.NodeStyler"/>), or null to leave nodes unstyled (or the current styler for <see cref="Diagram.Apply"/>).
    /// </summary>
    public Func<Node, NodeStyle?>? NodeStyler { get; init; }

    /// <summary>
    ///     Gets whether node names must be unique, or null to keep the default (true) or the current setting.
    /// </summary>
    public bool? RequireUniqueNodeNames { get; init; }

    /// <summary>
    ///     Gets what loading does with node types the registry does not know; the default is <see cref="UnknownNodeTypeHandling.Fail"/>.
    /// </summary>
    /// <remarks>
    ///     Only loaders (<see cref="Diagram.FromJson(string, NodeTypeRegistry, DiagramOptions?)"/>, <see cref="Diagram.FromSnapshot"/>, <see cref="Diagram.FromTree"/>)
    ///     read this member; <see cref="Diagram.Apply"/> and the constructor ignore it. The clipboard is always strict.
    /// </remarks>
    public UnknownNodeTypeHandling UnknownNodeTypes { get; init; } = UnknownNodeTypeHandling.Fail;
}
