namespace DemaConsulting.Plexus;

/// <content>
///     Moving elements between pages as one undoable operation.
/// </content>
public sealed partial class Diagram
{
    /// <summary>
    ///     Moves elements to another page (one undo entry).
    /// </summary>
    /// <remarks>
    ///     Nodes, annotations and groups move; a group brings all its members. Wires between two moved
    ///     nodes move with them and keep their routes. Wires between a moved node and a node that stays
    ///     behind are removed, because a wire cannot span pages. Positions, ids and names are unchanged.
    ///     Elements leaving a group whose other members stay behind are released from that group.
    ///     Undo restores every element, wire, group membership and list position. Wires passed in
    ///     <paramref name="elements"/> are ignored; elements already on the target page are skipped.
    /// </remarks>
    /// <example>
    ///     Move two connected nodes to a new page.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var diagram = new Diagram(types);                      // types: the registry of the DemaConsulting.Plexus example
    ///     var first = diagram.Pages.Add("Main");
    ///     var a = first.AddNode("demo.Source", 0, 0, name: "A");
    ///     var b = first.AddNode("demo.Sink", 180, 0, name: "B");
    ///     first.Connect("A.out", "B.in");
    ///
    ///     var second = diagram.Pages.Add("Second");
    ///     diagram.MoveToPage([a, b], second);                    // one undo entry; the wire A.out -> B.in moves with them
    ///     diagram.History.Undo();                                // everything is back on the first page
    ///     </code>
    /// </example>
    /// <param name="elements">The elements to move; may come from several pages of this diagram.</param>
    /// <param name="target">The destination page; must belong to this diagram.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the target or an element does not belong to this diagram.</exception>
    public void MoveToPage(IEnumerable<DiagramElement> elements, DiagramPage target)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(target);
        if (!ReferenceEquals(target.Diagram, this) || !target.IsAttached)
        {
            throw new ArgumentException("The target page must be a page of this diagram.", nameof(target));
        }

        // Resolve the set of moving elements, expanding groups to their members
        var moving = new HashSet<DiagramElement>();
        foreach (var element in elements)
        {
            ArgumentNullException.ThrowIfNull(element);
            if (element.Page is not { IsAttached: true } page || !ReferenceEquals(page.Diagram, this))
            {
                throw new ArgumentException("All elements must be attached to this diagram.", nameof(elements));
            }

            if (element is Wire)
            {
                continue;
            }

            moving.Add(element);
            if (element is Group group)
            {
                moving.UnionWith(group.AllMembers());
            }
        }

        moving.RemoveWhere(e => ReferenceEquals(e.Page, target));
        if (moving.Count == 0)
        {
            return;
        }

        var movingNodes = moving.OfType<Node>().ToHashSet();
        var sourcePages = Pages.Where(p => p.Elements.Any(moving.Contains) || p.Wires.Any(w => movingNodes.Contains(w.Source.Node) || movingNodes.Contains(w.Target.Node))).ToList();

        Perform("Move to page", plan =>
        {
            // Wires first: classify them per source page and detach every wire that is affected
            var carried = new List<Wire>();
            foreach (var page in sourcePages)
            {
                foreach (var wire in page.Wires.ToList())
                {
                    var sourceMoves = movingNodes.Contains(wire.Source.Node);
                    var targetMoves = movingNodes.Contains(wire.Target.Node);
                    if (!sourceMoves && !targetMoves)
                    {
                        continue;
                    }

                    plan.Run(new ElementPresenceCommand("Detach wire", page, wire, add: false));

                    // A wire survives only when both of its nodes travel together
                    if (sourceMoves && targetMoves)
                    {
                        carried.Add(wire);
                    }
                }
            }

            // Leave groups that do not travel as a whole, then detach in source z-order
            var ordered = sourcePages.SelectMany(p => p.Elements.Where(moving.Contains).Select(e => (Page: p, Element: e))).ToList();
            foreach (var (_, element) in ordered)
            {
                if (element.Parent is { } parent && !moving.Contains(parent))
                {
                    plan.Run(new GroupMembershipCommand("Leave group", parent, element, add: false));
                }
            }

            foreach (var (page, element) in ordered)
            {
                plan.Run(new ElementPresenceCommand("Detach element", page, element, add: false));
            }

            // Attach on the target: elements keep their relative order, wires come last
            foreach (var (_, element) in ordered)
            {
                plan.Run(new ElementPresenceCommand("Attach element", target, element, add: true));
            }

            foreach (var wire in carried)
            {
                plan.Run(new ElementPresenceCommand("Attach wire", target, wire, add: true));
            }
        });
    }
}
