using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <summary>
///     The root of a Plexus document: pages of nodes, wires, annotations and groups sharing one undo history.
/// </summary>
/// <remarks>
///     Create a diagram from a <see cref="NodeTypeRegistry"/>, add pages and build content through
///     the page API; every public mutation is automatically recorded in <see cref="History"/>.
///     Settings such as <see cref="Theme"/>, <see cref="Router"/>, <see cref="ConnectionRules"/> and
///     <see cref="RequireUniqueNodeNames"/> are configuration rather than document content: they
///     are not undoable and raise <see cref="Changed"/> with <see cref="DiagramChangeKind.SettingsChanged"/>. Bundle them in a
///     <see cref="DiagramOptions"/> and pass it to <see cref="Diagram(NodeTypeRegistry, DiagramOptions?)"/> and to every loader, so a
///     diagram that is new, opened or imported is configured the same way (<see cref="Apply"/> does it for an existing diagram).
///     <para>
///         The model is single-threaded: it is designed for the UI thread and is NOT thread-safe.
///         All calls, including reads, must come from the thread that owns the diagram.
///     </para>
/// </remarks>
/// <example>
///     Create a diagram, configure it, listen for changes and save it.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var options = new DiagramOptions { RequireUniqueNodeNames = true };   // configuration: not undoable, not saved; share it between all your diagrams
///     var diagram = new Diagram(types, options);        // types: your NodeTypeRegistry (see the DemaConsulting.Plexus namespace example)
///     diagram.Changed += (_, e) =&gt; Console.WriteLine($"{e.Kind} on page {e.Page?.Name}");
///
///     var main = diagram.Pages.Add("Main");             // pages are added through the collection
///     var node = main.AddNode("demo.Source", 30, 30);   // content is added through the page
///
///     diagram.History.Undo();                           // the node is gone again
///     string json = diagram.ToJson();                   // whole-document persistence
///     var loaded = Diagram.FromJson(json, types, options);   // restores pages, nodes, wires, groups and annotations; options are configuration, not content
///     </code>
/// </example>
public sealed partial class Diagram
{
    /// <summary>
    ///     Finds elements by id; contains only elements currently attached to a page of this diagram.
    /// </summary>
    private readonly Dictionary<string, DiagramElement> _elementIndex = new(StringComparer.Ordinal);

    /// <summary>
    ///     Current theme.
    /// </summary>
    private DiagramTheme _theme = DiagramTheme.Classic;

    /// <summary>
    ///     Current wire router.
    /// </summary>
    private IWireRouter _router = new OrthogonalRouter();

    /// <summary>
    ///     Current host connection rule.
    /// </summary>
    private ConnectionValidator _connectionRules = global::DemaConsulting.Plexus.ConnectionRules.Default;

    /// <summary>
    ///     Whether node names must be unique across the diagram.
    /// </summary>
    private bool _requireUniqueNodeNames = true;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Diagram"/> class with no pages.
    /// </summary>
    /// <param name="types">The node types (and port types) the diagram can instantiate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="types"/> is null.</exception>
    public Diagram(NodeTypeRegistry types)
        : this(types, null)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Diagram"/> class with no pages and the given configuration.
    /// </summary>
    /// <remarks>
    ///     The configuration is installed before the diagram is handed out, so no <see cref="Changed"/> notification is raised for it.
    /// </remarks>
    /// <param name="types">The node types (and port types) the diagram can instantiate.</param>
    /// <param name="options">The theme, router, rules and stylers to install; null keeps every default. See <see cref="DiagramOptions"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="types"/> is null.</exception>
    public Diagram(NodeTypeRegistry types, DiagramOptions? options)
    {
        ArgumentNullException.ThrowIfNull(types);
        Types = types;
        History = new UndoHistory();
        Pages = new PageCollection(this);

        // Straight to the fields: nobody can be listening yet, and a constructor must not raise events
        if (options is not null)
        {
            _theme = options.Theme ?? _theme;
            _router = options.Router ?? _router;
            _connectionRules = options.ConnectionRules ?? _connectionRules;
            _requireUniqueNodeNames = options.RequireUniqueNodeNames ?? _requireUniqueNodeNames;
            _wireStyler = options.WireStyler;
            _nodeStyler = options.NodeStyler;
        }
    }

    /// <summary>
    ///     Occurs for every change applied to the diagram, including undo and redo.
    /// </summary>
    /// <remarks>
    ///     <see cref="DiagramChangedEventArgs.Kind"/> says what happened (an element added, removed or changed, a page added,
    ///     removed or renamed, or a setting such as the theme changed) and the other members name the page and element involved.
    ///     Handlers run synchronously on the thread that made the change; keep them short and do not edit the diagram from them.
    /// </remarks>
    public event EventHandler<DiagramChangedEventArgs>? Changed;

    /// <summary>
    ///     Occurs before a node is renamed through the <see cref="Node.Name"/> setter, <see cref="Rename"/> or <see cref="TryRename"/>, so a handler can correct or veto the name.
    /// </summary>
    /// <remarks>
    ///     The handler runs after the built-in checks (non-empty, unique when <see cref="RequireUniqueNodeNames"/> is set)
    ///     and before anything changes. In the handler set <see cref="NodeRenamingEventArgs.NewName"/> to correct the
    ///     proposal (it is validated again) or set <see cref="NodeRenamingEventArgs.Cancel"/> and a
    ///     <see cref="NodeRenamingEventArgs.Reason"/> to refuse it. The reason becomes the message of the
    ///     <see cref="ArgumentException"/> thrown by <see cref="Rename"/>, the <c>reason</c> of <see cref="TryRename"/>, and the
    ///     tooltip of the Avalonia editor's inline rename box. The handler does not run for renames caused by undo and redo, or
    ///     for the automatically generated name of <see cref="DiagramPage.AddNode(string, double, double, string?, string?)"/>.
    ///     Use <see cref="NodeRenamed"/> to react after the fact.
    /// </remarks>
    /// <example>
    ///     Refuse names with spaces and normalize everything else to upper case.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
    ///     var page = diagram.Pages.Add("Main");
    ///     var node = page.AddNode("demo.Source", 0, 0, name: "A");
    ///
    ///     diagram.NodeRenaming += (_, e) =&gt;
    ///     {
    ///         if (e.ProposedName.Contains(' '))
    ///         {
    ///             e.Cancel = true;                             // veto ...
    ///             e.Reason = "Names cannot contain spaces.";   // ... with a message the user can read
    ///         }
    ///         else
    ///         {
    ///             e.NewName = e.ProposedName.ToUpperInvariant();   // or correct the name
    ///         }
    ///     };
    ///
    ///     bool ok = diagram.TryRename(node, "my node", out var reason);   // false, reason == "Names cannot contain spaces."
    ///     node.Name = "sensor";                                           // renamed to "SENSOR"
    ///     // node.Name = "my node";                                       // would throw ArgumentException with the reason
    ///     </code>
    /// </example>
    public event EventHandler<NodeRenamingEventArgs>? NodeRenaming;

    /// <summary>
    ///     Occurs after a node's name changed, including changes caused by undo and redo.
    /// </summary>
    public event EventHandler<NodeRenamedEventArgs>? NodeRenamed;

    /// <summary>
    ///     Gets a number that increases every time the diagram changes: a cheap cache key for anything computed from the diagram.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The version increments with every <see cref="Changed"/> notification, so it covers edits made
    ///         through the API, undo and redo, settings such as <see cref="Theme"/> or <see cref="WireStyler"/>,
    ///         and <see cref="RefreshAppearance"/>. Two reads that return the same value saw the same
    ///         diagram; the number itself has no other meaning and starts at zero for a new diagram.
    ///     </para>
    ///     <para>
    ///         Hosts use it to cache a derived value (for example a per-page simulation) without
    ///         subscribing to <see cref="Changed"/> and worrying about subscription order:
    ///         remember the version next to the value and recompute when it differs.
    ///         <see cref="WireStylers.PerPage"/> does exactly that for wire stylers.
    ///     </para>
    /// </remarks>
    public long Version { get; private set; }

    /// <summary>
    ///     Gets the node types the diagram can instantiate.
    /// </summary>
    public NodeTypeRegistry Types { get; }

    /// <summary>
    ///     Gets the pages of the diagram in display order.
    /// </summary>
    public PageCollection Pages { get; }

    /// <summary>
    ///     Gets the single undo history shared by all pages.
    /// </summary>
    public UndoHistory History { get; }

    /// <summary>
    ///     Gets or sets the theme that supplies every metric, color and font for routing, scene building and painting (default <see cref="DiagramTheme.Classic"/>).
    /// </summary>
    /// <remarks>
    ///     <see cref="DiagramTheme"/> is an immutable record: derive a variant with <c>DiagramTheme.Classic with { ... }</c>
    ///     and assign it. Replacing the theme is configuration (not undoable, not saved in JSON) and raises
    ///     <see cref="Changed"/> with <see cref="DiagramChangeKind.SettingsChanged"/>. Existing nodes keep their stored
    ///     bounds and existing wires keep their routes, so geometry-related changes (grid, port pitch, stub length) are
    ///     only reflected in new wires; call <see cref="DiagramPage.RerouteAll"/> to re-route the wires of a page. Prefer
    ///     setting the theme before adding content.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    public DiagramTheme Theme
    {
        get => _theme;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _theme = value;
            RaiseChanged(DiagramChangeKind.SettingsChanged, null, null, nameof(Theme));
        }
    }

    /// <summary>
    ///     Gets or sets the router used to lay out new and re-routed wires.
    /// </summary>
    /// <remarks>
    ///     Defaults to the obstacle-avoiding <see cref="OrthogonalRouter"/>; assign a <see cref="SimpleRouter"/>
    ///     for plain three-segment elbows.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    public IWireRouter Router
    {
        get => _router;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _router = value;
            RaiseChanged(DiagramChangeKind.SettingsChanged, null, null, nameof(Router));
        }
    }

    /// <summary>
    ///     Gets or sets the rule applied after Plexus's structural connection checks (default <see cref="DemaConsulting.Plexus.ConnectionRules.Default"/>).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This property and the static class <see cref="DemaConsulting.Plexus.ConnectionRules"/> share a name on purpose: the
    ///         property takes a <see cref="ConnectionValidator"/> that you build from the helpers on the class, as in
    ///         <c>diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType);</c>.
    ///     </para>
    ///     <para>
    ///         The default refuses a second wire into an input unless its <see cref="PortDefinition.AllowsMultipleWires"/>
    ///         is set. A replacement rule decides alone: include <see cref="DemaConsulting.Plexus.ConnectionRules.Default"/>
    ///         via <see cref="DemaConsulting.Plexus.ConnectionRules.All"/> to keep that behavior, or leave it out to allow fan-in everywhere.
    ///         Setting the rule is configuration: not undoable, not saved in JSON, and not applied to wires that already exist.
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    public ConnectionValidator ConnectionRules
    {
        get => _connectionRules;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _connectionRules = value;
            RaiseChanged(DiagramChangeKind.SettingsChanged, null, null, nameof(ConnectionRules));
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether node names must be unique across all pages (default true).
    /// </summary>
    public bool RequireUniqueNodeNames
    {
        get => _requireUniqueNodeNames;
        set
        {
            _requireUniqueNodeNames = value;
            RaiseChanged(DiagramChangeKind.SettingsChanged, null, null, nameof(RequireUniqueNodeNames));
        }
    }

    /// <summary>
    ///     Gets what the load that created this diagram left out; <see cref="DemaConsulting.Plexus.LoadReport.Empty"/> for diagrams that were not loaded or loaded completely.
    /// </summary>
    /// <remarks>
    ///     Only <see cref="UnknownNodeTypeHandling.Skip"/> loads can skip content; see <see cref="DiagramOptions.UnknownNodeTypes"/>.
    /// </remarks>
    public LoadReport LoadReport { get; private set; } = LoadReport.Empty;

    /// <summary>
    ///     Installs a configuration on this diagram: every member of <paramref name="options"/> that is not null replaces the current setting.
    /// </summary>
    /// <remarks>
    ///     Equivalent to assigning <see cref="Theme"/>, <see cref="Router"/>, <see cref="ConnectionRules"/>, <see cref="WireStyler"/>,
    ///     <see cref="NodeStyler"/> and <see cref="RequireUniqueNodeNames"/> one by one (each assignment raises <see cref="Changed"/>
    ///     with <see cref="DiagramChangeKind.SettingsChanged"/>). Like them it is not undoable. <see cref="DiagramOptions.UnknownNodeTypes"/> is ignored.
    ///     To remove a styler, assign null to the property directly.
    /// </remarks>
    /// <param name="options">The configuration to install.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public void Apply(DiagramOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Theme is { } theme)
        {
            Theme = theme;
        }

        if (options.Router is { } router)
        {
            Router = router;
        }

        if (options.ConnectionRules is { } rules)
        {
            ConnectionRules = rules;
        }

        if (options.RequireUniqueNodeNames is { } unique)
        {
            RequireUniqueNodeNames = unique;
        }

        if (options.WireStyler is { } wireStyler)
        {
            WireStyler = wireStyler;
        }

        if (options.NodeStyler is { } nodeStyler)
        {
            NodeStyler = nodeStyler;
        }
    }

    /// <summary>
    ///     Finds an element by id on any page.
    /// </summary>
    /// <param name="id">The element id.</param>
    /// <returns>The element, or null when no attached element has that id.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is null.</exception>
    public DiagramElement? FindElement(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _elementIndex.GetValueOrDefault(id);
    }

    /// <summary>
    ///     Finds a node by name on any page.
    /// </summary>
    /// <param name="name">The exact (case-sensitive) node name.</param>
    /// <returns>The first matching node in page order, or null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public Node? FindNode(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return AllNodes().FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Finds a port on any page from a <c>"Node.port"</c> path.
    /// </summary>
    /// <remarks>
    ///     See <see cref="DiagramPage.FindPort"/> for the path rules; pages are searched in order and the first match wins.
    /// </remarks>
    /// <param name="path">The path, for example <c>"Mixer1.sum"</c>; the node part is a name or an element id.</param>
    /// <param name="direction">Optional; defaults to null, which accepts either direction when the port name is unambiguous. May be null.</param>
    /// <returns>The port, or null when no node or port matches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no direction was given and an input and an output of the node share the port name.</exception>
    public Port? FindPort(string path, PortDirection? direction = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Pages.Select(page => page.FindPort(path, direction)).FirstOrDefault(port => port is not null);
    }

    /// <summary>
    ///     Enumerates every node of every page in page order.
    /// </summary>
    /// <returns>The nodes.</returns>
    internal IEnumerable<Node> AllNodes() => Pages.SelectMany(p => p.Nodes);

    /// <summary>
    ///     Makes an element findable by id.
    /// </summary>
    /// <remarks>
    ///     Uses indexer assignment rather than Add so that restoring state during undo can never throw.
    /// </remarks>
    /// <param name="element">The element that became attached.</param>
    internal void RegisterElement(DiagramElement element) => _elementIndex[element.Id] = element;

    /// <summary>
    ///     Removes an element from the id index.
    /// </summary>
    /// <param name="element">The element that became detached.</param>
    internal void UnregisterElement(DiagramElement element) => _elementIndex.Remove(element.Id);

    /// <summary>
    ///     Raises <see cref="Changed"/> and advances <see cref="Version"/>.
    /// </summary>
    /// <param name="kind">The kind of change.</param>
    /// <param name="page">The affected page, if any.</param>
    /// <param name="element">The affected element, if any.</param>
    /// <param name="propertyName">The changed property, if any.</param>
    internal void RaiseChanged(DiagramChangeKind kind, DiagramPage? page, DiagramElement? element, string? propertyName)
    {
        // The version moves before any handler runs, so a handler that rebuilds a scene already sees the new number
        Version++;
        Changed?.Invoke(this, new DiagramChangedEventArgs(kind, page, element, propertyName));
    }

    /// <summary>
    ///     Runs a multi-step operation and records it as one undo entry.
    /// </summary>
    /// <remarks>
    ///     Steps run immediately through the supplied <see cref="CommandPlan"/>. If any step throws,
    ///     all applied steps are reverted so the exception leaves the model unchanged.
    /// </remarks>
    /// <param name="description">Description of the combined entry.</param>
    /// <param name="plan">Applies the steps.</param>
    internal void Perform(string description, Action<CommandPlan> plan)
    {
        History.EnsureNotApplying();
        var commands = new CommandPlan();
        try
        {
            plan(commands);
        }
        catch
        {
            commands.Abort();
            throw;
        }

        if (!commands.IsEmpty)
        {
            History.Record(commands.Build(description));
        }
    }

    /// <summary>
    ///     Generates an unused node name of the form <c>{TypeKeyLastSegment}{n}</c>.
    /// </summary>
    /// <param name="type">The node type whose key supplies the base name.</param>
    /// <returns>The lowest unused numbered name.</returns>
    internal string GenerateNodeName(NodeType type)
    {
        // Build the used-name set once so the search is linear in the number of nodes
        var used = new HashSet<string>(AllNodes().Select(n => n.Name), StringComparer.Ordinal);
        var baseName = type.ShortName;
        var number = 1;
        string candidate;
        do
        {
            candidate = baseName + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            number++;
        }
        while (used.Contains(candidate));

        return candidate;
    }

    /// <summary>
    ///     Throws when an id is already used by an attached element.
    /// </summary>
    /// <param name="id">The candidate id.</param>
    /// <exception cref="ArgumentException">Thrown when the id is empty or already in use.</exception>
    internal void EnsureIdAvailable(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (_elementIndex.ContainsKey(id))
        {
            throw new ArgumentException($"An element with id '{id}' already exists in the diagram.", nameof(id));
        }
    }

    /// <summary>
    ///     Raises <see cref="NodeRenaming"/>.
    /// </summary>
    /// <param name="args">The event data.</param>
    private void OnNodeRenaming(NodeRenamingEventArgs args) => NodeRenaming?.Invoke(this, args);

    /// <summary>
    ///     Raises <see cref="NodeRenamed"/>.
    /// </summary>
    /// <param name="args">The event data.</param>
    private void OnNodeRenamed(NodeRenamedEventArgs args) => NodeRenamed?.Invoke(this, args);
}
