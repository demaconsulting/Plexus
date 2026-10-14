namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     The computed geometry of one group as the scene draws and hit-tests it.
/// </summary>
/// <remarks>
///     The rectangles are the very values the scene builder used for its drawing primitives and hit
///     targets, so hosts (automation, overlays, custom gestures) never need to re-derive them from theme
///     metrics. <see cref="Frame"/> is the group frame when expanded and the summary box when collapsed.
///     The proxy port lists are only filled for collapsed groups.
/// </remarks>
/// <param name="Group">The group.</param>
/// <param name="IsCollapsed">Whether the group is drawn as a collapsed box.</param>
/// <param name="Frame">The expanded group frame, or the collapsed box.</param>
/// <param name="Header">The title bar: the strip along the top of <see cref="Frame"/> that moves the group.</param>
/// <param name="CollapseToggle">The collapse/expand toggle, inside <see cref="Header"/>; clicking it toggles the group.</param>
/// <param name="InputPorts">The proxy ports on the left edge of a collapsed box (empty when expanded).</param>
/// <param name="OutputPorts">The proxy ports on the right edge of a collapsed box (empty when expanded).</param>
public sealed record GroupLayout(
    Group Group,
    bool IsCollapsed,
    Rect Frame,
    Rect Header,
    Rect CollapseToggle,
    IReadOnlyList<GroupProxyPortLayout> InputPorts,
    IReadOnlyList<GroupProxyPortLayout> OutputPorts);

/// <summary>
///     The computed geometry of one read-only proxy port on a collapsed group box.
/// </summary>
/// <param name="Port">The inner port whose wires the proxy stands in for.</param>
/// <param name="Anchor">The point displayed wires attach to, at the outer end of the triangle and label.</param>
/// <param name="Triangle">The rectangle of the proxy's triangle, outside the box edge.</param>
/// <param name="Label">The proxy's label (the inner port's <c>Node.port</c> path) and its rectangle.</param>
public sealed record GroupProxyPortLayout(Port Port, Point Anchor, Rect Triangle, LayoutText Label);
