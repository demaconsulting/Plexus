namespace DemaConsulting.Plexus;

/// <content>
///     Port lookup by path: <c>"NodeName.port"</c> (or <c>"NodeId.port"</c>) instead of navigating node and port objects.
/// </content>
public sealed partial class DiagramPage
{
    /// <summary>
    ///     Finds a port on this page from a <c>"Node.port"</c> path.
    /// </summary>
    /// <remarks>
    ///     The node part is a node name or, failing that, an element id. Node names and port names may
    ///     themselves contain dots: the path is split at every dot, starting from the last one, and the first
    ///     split that names an existing node and port wins. When <paramref name="direction"/> is null the
    ///     port name must be unambiguous on the node (an input and an output of the same name need a direction).
    /// </remarks>
    /// <param name="path">The path, for example <c>"Mixer1.sum"</c>.</param>
    /// <param name="direction">The direction to accept; null accepts either direction when the port name is unambiguous.</param>
    /// <returns>The port, or null when no node or port matches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no direction was given and an input and an output of the node share the port name.</exception>
    public Port? FindPort(string path, PortDirection? direction = null)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Names may contain dots, so try the split points from the right (the common case) to the left
        for (var dot = path.LastIndexOf('.'); dot > 0; dot = path.LastIndexOf('.', dot - 1))
        {
            var node = FindNodeByNameOrId(path[..dot]);
            if (node is null)
            {
                continue;
            }

            var port = node.LookupPort(path[(dot + 1)..], direction, out var ambiguous);
            if (ambiguous)
            {
                throw new InvalidOperationException(
                    $"Port path '{path}' is ambiguous: node '{node.Name}' has both an input and an output named '{path[(dot + 1)..]}'; pass a PortDirection.");
            }

            if (port is not null)
            {
                return port;
            }
        }

        return null;
    }

    /// <summary>
    ///     Connects two ports given as <c>"Node.port"</c> paths (undoable).
    /// </summary>
    /// <remarks>
    ///     <paramref name="from"/> prefers an output and <paramref name="to"/> prefers an input, so a node
    ///     whose input and output share a name (for example <c>value</c>) can be written naturally on both
    ///     ends: <c>page.Connect("Filter1.value", "Scope1.value")</c>. Otherwise this behaves like
    ///     <see cref="Connect(Port, Port, IReadOnlyList{Point}?, string?)"/>, including the automatic
    ///     ordering, validation (the default rules are summarized on <see cref="CanConnect"/>: direction, same page, different nodes, no duplicate,
    ///     one wire per input unless it allows fan-in; port types are compared only with <see cref="ConnectionRules.SameType"/>) and routing rules.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var diagram = new Diagram(types);                      // types: the registry of the DemaConsulting.Plexus example
    ///     var page = diagram.Pages.Add("Main");
    ///     page.AddNode("demo.Source", 0, 120, name: "C");
    ///     page.AddNode("demo.Sink", 180, 120, name: "D");
    ///     var wire = page.Connect("C.out", "D.in");              // "NodeName.portName" on both ends
    ///
    ///     // A failed connection throws InvalidOperationException with the reason; use CanConnect to ask first
    ///     try
    ///     {
    ///         page.Connect("C.out", "D.in");                     // duplicate wire
    ///     }
    ///     catch (InvalidOperationException ex)
    ///     {
    ///         Console.WriteLine(ex.Message);
    ///     }
    ///     </code>
    /// </example>
    /// <param name="from">The source path; an output is preferred.</param>
    /// <param name="to">The target path; an input is preferred.</param>
    /// <param name="route">The interior bend points; null routes automatically.</param>
    /// <param name="id">The element id; null generates one.</param>
    /// <returns>The new wire.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a path is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a path does not name a port on this page, or the id is in use.</exception>
    /// <exception cref="InvalidOperationException">Thrown, with the reason, when the connection is not allowed or the page is removed.</exception>
    public Wire Connect(string from, string to, IReadOnlyList<Point>? route = null, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        var source = FindPort(from, PortDirection.Output) ?? FindPort(from)
            ?? throw new ArgumentException($"'{from}' does not name a port on page '{Name}'.", nameof(from));
        var target = FindPort(to, PortDirection.Input) ?? FindPort(to)
            ?? throw new ArgumentException($"'{to}' does not name a port on page '{Name}'.", nameof(to));
        return Connect(source, target, route, id);
    }

    /// <summary>
    ///     Finds a node of this page by name, falling back to its element id.
    /// </summary>
    /// <param name="nameOrId">The node name or id.</param>
    /// <returns>The node, or null.</returns>
    private Node? FindNodeByNameOrId(string nameOrId) =>
        _nodes.FirstOrDefault(n => string.Equals(n.Name, nameOrId, StringComparison.Ordinal))
        ?? _nodes.FirstOrDefault(n => string.Equals(n.Id, nameOrId, StringComparison.Ordinal));
}
