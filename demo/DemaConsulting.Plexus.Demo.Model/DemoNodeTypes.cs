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
        // Port types give wires and triangles their color
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType(Number, Color.Parse("#FF8C00")));
        types.PortTypes.Add(new PortType(Bool, Color.Parse("#2EBF5B")));
        types.PortTypes.Add(new PortType(Fluid, Color.Parse("#1E90FF")));
        types.PortTypes.Add(new PortType(Audio, Color.Parse("#B05CFF")));
        types.PortTypes.Add(new PortType(Flow, Color.Parse("#F2D13D")));

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
        Size? size = null)
    {
        var type = new NodeType(key)
        {
            Title = title,
            DisplayName = displayName,
            Category = category,
            Icon = Icon(iconName),
            DefaultSize = size ?? NodeSize,
            Visual = visual,

            // Registering the exact payload type lets snapshots and the clipboard carry node.Data
            DataType = dataType,
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
    /// <returns>The property definition.</returns>
    private static PropertyDefinition Summarized(string name, object value, string? unit = null, string? category = null, string? description = null, string? editorHint = null) =>
        new(name, value) { Unit = unit, ShowInSummary = true, Category = category, Description = description, EditorHint = editorHint };

    /// <summary>
    ///     Creates a choice property that is shown in the node summary.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The default choice, one of <paramref name="choices"/>.</param>
    /// <param name="choices">The allowed values.</param>
    /// <param name="category">The category a properties editor groups the property under, or null.</param>
    /// <param name="description">The help text of the property, or null.</param>
    /// <returns>The property definition.</returns>
    private static PropertyDefinition Choice(string name, string value, IReadOnlyList<string> choices, string? category = null, string? description = null) =>
        new(name, value) { Choices = choices, ShowInSummary = true, Category = category, Description = description };
}
