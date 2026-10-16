using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <content>
///     Port updates: planning everything a recomputation of a node's ports implies, inside the undo entry of the
///     property or data change that triggered it.
/// </content>
public sealed partial class DiagramPage
{
    /// <summary>
    ///     Recomputes a node's ports from its type's <see cref="NodeType.PortProvider"/> and plans the consequences into an open plan.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The steps run in a fixed order so that undo (which reverts them newest first) restores the model exactly:
    ///     </para>
    ///     <list type="number">
    ///         <item><description>an opening notification marker, recorded at the start of the plan (before the triggering
    ///         change), which reports the change on undo once everything is restored (see
    ///         <see cref="NodePortsCommand.CreateNotificationSteps"/>);</description></item>
    ///         <item><description>the wires of ports that disappear are removed (in page order);</description></item>
    ///         <item><description>the ports are swapped (<see cref="NodePortsCommand"/>; surviving names keep their port objects);</description></item>
    ///         <item><description>each remaining wire on a port whose type or fan-in changed is checked with the connection rules,
    ///         in page order (<see cref="Wires"/>), as a reconnect of itself: it does not count against its own ports, the rules
    ///         see it as <see cref="WireBeingReconnected"/>, and wires refused earlier in this step are already
    ///         removed. A refused wire is removed. On an input that now accepts a single wire, every earlier wire is therefore
    ///         refused and the wire latest in page order survives;</description></item>
    ///         <item><description>when the number of inputs or outputs changed, the node takes the height its longer port column
    ///         needs but never less than its type's default height (the sizing <see cref="AddNode(NodeType, Point, string?, string?)"/>
    ///         uses), so it grows or shrinks while keeping its position and width; a recomputation that keeps both counts leaves
    ///         the size alone, including a size the host set. Every remaining wire of the node whose end anchor moved is
    ///         re-routed like after a resize;</description></item>
    ///         <item><description>a closing notification marker reports the node's <c>Ports</c> and its groups' <c>Bounds</c>
    ///         as changed, once, after every wire removal, bounds and route change above (on undo the opening marker reports
    ///         it after everything is restored).</description></item>
    ///     </list>
    ///     Nothing is planned when the provider returns ports equivalent to the current ones.
    /// </remarks>
    /// <param name="plan">The open plan that already applied the triggering change.</param>
    /// <param name="node">The node on this page whose ports are recomputed; its type declares a provider.</param>
    /// <exception cref="InvalidOperationException">Thrown when the provider returns null.</exception>
    /// <exception cref="ArgumentException">Thrown when the provider builds an invalid <see cref="PortSet"/>.</exception>
    internal void PlanPortUpdate(CommandPlan plan, Node node)
    {
        // Ask the provider; equivalent ports mean there is nothing to do
        var change = NodePortsCommand.TryCreate(node);
        if (change is null)
        {
            return;
        }

        // 0. The opening marker goes first in the plan, before the triggering change, so that undo reports the change
        //    once everything, the triggering change included, is restored
        var (opening, closing) = NodePortsCommand.CreateNotificationSteps(node);
        plan.AddFirst(opening);

        // 1. Wires of ports that disappear go first, so hosts see them removed before the node reports new ports
        var removed = new HashSet<Port>(change.RemovedPorts);
        foreach (var wire in _wires.Where(w => removed.Contains(w.Source) || removed.Contains(w.Target)).ToList())
        {
            plan.Run(new ElementPresenceCommand("Remove wire", this, wire, add: false));
        }

        // Remember where the remaining wires of the node attach, to find the ones whose anchors move, and how many
        // ports each column had, to decide whether the node must be resized
        var before = _wires
            .Where(w => ReferenceEquals(w.Source.Node, node) || ReferenceEquals(w.Target.Node, node))
            .ToDictionary(w => w, w => (Source: w.Source.Anchor, Target: w.Target.Anchor));
        var inputCount = node.Inputs.Count;
        var outputCount = node.Outputs.Count;

        // 2. Swap the ports
        plan.Run(change);

        // 3. Wires on retyped ports must still be allowed; later wires are judged after earlier removals
        var retyped = new HashSet<Port>(change.RetypedPorts);
        foreach (var wire in _wires.Where(w => retyped.Contains(w.Source) || retyped.Contains(w.Target)).ToList())
        {
            if (!CheckConnection(wire.Source, wire.Target, wire).IsAllowed)
            {
                plan.Run(new ElementPresenceCommand("Remove wire", this, wire, add: false));
                before.Remove(wire);
            }
        }

        // 4. When the number of ports changed, fit the height to them (never below the type's default height, keeping
        //    position and width); an unchanged count leaves the size alone. Then re-route the wires whose anchors moved
        var layout = new LayoutCommand("Update ports", this);
        var changed = new Dictionary<Node, Rect>();
        if (node.Inputs.Count != inputCount || node.Outputs.Count != outputCount)
        {
            // Only a real difference is recorded (a node already at the fitted height needs no layout entry)
            var resized = node.Bounds with { Height = NodeGeometry.ProviderNodeHeight(node, Diagram.Theme) };
            if (resized != node.Bounds)
            {
                layout.AddBounds(node, node.Bounds, resized);
                changed[node] = resized;
            }
        }

        var forced = before
            .Where(entry => ReferenceEquals(entry.Key.Page, this)
                && (AnchorOf(entry.Key.Source, changed) != entry.Value.Source || AnchorOf(entry.Key.Target, changed) != entry.Value.Target))
            .Select(entry => entry.Key)
            .ToHashSet();
        PlanWireUpdates(layout, changed, forced);
        if (!layout.IsEmpty)
        {
            plan.Run(layout);
        }

        // 5. The model is settled: report the node's new ports (and the frames of its groups) once
        plan.Run(closing);
    }
}
