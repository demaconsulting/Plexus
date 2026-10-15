namespace DemaConsulting.Plexus;

/// <content>
///     Per-node ports (an optional port provider) and the descriptive text hosts show in tooltips.
/// </content>
public sealed partial class NodeType
{
    /// <summary>
    ///     Gets or initializes a function that computes the ports of each node of this type from its properties or data; null (the default) gives every node the type's <see cref="Inputs"/> and <see cref="Outputs"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use a provider when the ports of a node depend on how it is configured, for example a bus with a <c>Channels</c>
    ///         property that has one input per channel. The provider is called when a node is created (after its default
    ///         payload from <see cref="CreateData"/>), when a node is loaded, pasted or duplicated (after its stored properties
    ///         and data, and before any wire is connected), and after every change of the node's <see cref="Node.Properties"/>
    ///         or <see cref="Node.Data"/>. Ports are never saved: they are always recomputed from the restored properties and data.
    ///     </para>
    ///     <para>
    ///         <b>Port identity is the port name within a direction.</b> When the ports are recomputed, a port whose name (and
    ///         direction) still exists keeps its <see cref="Port"/> object and its wires, even if it moved to another row or its
    ///         label or type changed; a port whose name disappeared is removed together with its wires. Use stable names (for
    ///         example <c>in0</c>, <c>in1</c>, ...): renaming a port drops its wires, and renumbering channels re-attaches the
    ///         wires by name. When a kept port changes its type or its fan-in, each of its wires is checked again with the
    ///         diagram's <see cref="Diagram.ConnectionRules"/> and removed when the rules now refuse it. The wires are checked in
    ///         page order (<see cref="DiagramPage.Wires"/>), each as a reconnect of itself (it does not count against its own
    ///         ports and the rule sees it as <see cref="DiagramPage.WireBeingReconnected"/>), after the wires refused before it
    ///         were removed; so when an input that accepted several wires now accepts one, every earlier wire is refused and the
    ///         wire latest in page order survives.
    ///     </para>
    ///     <para>
    ///         The recomputation is part of the same undo entry as the change that triggered it, so one undo restores the
    ///         property (or data), the previous ports (the same objects) and every removed wire exactly. When the number of ports
    ///         changes, the node takes the height its ports need, never less than its type's default height (the sizing a newly
    ///         added node gets), so it grows or shrinks while keeping its position and width, and the wires whose ends moved are
    ///         re-routed. A recomputation that keeps the number of inputs and outputs leaves the size alone; loading, pasting and
    ///         duplicating keep stored sizes.
    ///         Removed wires are reported through <see cref="Diagram.Changed"/> (an <see cref="DiagramChangeKind.ElementRemoved"/>
    ///         per wire) before the node reports <c>Ports</c> as changed; the <c>Ports</c> change is reported once per change,
    ///         undo and redo, when the node's ports, bounds and wires are all settled.
    ///     </para>
    ///     <para>
    ///         The provider must be a fast, deterministic, side-effect-free function of the node's <see cref="Node.Type"/>,
    ///         <see cref="Node.Properties"/> and <see cref="Node.Data"/>: it may be called for a node that is not on a page yet, and
    ///         redo replays the recorded result instead of calling it again. It must not return null: a null result is a
    ///         provider failure (an <see cref="InvalidOperationException"/> naming the node type). An empty <see cref="PortSet"/>
    ///         (no inputs, no outputs, or neither) is valid: the node then has no ports in that direction, the wires of the
    ///         removed ports are removed, and the height rule above applies with no rows (never below the type's default height).
    ///     </para>
    ///     <para>
    ///         A provider failure - an exception from the provider, a null result, an invalid <see cref="PortSet"/>, or a
    ///         connection rule that throws while the wires are re-checked - never leaves a partial change: after a property or
    ///         data change every applied step (including the change itself) is rolled back, nothing is recorded and the
    ///         exception propagates; when a node is added nothing is added and the exception propagates; when a document is
    ///         loaded a <see cref="SnapshotException"/> is thrown; a paste returns a failed <see cref="PasteResult"/> with the
    ///         message; and a duplicate throws an <see cref="InvalidOperationException"/>. In every case the target is unchanged.
    ///     </para>
    ///     <para>
    ///         The static <see cref="Inputs"/> and <see cref="Outputs"/> remain the type's declared defaults and should equal what
    ///         the provider returns for a new node: palettes, the drag preview size and
    ///         <see cref="Scene.NodeGeometry.RoutingObstacles(Rect, NodeType, Scene.DiagramTheme)"/> use them.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     A bus whose <c>Channels</c> property decides how many inputs it has.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var types = new NodeTypeRegistry();
    ///     types.Add(new NodeType("demo.Bus")
    ///     {
    ///         Inputs = { new PortDefinition("in0", "number"), new PortDefinition("in1", "number") },   // the defaults (Channels = 2)
    ///         Outputs = { new PortDefinition("out", "number") },
    ///         Properties = { new PropertyDefinition("Channels", 2) },
    ///         PortProvider = node =&gt; new PortSet(
    ///             Enumerable.Range(0, Math.Clamp(node.Properties.Get&lt;int&gt;("Channels"), 1, 16))
    ///                 .Select(i =&gt; new PortDefinition($"in{i}", "number") { Description = $"Channel {i}" }),
    ///             [new PortDefinition("out", "number")]),
    ///     });
    ///
    ///     var page = new Diagram(types).Pages.Add("Main");
    ///     var bus = page.AddNode("demo.Bus", 0, 0);
    ///     bus.Properties["Channels"] = 4;                       // one undo entry: the property and the new inputs in2, in3
    ///     Console.WriteLine(bus.Inputs.Count);                  // 4
    ///     bus.Properties["Channels"] = 1;                       // in1..in3 and their wires are removed; in0 keeps its wires
    ///     page.Diagram.History.Undo();                          // back to four inputs, wires included
    ///     </code>
    /// </example>
    public Func<Node, PortSet>? PortProvider { get; init; }

    /// <summary>
    ///     Gets or initializes an optional description of what nodes of this type do; null (the default) means none.
    /// </summary>
    /// <remarks>
    ///     A presentation hint, like <see cref="DisplayName"/>: the library never draws it on a node. Editors show it as the
    ///     node's tooltip when <see cref="ToolTip"/> is not set (see <see cref="GetToolTip"/>); palettes may show it too.
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    ///     Gets or initializes a function producing the tooltip text of a node; null (the default) uses <see cref="Description"/>.
    /// </summary>
    /// <remarks>
    ///     The function is called with the node each time an editor needs the tooltip (when the pointer moves onto the node),
    ///     so it can show live state, such as a computed value or the host's validation messages for the node. Its result is
    ///     used unchanged: return null or an empty string for no tooltip (<see cref="Description"/> is then not used). Keep it
    ///     fast and free of side effects; line breaks (<c>\n</c>) are allowed. An exception it throws is not caught by the
    ///     library: it propagates to the caller of <see cref="GetToolTip"/>.
    /// </remarks>
    /// <example>
    ///     Show the current value and the host's validation messages.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var gauge = new NodeType("demo.Gauge")
    ///     {
    ///         Description = "Shows a value.",
    ///         Properties = { new PropertyDefinition("Value", 0.0) },
    ///         ToolTip = node =&gt;
    ///         {
    ///             var text = $"Value = {node.Properties.Get&lt;double&gt;("Value")}";
    ///             var problems = validator.Check(node);         // validator: your application's checks
    ///             return problems.Count == 0 ? text : text + "\n" + string.Join("\n", problems);
    ///         },
    ///     };
    ///     </code>
    /// </example>
    public Func<Node, string?>? ToolTip { get; init; }

    /// <summary>
    ///     Produces the tooltip text of a node: the result of <see cref="ToolTip"/> when set, otherwise <see cref="Description"/>.
    /// </summary>
    /// <remarks>
    ///     Used by editors to pre-fill a node's tooltip before the host can override it. When <see cref="ToolTip"/> is set its
    ///     result is returned unchanged, including null or an empty string (meaning no tooltip; the description is not used as
    ///     a fallback); when it is not set the <see cref="Description"/> is returned. The function is called with the node on
    ///     every call, so the text follows the node's current properties and data. Calls the host function, so it has
    ///     whatever side effects that function has (none, if it follows the contract on <see cref="ToolTip"/>), and an
    ///     exception thrown by that function propagates unchanged.
    /// </remarks>
    /// <param name="node">The node to describe.</param>
    /// <returns>The text, or null when there is none (the function returned null, or the type has neither a tooltip function nor a description).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public string? GetToolTip(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ToolTip is { } function ? function(node) : Description;
    }

    /// <summary>
    ///     Calls <see cref="PortProvider"/> for a node and checks its result.
    /// </summary>
    /// <remarks>
    ///     Only called when a provider is declared; the caller keeps the static ports otherwise.
    /// </remarks>
    /// <param name="node">The node whose ports are wanted.</param>
    /// <returns>The node's ports.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no provider is declared or the provider returned null.</exception>
    internal PortSet ResolvePorts(Node node)
    {
        var provider = PortProvider
            ?? throw new InvalidOperationException($"Node type '{Key}' declares no port provider.");
        return provider(node)
            ?? throw new InvalidOperationException($"The port provider of node type '{Key}' returned no ports for node '{node.Name}'.");
    }
}
