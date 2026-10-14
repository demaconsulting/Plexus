using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <summary>
///     The group frames of a page, prepared once so every wire routed in one operation can add the frames it must avoid.
/// </summary>
/// <remarks>
///     A group frame is a routing obstacle for a wire unless one of the wire's two end nodes lies
///     inside the group at any depth (see <see cref="DiagramTheme.GroupsAreObstacles"/>), so the
///     list of obstacles depends on the wire. The frames themselves (the group's bounds inflated by
///     the theme's obstacle margin) are computed once because fitting an expanded group measures
///     all of its members.
/// </remarks>
internal sealed class GroupObstacles
{
    /// <summary>
    ///     The prepared frames in group order.
    /// </summary>
    private readonly List<(Group Group, Rect Box)> _frames = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="GroupObstacles"/> class.
    /// </summary>
    /// <param name="groups">The groups of a page.</param>
    /// <param name="theme">The theme supplying the option and the obstacle margin.</param>
    /// <param name="includeCollapsed">
    ///     Whether collapsed groups contribute their box; scene display routes pass false because
    ///     they add the (taller) boxes of collapsed groups themselves.
    /// </param>
    public GroupObstacles(IEnumerable<Group> groups, DiagramTheme theme, bool includeCollapsed)
    {
        if (!theme.GroupsAreObstacles)
        {
            return;
        }

        // Groups hidden inside a collapsed group are not drawn and therefore do not block anything
        foreach (var group in groups)
        {
            if (!group.IsHiddenByCollapse && (includeCollapsed || !group.IsCollapsed))
            {
                _frames.Add((group, group.Bounds.Inflate(theme.ObstacleMargin)));
            }
        }
    }

    /// <summary>
    ///     Adds the frames a wire between two nodes has to avoid to a list of obstacles.
    /// </summary>
    /// <param name="obstacles">The node obstacles shared by all wires.</param>
    /// <param name="first">One end node of the wire.</param>
    /// <param name="second">The other end node of the wire.</param>
    /// <returns>
    ///     <paramref name="obstacles"/> itself when no frame applies, otherwise a read-only view of
    ///     the node obstacles followed by the applicable frames (the obstacles are not copied).
    /// </returns>
    public IReadOnlyList<Rect> For(IReadOnlyList<Rect> obstacles, Node first, Node second)
    {
        List<Rect>? frames = null;
        foreach (var (group, box) in _frames)
        {
            if (IsInside(first, group) || IsInside(second, group))
            {
                continue;
            }

            frames ??= [];
            frames.Add(box);
        }

        return frames is null ? obstacles : new ExtendedObstacles(obstacles, frames);
    }

    /// <summary>
    ///     Determines whether a node lies inside a group at any depth.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="group">The group.</param>
    /// <returns><see langword="true"/> when the group is an ancestor of the node.</returns>
    private static bool IsInside(Node node, Group group)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (ReferenceEquals(parent, group))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
///     A read-only view of a list of obstacles followed by extra boxes, without copying the obstacles.
/// </summary>
/// <param name="obstacles">The shared obstacles.</param>
/// <param name="extra">The boxes that follow them.</param>
internal sealed class ExtendedObstacles(IReadOnlyList<Rect> obstacles, List<Rect> extra) : IReadOnlyList<Rect>
{
    /// <inheritdoc/>
    public int Count => obstacles.Count + extra.Count;

    /// <inheritdoc/>
    public Rect this[int index] => index < obstacles.Count ? obstacles[index] : extra[index - obstacles.Count];

    /// <inheritdoc/>
    public IEnumerator<Rect> GetEnumerator() => obstacles.Concat(extra).GetEnumerator();

    /// <inheritdoc/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
