namespace DemaConsulting.Plexus;

/// <content>
///     Removal API: removing elements (with their wires), removing groups with or without their members.
/// </content>
public sealed partial class DiagramPage
{
    /// <summary>
    ///     Removes one element (undoable).
    /// </summary>
    /// <remarks>
    ///     Removing a node also removes its wires. Removing a group removes only the frame; its members
    ///     stay (use <see cref="RemoveWithMembers(DiagramElement)"/> to delete them too).
    /// </remarks>
    /// <param name="element">The element to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the element is not on this page.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed.</exception>
    public void Remove(DiagramElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        Remove([element]);
    }

    /// <summary>
    ///     Removes several elements as one undo entry.
    /// </summary>
    /// <remarks>
    ///     Same rules as <see cref="Remove(DiagramElement)"/>. Undo restores everything, including ids,
    ///     order, wires, routes and group membership.
    /// </remarks>
    /// <param name="elements">The elements to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an element is not on this page.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed.</exception>
    public void Remove(IEnumerable<DiagramElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        RemoveCore(elements, withMembers: false);
    }

    /// <summary>
    ///     Removes one element; groups are removed together with all their members (undoable).
    /// </summary>
    /// <param name="element">The element to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the element is not on this page.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed.</exception>
    public void RemoveWithMembers(DiagramElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        RemoveWithMembers([element]);
    }

    /// <summary>
    ///     Removes several elements; groups are removed together with all their members (one undo entry).
    /// </summary>
    /// <param name="elements">The elements to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an element is not on this page.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed.</exception>
    public void RemoveWithMembers(IEnumerable<DiagramElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        RemoveCore(elements, withMembers: true);
    }

    /// <summary>
    ///     Shared implementation of the removal methods.
    /// </summary>
    /// <param name="elements">The requested elements.</param>
    /// <param name="withMembers">True to delete the members of removed groups as well.</param>
    private void RemoveCore(IEnumerable<DiagramElement> elements, bool withMembers)
    {
        EnsureAttached();

        // Resolve the complete set first so validation errors leave the page untouched
        var doomed = new HashSet<DiagramElement>();
        var requested = new List<DiagramElement>();
        foreach (var element in elements)
        {
            ArgumentNullException.ThrowIfNull(element);
            requested.Add(element);
            if (!ReferenceEquals(element.Page, this))
            {
                throw new ArgumentException("All elements must be on this page.", nameof(elements));
            }

            doomed.Add(element);
            if (withMembers && element is Group group)
            {
                doomed.UnionWith(group.AllMembers());
            }
        }

        // A removed node takes every wire attached to it along
        foreach (var node in doomed.OfType<Node>().ToList())
        {
            doomed.UnionWith(_wires.Where(w => ReferenceEquals(w.Source.Node, node) || ReferenceEquals(w.Target.Node, node)));
        }

        if (doomed.Count == 0)
        {
            return;
        }

        // Iterate in z-order so the plan (and therefore history) is deterministic
        var ordered = _elements.Where(doomed.Contains).ToList();
        Diagram.Perform(DescribeRemoval(requested), plan =>
        {
            foreach (var wire in ordered.OfType<Wire>())
            {
                plan.Run(new ElementPresenceCommand("Remove wire", this, wire, add: false));
            }

            // Plain elements leave their group before they leave the page
            foreach (var element in ordered.Where(e => e is Node or Annotation))
            {
                if (element.Parent is { } parent)
                {
                    plan.Run(new GroupMembershipCommand("Leave group", parent, element, add: false));
                }

                plan.Run(new ElementPresenceCommand("Remove element", this, element, add: false));
            }

            // Inner groups go first so freed members flow outward through the group hierarchy
            foreach (var group in ordered.OfType<Group>().OrderByDescending(Depth))
            {
                ReleaseGroup(plan, group);
            }
        });
    }

    /// <summary>
    ///     Builds the undo description of a removal.
    /// </summary>
    /// <param name="removed">The elements the caller asked to remove.</param>
    /// <returns>A short description such as "Remove node" or "Remove 3 elements".</returns>
    private static string DescribeRemoval(IReadOnlyList<DiagramElement> removed) =>
        removed.Count == 1
            ? $"Remove {removed[0].GetType().Name.ToLowerInvariant()}"
            : $"Remove {removed.Count} elements";

    /// <summary>
    ///     Counts how many groups enclose a group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The nesting depth (0 for a top-level group).</returns>
    private static int Depth(Group group)
    {
        var depth = 0;
        for (var parent = group.Parent; parent is not null; parent = parent.Parent)
        {
            depth++;
        }

        return depth;
    }
}
