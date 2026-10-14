using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <summary>
///     What a loader does with a node whose type is not in the <see cref="NodeTypeRegistry"/>.
/// </summary>
/// <remarks>
///     A file written by a newer version of the application, or by one with plug-ins, can mention node types this
///     application has not registered. Choose <see cref="Fail"/> to refuse such a file, or <see cref="Skip"/> to open what is
///     understood and tell the user what was left out (<see cref="Diagram.LoadReport"/>). To test either path, save a diagram
///     and change a node's <c>TypeKey</c> (the <c>typeKey</c> member of each node in the JSON) to a key the registry lacks;
///     the example shows how to do that on the snapshot without editing text.
/// </remarks>
/// <example>
///     Simulate a file from a newer version by renaming a node's type key, then load it both ways.
///     <code>
///     using DemaConsulting.Plexus;
///
///     // types: your NodeTypeRegistry, which contains the key "demo.Source"
///     var diagram = new Diagram(types);
///     diagram.Pages.Add("Main").AddNode("demo.Source", 0, 0, name: "A");
///
///     DiagramSnapshot snapshot = diagram.ToSnapshot();
///     PageSnapshot page = snapshot.Pages[0];
///     DiagramSnapshot future = snapshot with
///     {
///         Pages = [page with { Nodes = [.. page.Nodes.Select(n => n with { TypeKey = "demo.FromTheFuture" })] }],
///     };
///
///     var skip = new DiagramOptions { UnknownNodeTypes = UnknownNodeTypeHandling.Skip };
///     Diagram opened = Diagram.FromSnapshot(future, types, skip);
///     Console.WriteLine(opened.LoadReport.Message);                // Skipped 1 node(s) of unknown type(s) demo.FromTheFuture, ...
///
///     try { Diagram.FromSnapshot(future, types); }                 // the default is Fail
///     catch (SnapshotException ex) { Console.WriteLine(ex.Message); }
///     </code>
/// </example>
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
    ///     Initializes a new instance of the <see cref="DiagramOptions"/> record with every member unset, so a diagram keeps all defaults.
    /// </summary>
    public DiagramOptions()
    {
        // Nothing to initialize: every member has a usable default
    }
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

    /// <summary>
    ///     Gets a callback invoked with every diagram the options create or configure, so per-instance setup (event handlers) lives in one place; null (the default) means none.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Event handlers such as <see cref="Diagram.NodeRenaming"/> belong to one diagram instance and are not part of a saved file, so a host
    ///         that opens or creates diagrams in several places would otherwise have to remember to subscribe after each one.
    ///         The callback runs after the diagram is complete: at the end of <c>new Diagram(types, options)</c>, once
    ///         <see cref="Diagram.FromJson(string, NodeTypeRegistry, DiagramOptions?)"/>, <see cref="Diagram.FromSnapshot"/> or <see cref="Diagram.FromTree"/>
    ///         have loaded the whole document (with an empty undo history), and last in <see cref="Diagram.Apply"/>.
    ///     </para>
    ///     <para>
    ///         <see cref="Diagram.Apply"/> runs it on every call, so only use <see cref="Diagram.Apply"/> with options that carry an
    ///         initializer on a diagram that has not been initialized before, otherwise handlers are attached twice.
    ///         Exceptions thrown by the callback propagate to the caller of the entry point.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     Install one rename rule on every diagram of the application.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var options = new DiagramOptions
    ///     {
    ///         Initialize = diagram =&gt; diagram.NodeRenaming += (_, e) =&gt;
    ///         {
    ///             if (e.ProposedName.Contains(' '))
    ///             {
    ///                 e.Cancel = true;
    ///                 e.Reason = "Names cannot contain spaces.";
    ///             }
    ///         },
    ///     };
    ///
    ///     var fresh = new Diagram(types, options);                                      // rule installed
    ///     var opened = Diagram.FromJson(File.ReadAllText("diagram.json"), types, options);   // rule installed here too
    ///     </code>
    /// </example>
    public Action<Diagram>? Initialize { get; init; }
}
