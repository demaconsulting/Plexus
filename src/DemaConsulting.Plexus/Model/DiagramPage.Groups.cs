namespace DemaConsulting.Plexus;

/// <content>
///     Group API: creation, ungrouping, membership changes and collapse.
/// </content>
public sealed partial class DiagramPage
{
    /// <summary>
    ///     Creates a group around existing elements (undoable).
    /// </summary>
    /// <remarks>
    ///     Wires in <paramref name="members"/> are ignored: wires follow their nodes and are never
    ///     members. All members must either be ungrouped or belong to the same group; in the latter
    ///     case the new group nests inside that group at the position of the first member.
    /// </remarks>
    /// <param name="members">Nodes, annotations and groups of this page.</param>
    /// <param name="title">The group title.</param>
    /// <param name="id">Optional; defaults to null, which generates an element id. May be null.</param>
    /// <returns>The new group.</returns>
    /// <exception cref="ArgumentException">Thrown when no members are supplied, a member is on another page, or the id is in use.</exception>
    /// <exception cref="InvalidOperationException">Thrown when members belong to different groups, or the page is removed.</exception>
    public Group CreateGroup(IEnumerable<DiagramElement> members, string title, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(title);
        EnsureAttached();

        // Wires are not members; everything else must live on this page
        var list = members.Where(m => m is not Wire).Distinct().ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("A group needs at least one node, annotation or group.", nameof(members));
        }

        if (list.Any(m => !ReferenceEquals(m.Page, this)))
        {
            throw new ArgumentException("All members must be on this page.", nameof(members));
        }

        var parent = list[0].Parent;
        if (list.Any(m => !ReferenceEquals(m.Parent, parent)))
        {
            throw new InvalidOperationException("Members must either all be ungrouped or all belong to the same group.");
        }

        var group = new Group(ResolveId(id), title);
        Diagram.Perform("Create group", plan =>
        {
            plan.Run(new ElementPresenceCommand("Add group", this, group, add: true));

            // When regrouping inside an existing group, the new group takes the first member's place
            if (parent is not null)
            {
                var insertAt = list.Min(m => parent.Members.IndexOf(m));
                foreach (var member in list)
                {
                    plan.Run(new GroupMembershipCommand("Release member", parent, member, add: false));
                }

                plan.Run(new GroupMembershipCommand("Nest group", parent, group, add: true, insertAt));
            }

            foreach (var member in list)
            {
                plan.Run(new GroupMembershipCommand("Add member", group, member, add: true));
            }

            // Remember where the group sits so a later collapse has a sensible starting position
            SetCollapsedPosition(plan, group, group.ExpandedBounds().Position);
        });

        return group;
    }

    /// <summary>
    ///     Dissolves a group, keeping its members (undoable).
    /// </summary>
    /// <remarks>
    ///     The members take the group's place in its parent group (or become top-level). Equivalent to
    ///     <see cref="Remove(DiagramElement)"/> on a group.
    /// </remarks>
    /// <param name="group">The group to dissolve.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="group"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the group is not on this page.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed.</exception>
    public void Ungroup(Group group)
    {
        ArgumentNullException.ThrowIfNull(group);
        EnsureAttached();
        if (!ReferenceEquals(group.Page, this))
        {
            throw new ArgumentException("The group is not on this page.", nameof(group));
        }

        Diagram.Perform("Ungroup", plan => ReleaseGroup(plan, group));
    }

    /// <summary>
    ///     Sets a group's collapsed state; used by <see cref="Group.IsCollapsed"/>.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="collapsed">The wanted state.</param>
    internal void SetGroupCollapsed(Group group, bool collapsed)
    {
        EnsureAttached();
        if (group.IsCollapsed == collapsed)
        {
            return;
        }

        // Collapsing pins the box where the expanded frame currently is; expanding keeps the pin
        var oldPosition = group.CollapsedPosition;
        var newPosition = collapsed ? group.ExpandedBounds().Position : oldPosition;
        var oldCollapsed = group.IsCollapsed;
        Diagram.History.Execute(
            collapsed ? "Collapse group" : "Expand group",
            () => group.SetCollapsedCore(collapsed, newPosition),
            () => group.SetCollapsedCore(oldCollapsed, oldPosition));
    }

    /// <summary>
    ///     Adds an element to a group, taking it out of its current group; used by <see cref="Group.AddMember"/>.
    /// </summary>
    /// <param name="group">The target group.</param>
    /// <param name="element">The element to add.</param>
    internal void AddToGroup(Group group, DiagramElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        EnsureAttached();
        if (element is Wire)
        {
            throw new ArgumentException("Wires cannot be group members.", nameof(element));
        }

        if (!ReferenceEquals(element.Page, this))
        {
            throw new ArgumentException("The element must be on the same page as the group.", nameof(element));
        }

        if (element is Group nested && group.IsSelfOrDescendantOf(nested))
        {
            throw new InvalidOperationException("A group cannot be added to itself or to one of its own members.");
        }

        if (ReferenceEquals(element.Parent, group))
        {
            return;
        }

        Diagram.Perform("Add to group", plan =>
        {
            if (element.Parent is { } oldParent)
            {
                plan.Run(new GroupMembershipCommand("Leave group", oldParent, element, add: false));
            }

            plan.Run(new GroupMembershipCommand("Join group", group, element, add: true));
        });
    }

    /// <summary>
    ///     Removes an element from a group; used by <see cref="Group.RemoveMember"/>.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="element">The direct member to release.</param>
    internal void RemoveFromGroup(Group group, DiagramElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        EnsureAttached();
        if (!ReferenceEquals(element.Parent, group))
        {
            throw new ArgumentException("The element is not a direct member of the group.", nameof(element));
        }

        Diagram.History.Execute(new GroupMembershipCommand("Remove from group", group, element, add: false));
    }

    /// <summary>
    ///     Plans the dissolution of a group frame: members replace it in its parent, then the frame is removed.
    /// </summary>
    /// <param name="plan">The plan collecting the steps.</param>
    /// <param name="group">The group whose frame is removed.</param>
    private void ReleaseGroup(CommandPlan plan, Group group)
    {
        var parent = group.Parent;
        var insertAt = parent?.Members.IndexOf(group) ?? -1;
        var members = group.Members.ToList();

        foreach (var member in members)
        {
            plan.Run(new GroupMembershipCommand("Release member", group, member, add: false));
        }

        // The frame leaves its parent first so the freed members can take its slot in order
        if (parent is not null)
        {
            plan.Run(new GroupMembershipCommand("Leave group", parent, group, add: false));
            for (var i = 0; i < members.Count; i++)
            {
                plan.Run(new GroupMembershipCommand("Join group", parent, members[i], add: true, insertAt + i));
            }
        }

        plan.Run(new ElementPresenceCommand("Remove group", this, group, add: false));
    }

    /// <summary>
    ///     Plans a change of a group's remembered collapsed position.
    /// </summary>
    /// <param name="plan">The plan collecting the steps.</param>
    /// <param name="group">The group.</param>
    /// <param name="position">The new collapsed position.</param>
    private static void SetCollapsedPosition(CommandPlan plan, Group group, Point position)
    {
        var old = group.CollapsedPosition;
        if (old == position)
        {
            return;
        }

        plan.Run(
            "Set group position",
            () => group.SetCollapsedPositionCore(position),
            () => group.SetCollapsedPositionCore(old));
    }
}
