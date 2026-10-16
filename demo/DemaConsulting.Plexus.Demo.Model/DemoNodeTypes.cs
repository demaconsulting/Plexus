using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     Defines the demo's port types and, in the per-domain partial files, its node types.
/// </summary>
/// <remarks>
///     The demo shows what a host supplies: a registry with port types (number, bool, fluid, audio
///     and flow, each with its own color) and node types with a title, an embedded SVG icon,
///     ports, properties and (optionally) a host data payload or a custom visual. Everything else
///     - layout, routing, undo, rendering, persistence - comes from the library. Five small
///     domains are provided so a diagram can mix them freely: math dataflow, plumbing, logic gates,
///     an audio signal chain and a business workflow.
/// </remarks>
public static partial class DemoNodeTypes
{
    /// <summary>
    ///     The key of the numeric signal port type (orange).
    /// </summary>
    public const string Number = "number";

    /// <summary>
    ///     The key of the boolean signal port type (green).
    /// </summary>
    public const string Bool = "bool";

    /// <summary>
    ///     The key of the fluid port type (blue); which fluid flows is a property of the nodes.
    /// </summary>
    public const string Fluid = "fluid";

    /// <summary>
    ///     The key of the audio signal port type (violet).
    /// </summary>
    public const string Audio = "audio";

    /// <summary>
    ///     The key of the control-flow port type (yellow) used by workflows.
    /// </summary>
    public const string Flow = "flow";

    /// <summary>
    ///     The category of the math / dataflow node types.
    /// </summary>
    public const string MathCategory = "Math";

    /// <summary>
    ///     The category of the plumbing node types.
    /// </summary>
    public const string PlumbingCategory = "Plumbing";

    /// <summary>
    ///     The category of the logic gate node types.
    /// </summary>
    public const string LogicCategory = "Logic";

    /// <summary>
    ///     The category of the audio synth node types.
    /// </summary>
    public const string AudioCategory = "Audio";

    /// <summary>
    ///     The category of the business workflow node types.
    /// </summary>
    public const string WorkflowCategory = "Workflow";

    /// <summary>
    ///     The fluids a pump or tank can carry, the choices of their <c>Fluid</c> property.
    /// </summary>
    /// <remarks>
    ///     Wires carry the <see cref="Fluid"/> port type only; a host that wants wires colored by
    ///     what flows through them reads the <c>Fluid</c> property of the wire's source node.
    /// </remarks>
    public static readonly IReadOnlyList<string> FluidChoices = ["Water", "Oil", "Steam"];

    /// <summary>
    ///     The keys of the node types in the "basic" type set (see <see cref="CreateBasicRegistry"/>): the math types.
    /// </summary>
    public static readonly IReadOnlySet<string> BasicTypeKeys = new HashSet<string>
    {
        Constant, Add, Multiply, Sine, Clamp, Scope,
    };

    /// <summary>
    ///     The size shared by the standard demo nodes, in diagram units (a whole number of grid cells).
    /// </summary>
    private static readonly Size NodeSize = new(90, 105);

    /// <summary>
    ///     Creates a registry holding the demo port types and node types.
    /// </summary>
    /// <returns>A new registry; each call returns an independent instance.</returns>
    public static NodeTypeRegistry CreateRegistry()
    {
        // Port types give wires and triangles their color, and port tooltips their type name
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType(Number, Color.Parse("#FF8C00")) { DisplayName = "Number" });
        types.PortTypes.Add(new PortType(Bool, Color.Parse("#2EBF5B")) { DisplayName = "Boolean" });
        types.PortTypes.Add(new PortType(Fluid, Color.Parse("#1E90FF")) { DisplayName = "Fluid" });
        types.PortTypes.Add(new PortType(Audio, Color.Parse("#B05CFF")) { DisplayName = "Audio" });
        types.PortTypes.Add(new PortType(Flow, Color.Parse("#F2D13D")) { DisplayName = "Control flow" });

        // Node types follow, in the order a palette would list them
        AddMathTypes(types);
        AddPlumbingTypes(types);
        AddLogicTypes(types);
        AddAudioTypes(types);
        AddWorkflowTypes(types);
        return types;
    }

    /// <summary>
    ///     Creates a registry holding the demo port types and only the "basic" node types (the math types).
    /// </summary>
    /// <remarks>
    ///     A host that knows fewer node types than another instance of itself is how the demo shows
    ///     the clipboard failure path: pasting a diagram fragment that uses a type this registry
    ///     lacks is refused and reports the missing keys.
    /// </remarks>
    /// <returns>A new registry; each call returns an independent instance.</returns>
    public static NodeTypeRegistry CreateBasicRegistry()
    {
        // Start from the full set and drop everything outside the basic subset
        var types = CreateRegistry();
        foreach (var type in types.Where(t => !BasicTypeKeys.Contains(t.Key)).ToList())
        {
            types.Remove(type.Key);
        }

        return types;
    }

    /// <summary>
    ///     Loads an embedded demo icon.
    /// </summary>
    /// <param name="name">The icon's file name without extension.</param>
    /// <returns>The icon source.</returns>
    private static IconSource Icon(string name) =>
        IconSource.FromSvgResource(typeof(DemoNodeTypes).Assembly, $"Icons.{name}.svg");

    /// <summary>
    ///     Creates a node type with the demo's shared size, an embedded icon and a category.
    /// </summary>
    /// <param name="key">The node type key.</param>
    /// <param name="title">The title drawn in the node header.</param>
    /// <param name="displayName">The host-facing name used in palettes and menus.</param>
    /// <param name="category">The palette category.</param>
    /// <param name="iconName">The embedded icon name.</param>
    /// <param name="inputs">The input ports, top to bottom.</param>
    /// <param name="outputs">The output ports, top to bottom. Names only need to be unique within a direction.</param>
    /// <param name="properties">The properties, in the order a properties dialog lists them.</param>
    /// <param name="visual">A custom visual replacing the standard node template, or null.</param>
    /// <param name="dataType">The type of the host data payload, or null when nodes carry none.</param>
    /// <param name="size">The default node size, or null for the demo's shared size.</param>
    /// <param name="createData">Creates the initial host data of every new node, or null when nodes start without data.</param>
    /// <param name="portProvider">Computes each node's ports from its properties, or null when every node has the declared ports.</param>
    /// <param name="description">What the node type does, shown in tooltips and palettes, or null.</param>
    /// <param name="toolTip">Produces a node's live tooltip text, or null to show the description.</param>
    /// <returns>The node type.</returns>
    private static NodeType Define(
        string key,
        string title,
        string displayName,
        string category,
        string iconName,
        PortDefinition[] inputs,
        PortDefinition[] outputs,
        PropertyDefinition[] properties,
        INodeVisual? visual = null,
        Type? dataType = null,
        Size? size = null,
        Func<object>? createData = null,
        Func<Node, PortSet>? portProvider = null,
        string? description = null,
        Func<Node, string?>? toolTip = null)
    {
        var type = new NodeType(key)
        {
            Title = title,
            DisplayName = displayName,
            Category = category,
            Icon = Icon(iconName),
            DefaultSize = size ?? NodeSize,
            Visual = visual,

            // Registering the exact payload type lets snapshots and the clipboard carry node.Data;
            // the factory gives every new node (added, dropped or pasted) its own payload object
            DataType = dataType,
            CreateData = createData,

            // Optional per-node ports and the text the editor shows when the pointer rests on a node
            PortProvider = portProvider,
            Description = description,
            ToolTip = toolTip,
        };

        // Ports and properties are added after construction because the type is immutable once registered
        foreach (var input in inputs)
        {
            type.Inputs.Add(input);
        }

        foreach (var output in outputs)
        {
            type.Outputs.Add(output);
        }

        foreach (var property in properties)
        {
            type.Properties.Add(property);
        }

        return type;
    }

    /// <summary>
    ///     Creates a property that is shown in the node summary.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The default value.</param>
    /// <param name="unit">The unit shown after the value, or null.</param>
    /// <param name="category">The category a properties editor groups the property under, or null.</param>
    /// <param name="description">The help text of the property, or null.</param>
    /// <param name="editorHint">The key of the custom editor a properties editor should use (for example <c>slider</c>), or null.</param>
    /// <param name="order">The position of the property within its category in a properties editor (lower first); 0 keeps alphabetical order.</param>
    /// <returns>The property definition.</returns>
    private static PropertyDefinition Summarized(string name, object value, string? unit = null, string? category = null, string? description = null, string? editorHint = null, int order = 0) =>
        new(name, value) { Unit = unit, ShowInSummary = true, Category = category, Description = description, EditorHint = editorHint, Order = order };

    /// <summary>
    ///     Creates a choice property that is shown in the node summary.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The default choice, one of <paramref name="choices"/>.</param>
    /// <param name="choices">The allowed values.</param>
    /// <param name="category">The category a properties editor groups the property under, or null.</param>
    /// <param name="description">The help text of the property, or null.</param>
    /// <param name="order">The position of the property within its category in a properties editor (lower first); 0 keeps alphabetical order.</param>
    /// <returns>The property definition.</returns>
    private static PropertyDefinition Choice(string name, string value, IReadOnlyList<string> choices, string? category = null, string? description = null, int order = 0) =>
        new(name, value) { Choices = choices, ShowInSummary = true, Category = category, Description = description, Order = order };

    /// <summary>
    ///     Formats a node's current property value with its unit for a tooltip, for example <c>12 L/min</c>.
    /// </summary>
    /// <remarks>
    ///     Uses the definition's own formatting (invariant culture), so a tooltip shows a value the way the node summary does.
    ///     A value missing from the bag (a property declared after the node was stored) shows the default.
    /// </remarks>
    /// <param name="node">The node.</param>
    /// <param name="name">The name of a property its type declares.</param>
    /// <returns>The formatted value, followed by a space and the unit when the property has one.</returns>
    private static string Value(Node node, string name)
    {
        var definition = node.Type.Properties.First(p => p.Name == name);
        var value = node.Properties.TryGetValue(name, out var current) ? current : definition.DefaultValue;
        var text = definition.FormatValue(value);
        return string.IsNullOrEmpty(definition.Unit) ? text : $"{text} {definition.Unit}";
    }

    /// <summary>
    ///     Makes a live tooltip: the type's description on the first line and a summary of the node's current values below it.
    /// </summary>
    /// <param name="description">The node type's description.</param>
    /// <param name="summary">Builds the one-line summary of a node's current values.</param>
    /// <returns>The tooltip function for <see cref="NodeType.ToolTip"/>.</returns>
    private static Func<Node, string?> Live(string description, Func<Node, string> summary) =>
        node => $"{description}\n{summary(node)}";
}
