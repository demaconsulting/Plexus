using System.Runtime.CompilerServices;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     The demo's dynamic appearance: wires colored by port type, by the fluid they carry and by the live state of logic
///     signals, lamps that light up, and tank icons tinted by the fluid.
/// </summary>
/// <remarks>
///     <para>
///         This is one <see cref="Diagram.WireStyler"/> and one <see cref="Diagram.NodeStyler"/>, installed with
///         <see cref="Options"/>, so the GUI and the command-line renderer draw the same diagram. The wire styler
///         combines three layers with
///         <see cref="WireStylers.Combine"/>, later layers overriding earlier ones:
///     </para>
///     <list type="number">
///         <item><description>Every wire takes the color of its source port's type (<see cref="WireStylers.ByPortType"/>).</description></item>
///         <item><description>
///             A fluid wire takes the color of the fluid that flows in it: the <c>Fluid</c> property of the
///             pump or tank found by walking upstream from the wire's source through valves, junctions and
///             flow meters. Steam is also dashed.
///         </description></item>
///         <item><description>
///             A boolean wire is bright green and thick while it carries true and dark gray while it
///             carries false, as computed by <see cref="LogicEvaluator"/>.
///         </description></item>
///     </list>
///     <para>
///         The logic values are expensive compared with one wire, so <see cref="WireStylers.PerPage"/>
///         computes them once per page and <see cref="Diagram.Version"/> (a node property such as a switch's
///         <c>On</c> raises <see cref="Diagram.Changed"/>, and so advances the version) and reuses them for
///         every wire of the scene build. The styler runs for every wire of every scene build, so without
///         that a page of N wires would be evaluated N times per repaint.
///     </para>
///     <para>
///         The node styler has two layers. A lamp lights up while its input is true: the fill becomes a warm
///         glow of its <c>Color</c> property (green, red or amber), the border and the icon go bright and a
///         halo surrounds it; with a false input it keeps the default gray look. A tank's icon is tinted by the
///         fluid it holds (water blue, oil brown, steam light gray). The lamp's state comes from the same
///         <see cref="LogicEvaluator"/> result the wires use: both stylers read one per-page snapshot keyed by
///         <see cref="Diagram.Version"/>, so a scene build evaluates the logic once however many wires and
///         lamps it styles, and toggling a switch (which advances the version) updates both at once.
///     </para>
/// </remarks>
public static class DemoAppearance
{
    /// <summary>
    ///     The name of the choice property of pumps and tanks that says which fluid they supply.
    /// </summary>
    private const string FluidProperty = "Fluid";

    /// <summary>
    ///     The color of a logic wire that carries true.
    /// </summary>
    private static readonly Color LogicOn = Color.Parse("#00E83C");

    /// <summary>
    ///     The color of a logic wire that carries false.
    /// </summary>
    private static readonly Color LogicOff = Color.Parse("#3A3A3A");

    /// <summary>
    ///     The name of the choice property of lamps that says which color they glow.
    /// </summary>
    private const string LampColorProperty = "Color";

    /// <summary>
    ///     The logic values of each page at the diagram version they were evaluated for, shared by the wire and node stylers.
    /// </summary>
    private static readonly ConditionalWeakTable<DiagramPage, LogicSnapshot> Snapshots = new();

    /// <summary>
    ///     Gets the demo's configuration: wire and node stylers plus the connection rule; install it on every diagram.
    /// </summary>
    /// <remarks>
    ///     One instance serves the whole application: pass it to <c>new Diagram(types, DemoAppearance.Options)</c> and to
    ///     <c>Diagram.FromJson(json, types, DemoAppearance.Options)</c>, so the GUI and the command-line renderer draw every
    ///     new, loaded or sample diagram the same way. It is sharable because the per-page state lives in the stylers' own
    ///     tables keyed by page, not in the options.
    /// </remarks>
    public static DiagramOptions Options { get; } = new()
    {
        // The logic values are computed once per page and diagram version by PerPage, not once per wire
        WireStyler = WireStylers.Combine(
            WireStylers.ByPortType(),
            FluidStyle,
            WireStylers.PerPage(LogicStyler)),
        NodeStyler = NodeStylers.Combine(
            TankStyle,
            NodeStylers.PerPage(LampStyler)),

        // A wire joins ports of one type only (a number cannot drive an audio input); the editor shows the reason.
        // The default rule stays in the mix, so an input still accepts one wire unless its definition opts into fan-in
        ConnectionRules = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType),
    };

    /// <summary>
    ///     Builds the logic styler of a page: evaluates the page's logic lazily, on the first boolean wire, and styles every wire from the result.
    /// </summary>
    /// <param name="page">The page the styler is for.</param>
    /// <returns>The styler for the wires of <paramref name="page"/>.</returns>
    private static Func<Wire, WireStyle?> LogicStyler(DiagramPage page)
    {
        // A page without boolean wires never pays for an evaluation
        var values = new Lazy<IReadOnlyDictionary<Port, bool>>(() => LogicValues(page));
        return wire => LogicStyle(wire, values);
    }

    /// <summary>
    ///     Builds the lamp styler of a page: evaluates the page's logic lazily, on the first lamp, and lights every lamp whose input is true.
    /// </summary>
    /// <param name="page">The page the styler is for.</param>
    /// <returns>The styler for the nodes of <paramref name="page"/>.</returns>
    private static Func<Node, NodeStyle?> LampStyler(DiagramPage page)
    {
        // A page without lamps never pays for an evaluation
        var values = new Lazy<IReadOnlyDictionary<Port, bool>>(() => LogicValues(page));
        return node => LampStyle(node, values);
    }

    /// <summary>
    ///     Gets the logic values of a page, evaluating it only when the diagram has changed since the last evaluation.
    /// </summary>
    /// <remarks>
    ///     The wire and the node stylers both build their per-page state when the version advances; this
    ///     snapshot makes that one <see cref="LogicEvaluator.Evaluate"/> call instead of two.
    /// </remarks>
    /// <param name="page">The page.</param>
    /// <returns>The logic values at the current version of the diagram.</returns>
    private static IReadOnlyDictionary<Port, bool> LogicValues(DiagramPage page)
    {
        var snapshot = Snapshots.GetValue(page, _ => new LogicSnapshot());
        var version = page.Diagram.Version;
        if (snapshot.Values is null || snapshot.Version != version)
        {
            snapshot.Values = LogicEvaluator.Evaluate(page);
            snapshot.Version = version;
        }

        return snapshot.Values;
    }

    /// <summary>
    ///     Styles a lamp by whether its input is true.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="values">The logic values of the node's page, evaluated on first use.</param>
    /// <returns>The lit style of the lamp's color, or null for a lamp that is off or a node that is not a lamp.</returns>
    private static NodeStyle? LampStyle(Node node, Lazy<IReadOnlyDictionary<Port, bool>> values)
    {
        if (node.Type.Key != DemoNodeTypes.Lamp)
        {
            return null;
        }

        // An unconnected or false input is off, which keeps the default gray look
        if (!values.Value.TryGetValue(node.Input("in"), out var on) || !on)
        {
            return null;
        }

        return node.Properties.Get<string>(LampColorProperty) switch
        {
            "Green" => Lit("#1E8A3C", "#6FE39A", "#00E83C", "#DBFFE6"),
            "Red" => Lit("#B02A2A", "#FF8A8A", "#FF3030", "#FFE3E3"),
            _ => Lit("#B87A0A", "#FFD166", "#FFB000", "#FFF3CC"),
        };
    }

    /// <summary>
    ///     Creates the style of a lit lamp.
    /// </summary>
    /// <param name="fill">The warm body color.</param>
    /// <param name="border">The bright rim color.</param>
    /// <param name="glow">The halo color.</param>
    /// <param name="icon">The near-white tint of the bulb icon.</param>
    /// <returns>The style.</returns>
    private static NodeStyle Lit(string fill, string border, string glow, string icon) => new()
    {
        Fill = Color.Parse(fill),
        Border = Color.Parse(border),
        BorderThickness = 2,
        Glow = Color.Parse(glow),
        IconTint = Color.Parse(icon),
    };

    /// <summary>
    ///     Styles a tank by tinting its icon with the fluid it holds.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The style, or null for a node that is not a tank or holds an unknown fluid.</returns>
    private static NodeStyle? TankStyle(Node node)
    {
        if (node.Type.Key != DemoNodeTypes.Tank)
        {
            return null;
        }

        return node.Properties.Get<string>(FluidProperty) switch
        {
            "Water" => new NodeStyle { IconTint = Color.Parse("#4DA8FF") },
            "Oil" => new NodeStyle { IconTint = Color.Parse("#C2791E") },
            "Steam" => new NodeStyle { IconTint = Color.Parse("#E4E8EC") },
            _ => null,
        };
    }

    /// <summary>
    ///     Gets the fluid a pipe carries, by walking upstream from the wire's source to a pump or tank.
    /// </summary>
    /// <param name="wire">The wire to look at.</param>
    /// <returns>The fluid name (one of <see cref="DemoNodeTypes.FluidChoices"/>), or null when no pump or tank feeds the wire.</returns>
    public static string? FluidOf(Wire wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return wire.Source.TypeKey == DemoNodeTypes.Fluid ? FluidOf(wire.Source.Node, []) : null;
    }

    /// <summary>
    ///     Styles a fluid wire by the fluid it carries.
    /// </summary>
    /// <param name="wire">The wire.</param>
    /// <returns>The style, or null for a wire that is not a pipe or has no known fluid.</returns>
    private static WireStyle? FluidStyle(Wire wire) => FluidOf(wire) switch
    {
        "Water" => new WireStyle { Color = Color.Parse("#1E78E6"), Thickness = 3 },
        "Oil" => new WireStyle { Color = Color.Parse("#A8600F"), Thickness = 3 },
        "Steam" => new WireStyle { Color = Color.Parse("#E4E8EC"), Thickness = 3, Dash = [8, 5] },
        _ => null,
    };

    /// <summary>
    ///     Styles a boolean wire by the live value of the signal it carries.
    /// </summary>
    /// <param name="wire">The wire.</param>
    /// <param name="values">The logic values of the wire's page, evaluated on first use.</param>
    /// <returns>The style, or null for a wire that is not a logic signal.</returns>
    private static WireStyle? LogicStyle(Wire wire, Lazy<IReadOnlyDictionary<Port, bool>> values)
    {
        if (wire.Source.TypeKey != DemoNodeTypes.Bool)
        {
            return null;
        }

        // A bool wire from a node the evaluator does not know keeps its port-type color
        if (!values.Value.TryGetValue(wire.Source, out var on))
        {
            return null;
        }

        return on
            ? new WireStyle { Color = LogicOn, Thickness = 4 }
            : new WireStyle { Color = LogicOff, Thickness = 2 };
    }

    /// <summary>
    ///     Finds the fluid that leaves a node: its own for a pump or tank, otherwise the one that arrives.
    /// </summary>
    /// <param name="node">The node whose output is being looked at.</param>
    /// <param name="visited">The nodes already walked, which cuts loops in a pipe network.</param>
    /// <returns>The fluid name, or null when nothing upstream supplies one.</returns>
    private static string? FluidOf(Node node, HashSet<Node> visited)
    {
        // Pumps and tanks supply the fluid; the walk ends there
        if (node.Type.Key is DemoNodeTypes.Pump or DemoNodeTypes.Tank)
        {
            return node.Properties.Get<string>(FluidProperty);
        }

        // Valves, junctions and meters pass on whatever flows in, so ask whoever feeds them
        if (!visited.Add(node))
        {
            return null;
        }

        return node.Inputs
            .Where(input => input.TypeKey == DemoNodeTypes.Fluid)
            .SelectMany(input => input.Wires)
            .Select(feeder => FluidOf(feeder.Source.Node, visited))
            .FirstOrDefault(fluid => fluid is not null);
    }

    /// <summary>
    ///     The logic values of one page and the diagram version they were evaluated for.
    /// </summary>
    private sealed class LogicSnapshot
    {
        /// <summary>
        ///     Gets or sets the values, or null before the first evaluation.
        /// </summary>
        public IReadOnlyDictionary<Port, bool>? Values { get; set; }

        /// <summary>
        ///     Gets or sets the <see cref="Diagram.Version"/> the values were evaluated at.
        /// </summary>
        public long Version { get; set; }
    }
}
