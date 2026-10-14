namespace DemaConsulting.Plexus;

/// <summary>
///     The UI-free Plexus core: the diagram model, node and port types, undo history, connection rules and JSON persistence.
/// </summary>
/// <example>
///     A complete "hello diagram": describe your node types once, build a diagram, connect two nodes, edit, undo and save.
///     The examples in this documentation assume a project with <c>ImplicitUsings</c> enabled (<c>System</c>, <c>System.IO</c>, <c>System.Linq</c>,
///     <c>System.Collections.Generic</c> and <c>System.Threading.Tasks</c> imported) and fragments refer to the <c>types</c>, <c>diagram</c> and <c>page</c> built here.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     // 1. Describe your domain once: port types (with a color) and node types (ports, properties, icon).
///     var types = new NodeTypeRegistry();
///     types.PortTypes.Add(new PortType("number", Color.Parse("#FF8C00")));
///     types.Add(new NodeType("demo.Source")
///     {
///         Title = "SOURCE",
///         Category = "Demo",
///         Icon = IconSource.FromSvg("&lt;svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"&gt;&lt;circle cx=\"12\" cy=\"12\" r=\"8\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2\"/&gt;&lt;/svg&gt;"),
///         Outputs = { new PortDefinition("out", "number") },
///         Properties = { new PropertyDefinition("Value", 1.0) { ShowInSummary = true } },
///     });
///     types.Add(new NodeType("demo.Sink")
///     {
///         Title = "SINK",
///         Category = "Demo",
///         Inputs = { new PortDefinition("in", "number") },
///     });
///
///     // 2. Build a diagram. Every change below is one undoable history entry.
///     var diagram = new Diagram(types);
///     var page = diagram.Pages.Add("Main");
///     var a = page.AddNode("demo.Source", 0, 0, name: "A");
///     var b = page.AddNode("demo.Sink", 180, 0, name: "B");
///     var wire = page.Connect("A.out", "B.in");   // checked by the connection rules and routed automatically
///     a.Properties["Value"] = 5.0;                // validated against the declared property
///
///     // 3. Undo, redo and persist.
///     diagram.History.Undo();                     // Value is 1.0 again
///     diagram.History.Redo();                     // Value is 5.0 again
///     var json = diagram.ToJson();                // portable JSON; the node types themselves are not stored
///     var copy = Diagram.FromJson(json, types);   // the registry must contain every node type the JSON uses
///     </code>
///     <para>
///         The model has three layers. <see cref="NodeTypeRegistry"/> (holding <see cref="NodeType"/>,
///         <see cref="PortType"/>, <see cref="PortDefinition"/> and <see cref="PropertyDefinition"/>) is
///         configuration that your application creates once at start-up. <see cref="Diagram"/> owns a
///         <see cref="PageCollection"/> of <see cref="DiagramPage"/>s; a page owns the <see cref="Node"/>s,
///         <see cref="Wire"/>s, <see cref="Annotation"/>s and <see cref="Group"/>s drawn on it, and a node owns its
///         <see cref="Port"/>s and a <see cref="PropertyBag"/> of values. Everything on a page derives from
///         <see cref="DiagramElement"/>.
///     </para>
///     <para>
///         The easy path is the page API: <see cref="DiagramPage.AddNode(string, double, double, string?, string?)"/>,
///         <see cref="DiagramPage.Connect(string, string, System.Collections.Generic.IReadOnlyList{DemaConsulting.Plexus.Geometry.Point}?, string?)"/>
///         (ports named as <c>"Node.port"</c>), <see cref="DiagramPage.Move"/>, <see cref="Diagram.MoveToPage"/>
///         and <see cref="DiagramPage.Remove(DiagramElement)"/>; <see cref="DiagramPage.CreateGroup"/> frames nodes in a
///         collapsible <see cref="Group"/> (groups nest) and <see cref="DiagramPage.AddAnnotation"/> adds a free text <see cref="Annotation"/>.
///         Every such call is validated, automatically routed and recorded as one entry
///         in <see cref="Diagram.History"/> (an <see cref="UndoHistory"/>), so undo and redo need no extra work; wrap several
///         calls in <see cref="UndoHistory.BeginTransaction"/> to undo them as one step. Port types are strings you define and register
///         with <see cref="NodeTypeRegistry.PortTypes"/> for their color (nothing is predefined), and a property with a list of choices is a string
///         property with <see cref="PropertyDefinition.Choices"/> (there is no separate choice type). Listen to <see cref="Diagram.Changed"/> to
///         react to every change and to <see cref="Diagram.NodeRenaming"/> to correct or veto a rename.
///     </para>
///     <para>
///         Persistence is <see cref="Diagram.ToJson(bool)"/> and <see cref="Diagram.FromJson(string, NodeTypeRegistry, DiagramOptions?)"/> for a whole document, and
///         <see cref="PlexusClipboard"/> for copy and paste of a selection between pages, diagrams and applications.
///         The library does no file or clipboard I/O of its own.
///     </para>
///     <para>
///         The main extension points are a custom <see cref="ConnectionValidator"/> assigned to
///         <see cref="Diagram.ConnectionRules"/> (built from the helpers on the <see cref="ConnectionRules"/> class, which shares its name with the
///         property on purpose), host data on <see cref="Node.Data"/>
///         with an <see cref="INodeDataSerializer"/>, your own commands for <see cref="UndoHistory.Execute(string, System.Action, System.Action)"/>,
///         and the appearance hooks in the <c>DemaConsulting.Plexus.Scene</c> namespace. The model is single-threaded:
///         use it from one thread, normally the UI thread.
///     </para>
/// </example>
internal static class NamespaceDoc
{
}
