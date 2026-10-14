namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     The computed geometry of one port of a node: where wires attach, where its triangle and label are drawn and where it can be hit (from just inside the box edge out to the anchor).
/// </summary>
/// <param name="Port">The model port.</param>
/// <param name="Anchor">The point wires attach to, at the outer end of the triangle and label; identical to <see cref="Plexus.Port.Anchor"/>.</param>
/// <param name="Triangle">The rectangle of the port triangle, which points in the direction of the data flow (to the right).</param>
/// <param name="HitBounds">The region that counts as hitting the port.</param>
/// <param name="Label">The port label and its rectangle, outside the node box.</param>
public sealed record PortLayout(Port Port, Point Anchor, Rect Triangle, Rect HitBounds, LayoutText Label);
