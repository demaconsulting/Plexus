namespace DemaConsulting.Plexus;

/// <summary>
///     Adds an element to a group or removes it, remembering the member position so the reverse is lossless.
/// </summary>
/// <remarks>
///     Group membership is the primitive behind grouping, ungrouping and moving grouped elements
///     between pages. The position is captured when the element is removed (and fixed when it is
///     added) so undo restores member order exactly.
/// </remarks>
internal sealed class GroupMembershipCommand : IUndoableCommand
{
    /// <summary>
    ///     The group whose member list changes.
    /// </summary>
    private readonly Group _group;

    /// <summary>
    ///     The member added or removed.
    /// </summary>
    private readonly DiagramElement _member;

    /// <summary>
    ///     True when Do adds the member; false when Do removes it.
    /// </summary>
    private readonly bool _add;

    /// <summary>
    ///     The member's position in the group, or -1 for "append" before it is known.
    /// </summary>
    private int _index;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GroupMembershipCommand"/> class.
    /// </summary>
    /// <param name="description">Human-readable description.</param>
    /// <param name="group">The group whose members change.</param>
    /// <param name="member">The member to add or remove.</param>
    /// <param name="add">True to add on Do; false to remove on Do.</param>
    /// <param name="index">For additions, the insertion position, or -1 to append.</param>
    public GroupMembershipCommand(string description, Group group, DiagramElement member, bool add, int index = -1)
    {
        Description = description;
        _group = group;
        _member = member;
        _add = add;
        _index = index;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public void Do()
    {
        if (_add)
        {
            Insert();
        }
        else
        {
            Remove();
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        if (_add)
        {
            Remove();
        }
        else
        {
            Insert();
        }
    }

    /// <summary>
    ///     Inserts the member at the remembered position and fixes the position for later replays.
    /// </summary>
    private void Insert()
    {
        _index = _index < 0 ? _group.Members.Count : _index;
        _group.InsertMemberCore(_index, _member);
    }

    /// <summary>
    ///     Removes the member and remembers the position it had.
    /// </summary>
    private void Remove() => _index = _group.RemoveMemberCore(_member);
}
