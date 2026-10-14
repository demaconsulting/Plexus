namespace DemaConsulting.Plexus;

/// <content>
///     Layout API: moving and resizing elements, and keeping wire routes consistent with the change.
/// </content>
public sealed partial class DiagramPage
{
    /// <summary>
    ///     Tolerance when comparing two translation deltas for equality.
    /// </summary>
    private const double DeltaTolerance = 1e-9;

    /// <summary>
    ///     Recomputes the route of every wire on the page with the diagram's router (one undoable entry).
    /// </summary>
    /// <remarks>
    ///     Manual route edits are discarded. Use it after building a diagram in code, or after
    ///     changing <see cref="Diagram.Router"/> or the theme, so every wire avoids all the nodes
    ///     that now exist, and every group frame that none of its end nodes lies in (see
    ///     <see cref="Scene.DiagramTheme.GroupsAreObstacles"/>). Wires whose route does not change record
    ///     nothing; when no route changes at all, no history entry is created.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed.</exception>
    public void RerouteAll()
    {
        EnsureAttached();
        if (_wires.Count == 0)
        {
            return;
        }

        // Obstacles are shared by every wire; the transaction folds all route changes into one entry.
        // Wires are routed in page order and each new route joins the traffic the next wire keeps clear of
        var obstacles = BuildObstacles(null);
        var groups = BuildGroupObstacles();
        var traffic = new WireTraffic();
        using var transaction = Diagram.History.BeginTransaction("Reroute all wires");
        foreach (var wire in _wires.ToArray())
        {
            var route = ComputeRoute(wire.Source, wire.Target, obstacles, null, traffic, groups);
            wire.SetRoute(route);
            traffic.Add(wire.Source, wire.Target, wire.Source.Anchor, route, wire.Target.Anchor);
        }
    }

    /// <summary>
    ///     Moves elements by a delta (undoable).
    /// </summary>
    /// <remarks>
    ///     Groups bring their members along. Wires are never moved directly: a wire whose BOTH end
    ///     nodes moved (by the same delta) has its route translated so a manual shape is preserved,
    ///     while a wire with only one moved end is re-routed by the router, discarding manual edits.
    ///     Wires passed in <paramref name="elements"/> are ignored. A zero delta records nothing.
    ///     Consecutive moves inside one transaction merge into a single history entry.
    /// </remarks>
    /// <param name="elements">The nodes, annotations and groups to move; all must be on this page.</param>
    /// <param name="dx">Horizontal delta.</param>
    /// <param name="dy">Vertical delta.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a delta is not finite or an element is not on this page.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed.</exception>
    public void Move(IEnumerable<DiagramElement> elements, double dx, double dy)
    {
        ArgumentNullException.ThrowIfNull(elements);
        EnsureFinite(dx, nameof(dx));
        EnsureFinite(dy, nameof(dy));
        EnsureAttached();

        // Expand groups to their members, keeping first-seen order and dropping duplicates
        var movable = new List<DiagramElement>();
        var seen = new HashSet<DiagramElement>();
        foreach (var element in elements)
        {
            ArgumentNullException.ThrowIfNull(element);
            if (!ReferenceEquals(element.Page, this))
            {
                throw new ArgumentException("All elements must be on this page.", nameof(elements));
            }

            if (element is Wire)
            {
                continue;
            }

            AddUnique(movable, seen, element);
            if (element is Group group)
            {
                foreach (var member in group.AllMembers())
                {
                    AddUnique(movable, seen, member);
                }
            }
        }

        // Nothing to do for an empty set or a zero delta
        if (movable.Count == 0 || (dx == 0 && dy == 0))
        {
            return;
        }

        var newBounds = new Dictionary<DiagramElement, Rect>();
        var newPositions = new Dictionary<Group, Point>();
        foreach (var element in movable)
        {
            switch (element)
            {
                case Node node:
                    newBounds[node] = node.Bounds.Offset(dx, dy);
                    break;
                case Annotation annotation:
                    newBounds[annotation] = annotation.Bounds.Offset(dx, dy);
                    break;
                case Group group:
                    newPositions[group] = group.CollapsedPosition.Offset(dx, dy);
                    break;
            }
        }

        CommitLayout("Move", newBounds, newPositions);
    }

    /// <summary>
    ///     Sets the bounds of a node or annotation; used by their Bounds setters.
    /// </summary>
    /// <param name="element">The node or annotation.</param>
    /// <param name="bounds">The new bounds.</param>
    internal void ApplyBounds(DiagramElement element, Rect bounds)
    {
        EnsureAttached();
        EnsureValidBounds(bounds, nameof(bounds));
        var current = element switch
        {
            Node node => node.Bounds,
            Annotation annotation => annotation.Bounds,
            _ => throw new InvalidOperationException("Only nodes and annotations have settable bounds."),
        };

        if (current == bounds)
        {
            return;
        }

        var description = current.Size == bounds.Size ? "Move" : "Resize";
        CommitLayout(description, new Dictionary<DiagramElement, Rect> { [element] = bounds }, new Dictionary<Group, Point>());
    }

    /// <summary>
    ///     Applies a set of bounds and collapsed-position changes together with the wire route updates they imply.
    /// </summary>
    /// <remarks>
    ///     Routes are computed against the hypothetical new layout before anything changes, so the
    ///     resulting <see cref="LayoutCommand"/> contains exact old and new states and can be executed
    ///     (and merged) like any other command.
    /// </remarks>
    /// <param name="description">The undo description.</param>
    /// <param name="newBounds">New bounds of nodes and annotations.</param>
    /// <param name="newPositions">New collapsed positions of groups.</param>
    private void CommitLayout(
        string description,
        IReadOnlyDictionary<DiagramElement, Rect> newBounds,
        IReadOnlyDictionary<Group, Point> newPositions)
    {
        var command = new LayoutCommand(description, this);
        var changedNodes = new Dictionary<Node, Rect>();
        foreach (var (element, bounds) in newBounds)
        {
            var old = element is Node n ? n.Bounds : ((Annotation)element).Bounds;
            if (old == bounds)
            {
                continue;
            }

            command.AddBounds(element, old, bounds);
            if (element is Node node)
            {
                changedNodes[node] = bounds;
            }
        }

        foreach (var (group, position) in newPositions)
        {
            if (group.CollapsedPosition != position)
            {
                command.AddPosition(group, group.CollapsedPosition, position);
            }
        }

        PlanWireUpdates(command, changedNodes);
        if (!command.IsEmpty)
        {
            Diagram.History.Execute(command);
        }
    }

    /// <summary>
    ///     Adds the route changes implied by node bounds changes: translate when both ends moved alike, otherwise re-route.
    /// </summary>
    /// <param name="command">The command to add route changes to.</param>
    /// <param name="changedNodes">The nodes whose bounds change and their new bounds.</param>
    private void PlanWireUpdates(LayoutCommand command, IReadOnlyDictionary<Node, Rect> changedNodes)
    {
        if (changedNodes.Count == 0)
        {
            return;
        }

        // Wires that only translate (or are untouched) keep their shape and form the traffic the re-routed
        // wires keep clear of; wires that need a new route are collected and routed afterwards, in page order
        IReadOnlyList<Rect>? obstacles = null;
        var traffic = new WireTraffic();
        var rerouted = new List<Wire>();
        foreach (var wire in _wires)
        {
            var sourceMoved = changedNodes.TryGetValue(wire.Source.Node, out var sourceBounds);
            var targetMoved = changedNodes.TryGetValue(wire.Target.Node, out var targetBounds);
            if (!sourceMoved && !targetMoved)
            {
                if (!wire.Source.Node.IsHiddenByCollapse && !wire.Target.Node.IsHiddenByCollapse)
                {
                    traffic.Add(wire.Source, wire.Target, wire.Source.Anchor, wire.Route, wire.Target.Anchor);
                }

                continue;
            }

            if (sourceMoved && targetMoved
                && TryGetCommonTranslation(wire.Source.Node.Bounds, sourceBounds, wire.Target.Node.Bounds, targetBounds, out var delta))
            {
                var route = wire.Route.Select(p => p.Offset(delta.X, delta.Y)).ToArray();
                traffic.Add(wire.Source, wire.Target, AnchorOf(wire.Source, changedNodes), route, AnchorOf(wire.Target, changedNodes));
                if (!wire.Route.SequenceEqual(route))
                {
                    command.AddRoute(wire, [.. wire.Route], route);
                }
            }
            else
            {
                rerouted.Add(wire);
            }
        }

        GroupObstacles? groups = null;
        foreach (var wire in rerouted)
        {
            obstacles ??= BuildObstacles(changedNodes);
            groups ??= BuildGroupObstacles();
            var route = ComputeRoute(wire.Source, wire.Target, obstacles, changedNodes, traffic, groups);
            traffic.Add(wire.Source, wire.Target, AnchorOf(wire.Source, changedNodes), route, AnchorOf(wire.Target, changedNodes));
            if (!wire.Route.SequenceEqual(route))
            {
                command.AddRoute(wire, [.. wire.Route], route);
            }
        }
    }

    /// <summary>
    ///     Determines whether two nodes were moved by exactly the same translation without resizing.
    /// </summary>
    /// <param name="oldA">First node's old bounds.</param>
    /// <param name="newA">First node's new bounds.</param>
    /// <param name="oldB">Second node's old bounds.</param>
    /// <param name="newB">Second node's new bounds.</param>
    /// <param name="delta">Receives the shared translation.</param>
    /// <returns><see langword="true"/> when both nodes were translated by the same delta and neither was resized.</returns>
    private static bool TryGetCommonTranslation(Rect oldA, Rect newA, Rect oldB, Rect newB, out Point delta)
    {
        delta = newA.Position - oldA.Position;
        var other = newB.Position - oldB.Position;
        return oldA.Size == newA.Size
            && oldB.Size == newB.Size
            && Math.Abs(delta.X - other.X) <= DeltaTolerance
            && Math.Abs(delta.Y - other.Y) <= DeltaTolerance;
    }

    /// <summary>
    ///     Adds an element to an ordered list if it has not been seen yet.
    /// </summary>
    /// <param name="list">The ordered result list.</param>
    /// <param name="seen">The set of elements already added.</param>
    /// <param name="element">The element to add.</param>
    private static void AddUnique(List<DiagramElement> list, HashSet<DiagramElement> seen, DiagramElement element)
    {
        if (seen.Add(element))
        {
            list.Add(element);
        }
    }
}
