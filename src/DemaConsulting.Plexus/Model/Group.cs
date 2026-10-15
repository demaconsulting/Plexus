using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <summary>
///     A titled frame around nodes, annotations and other groups that moves, collapses and nests as a unit.
/// </summary>
/// <remarks>
///     An element belongs to at most one group. While expanded, the group's <see cref="Bounds"/> are
///     computed to enclose the full visual extent of its members (nodes include their name, summary
///     lines and port labels) plus padding and a title bar, so they follow the members
///     automatically. While collapsed, the group is a fixed-size box at <see cref="CollapsedPosition"/>
///     and its members are hidden. Create groups with <see cref="DiagramPage.CreateGroup"/>. While visible, a group's
///     frame is a routing obstacle for wires none of whose end nodes belong to the group (see
///     <see cref="DiagramTheme.GroupsAreObstacles"/>); wires are only re-routed when created, when an end node moves
///     and by <see cref="DiagramPage.RerouteAll"/>, so moving a group alone does not re-route unrelated wires. Not thread-safe.
///     <para>
///         A group has no position or size to set: while expanded its frame follows its members, so you move a group with
///         <see cref="DiagramPage.Move"/> (which moves every member, and a collapsed group's box). In the Avalonia
///         <c>DiagramEditor</c> the user collapses and expands a group by clicking the toggle in its title bar (which sets
///         <see cref="IsCollapsed"/>) and drags a group by its title bar. Wires to nodes inside a collapsed group are
///         drawn to the collapsed box, which shows read-only proxy ports: one triangle per distinct inner port whose wires cross the group boundary,
///         inputs on the left edge and outputs on the right edge, labeled with the inner port's path <c>Node.port</c> (for example <c>Ctrl2.fan</c>).
///         They are display only: they are not <see cref="Port"/>s of the group and cannot be connected (wires stay connected to the real node ports); see
///         <see cref="GroupProxyPortLayout"/>.
///     </para>
///     <para>
///         Inherited from <see cref="DiagramElement"/> (the member tables of this page list only what <see cref="Group"/> declares): <see cref="DiagramElement.Id"/>,
///         <see cref="DiagramElement.Page"/> (null while the group is removed), <see cref="DiagramElement.Parent"/> (the enclosing group of a nested group, or null),
///         <see cref="DiagramElement.IsVisible"/>, <see cref="DiagramElement.GetBounds"/> (the same rectangle as <see cref="Bounds"/>), <see cref="DiagramElement.Remove"/>
///         and the <see cref="DiagramElement.PropertyChanged"/> event.
///     </para>
/// </remarks>
/// <example>
///     Create, nest, collapse, move and dissolve groups. Every step is one undo entry.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///     var a = page.AddNode("demo.Source", 0, 0, name: "A");
///     var b = page.AddNode("demo.Sink", 200, 0, name: "B");
///     var c = page.AddNode("demo.Sink", 200, 150, name: "C");
///     page.Connect("A.out", "B.in");
///
///     Group inner = page.CreateGroup([a, b], "Front end");         // wires listed as members are ignored: they follow their nodes
///     Group outer = page.CreateGroup([inner, c], "Everything");    // groups nest; the members must share the same parent
///
///     inner.IsCollapsed = true;                                    // members hidden, the group shows as a box
///     page.Move([outer], 40, 0);                                   // moves every member at any depth
///     inner.Title = "Input stage";                                 // titles are undoable too
///     Console.WriteLine(outer.Members.Count);                      // 2: the inner group and C
///     Console.WriteLine(a.Parent == inner);                        // True
///     Console.WriteLine(a.IsVisible);                              // False while the group is collapsed
///
///     inner.RemoveMember(b);                                       // b becomes ungrouped
///     page.Ungroup(outer);                                         // members stay, the frame goes (same as page.Remove(outer))
///     page.RemoveWithMembers(inner);                               // removes the frame AND its members (and their wires)
///     </code>
/// </example>
public sealed partial class Group : DiagramElement
{
    /// <summary>
    ///     The mutable member list.
    /// </summary>
    private readonly ObservableCollection<DiagramElement> _members = [];

    /// <summary>
    ///     The group title.
    /// </summary>
    private string _title;

    /// <summary>
    ///     Whether the group is collapsed.
    /// </summary>
    private bool _isCollapsed;

    /// <summary>
    ///     Top-left corner of the box while collapsed.
    /// </summary>
    private Point _collapsedPosition;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Group"/> class.
    /// </summary>
    /// <param name="id">The element id.</param>
    /// <param name="title">The group title.</param>
    internal Group(string id, string title)
        : base(id)
    {
        _title = title;
        Members = new ReadOnlyObservableCollection<DiagramElement>(_members);
    }

    /// <summary>
    ///     Gets or sets the title shown in the group's title bar (undoable).
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the group is removed from its page.</exception>
    public string Title
    {
        get => _title;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var page = RequirePage();
            if (string.Equals(_title, value, StringComparison.Ordinal))
            {
                return;
            }

            page.Diagram.History.Execute(new SetValueCommand<string>(
                "Rename group",
                this,
                nameof(Title),
                _title,
                value,
                v =>
                {
                    _title = v;
                    NotifyChanged(nameof(Title));
                }));
        }
    }

    /// <summary>
    ///     Gets the direct members (nodes, annotations and nested groups) in order; observable and read-only.
    /// </summary>
    public ReadOnlyObservableCollection<DiagramElement> Members { get; }

    /// <summary>
    ///     Gets or sets a value indicating whether the group is collapsed (undoable).
    /// </summary>
    /// <remarks>
    ///     Collapsing records the group's current top-left corner as <see cref="CollapsedPosition"/>.
    ///     Members keep their own positions, so expanding restores the previous layout exactly.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the group is removed from its page.</exception>
    [SuppressMessage("Major Code Smell", "S4275:Getters and setters should access the expected fields", Justification = "The setter routes through the undoable collapse command, which writes the field via Group.SetCollapsedCore.")]
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => RequirePage().SetGroupCollapsed(this, value);
    }

    /// <summary>
    ///     Gets the top-left corner of the group's box while collapsed.
    /// </summary>
    public Point CollapsedPosition => _collapsedPosition;

    /// <summary>
    ///     Gets the group's bounding box: fitted around the members when expanded, a fixed theme-sized box when collapsed.
    /// </summary>
    public Rect Bounds => _isCollapsed || _members.Count == 0 ? CollapsedBounds() : ExpandedBounds();

    /// <summary>
    ///     Gets the rectangle the group occupies: the fitted frame while expanded, the collapsed box while collapsed.
    /// </summary>
    /// <returns>The same value as <see cref="Bounds"/>.</returns>
    public override Rect GetBounds() => Bounds;

    /// <summary>
    ///     Enumerates every member at any depth (members of nested groups included, nested groups themselves too).
    /// </summary>
    /// <returns>The descendants in depth-first order.</returns>
    public IEnumerable<DiagramElement> AllMembers()
    {
        foreach (var member in _members)
        {
            yield return member;
            if (member is Group nested)
            {
                foreach (var inner in nested.AllMembers())
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>
    ///     Adds an element of the same page to this group (undoable), taking it out of its current group first.
    /// </summary>
    /// <param name="element">A node, annotation or group on the same page.</param>
    /// <exception cref="ArgumentException">Thrown when the element is a wire or is on another page.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the element is this group or one of its ancestors, or the group is detached.</exception>
    public void AddMember(DiagramElement element) => RequirePage().AddToGroup(this, element);

    /// <summary>
    ///     Removes an element from this group (undoable); the element becomes ungrouped.
    /// </summary>
    /// <param name="element">A direct member.</param>
    /// <exception cref="ArgumentException">Thrown when the element is not a direct member.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the group is detached.</exception>
    public void RemoveMember(DiagramElement element) => RequirePage().RemoveFromGroup(this, element);

    /// <summary>
    ///     Determines whether this group is the given group or lies inside it at any depth.
    /// </summary>
    /// <param name="other">The potential ancestor.</param>
    /// <returns><see langword="true"/> when <paramref name="other"/> is this group or an ancestor.</returns>
    internal bool IsSelfOrDescendantOf(Group other)
    {
        for (var group = this; group is not null; group = group.Parent)
        {
            if (ReferenceEquals(group, other))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Computes the bounds the group has while expanded: member extents plus padding and title bar.
    /// </summary>
    /// <remarks>
    ///     Used both for <see cref="Bounds"/> and to remember the top-left corner when collapsing.
    ///     An empty group falls back to its collapsed box.
    /// </remarks>
    /// <returns>The expanded bounds.</returns>
    internal Rect ExpandedBounds()
    {
        if (_members.Count == 0)
        {
            return CollapsedBounds();
        }

        // Union of all member boxes, using each member's own (possibly nested) bounds
        var union = MemberBounds(_members[0]);
        for (var i = 1; i < _members.Count; i++)
        {
            union = union.Union(MemberBounds(_members[i]));
        }

        var theme = Theme;
        var padding = theme.GroupPadding;
        return new Rect(
            union.Left - padding,
            union.Top - padding - theme.GroupTitleHeight,
            union.Width + (2 * padding),
            union.Height + (2 * padding) + theme.GroupTitleHeight);
    }

    /// <summary>
    ///     Inserts a member and updates its parent link; used by commands.
    /// </summary>
    /// <param name="index">The insertion index, or a negative value to append.</param>
    /// <param name="member">The member to insert.</param>
    internal void InsertMemberCore(int index, DiagramElement member)
    {
        member.Parent = this;
        _members.Insert(index < 0 ? _members.Count : index, member);
        NotifyChanged(nameof(Members));
        NotifyBoundsChanged();
    }

    /// <summary>
    ///     Removes a member and clears its parent link; used by commands.
    /// </summary>
    /// <param name="member">The member to remove.</param>
    /// <returns>The index the member had.</returns>
    internal int RemoveMemberCore(DiagramElement member)
    {
        // Detach the parent link first so observers of the list change see an ungrouped element
        var index = _members.IndexOf(member);
        member.Parent = null;
        _members.RemoveAt(index);
        NotifyChanged(nameof(Members));
        NotifyBoundsChanged();
        return index;
    }

    /// <summary>
    ///     Stores the collapsed state and position and raises change notifications; used by commands.
    /// </summary>
    /// <param name="collapsed">The collapsed state.</param>
    /// <param name="position">The collapsed top-left corner.</param>
    internal void SetCollapsedCore(bool collapsed, Point position)
    {
        _isCollapsed = collapsed;
        _collapsedPosition = position;
        NotifyChanged(nameof(IsCollapsed));
        NotifyBoundsChanged();
    }

    /// <summary>
    ///     Stores only the collapsed position (when the group moves) and raises change notifications; used by commands.
    /// </summary>
    /// <param name="position">The collapsed top-left corner.</param>
    internal void SetCollapsedPositionCore(Point position)
    {
        _collapsedPosition = position;
        NotifyChanged(nameof(CollapsedPosition));
    }

    /// <summary>
    ///     Reports a bounds change on this group and every ancestor, since their fitted bounds depend on it.
    /// </summary>
    internal void NotifyBoundsChanged()
    {
        for (var group = this; group is not null; group = group.Parent)
        {
            group.NotifyChanged(nameof(Bounds));
        }
    }

    /// <summary>
    ///     Computes the fixed box used while collapsed.
    /// </summary>
    /// <returns>The collapsed bounds.</returns>
    private Rect CollapsedBounds() =>
        new(_collapsedPosition, new Size(Theme.CollapsedGroupWidth, Theme.CollapsedGroupHeight));

    /// <summary>
    ///     Gets the bounds of a member element.
    /// </summary>
    /// <remarks>
    ///     A node contributes its full visual extent (name above, summary below, port labels at the
    ///     sides, see <see cref="NodeGeometry.VisualBounds(Node, DiagramTheme)"/>) rather than just its box, so the frame
    ///     never cuts through text drawn for the node.
    /// </remarks>
    /// <param name="member">A node, annotation or group.</param>
    /// <returns>The member's extent.</returns>
    private Rect MemberBounds(DiagramElement member) => member switch
    {
        Node node => NodeGeometry.VisualBounds(node, Theme),
        Annotation annotation => annotation.Bounds,
        Group group => group.Bounds,
        _ => throw new InvalidOperationException("Groups can only contain nodes, annotations and groups."),
    };
}
