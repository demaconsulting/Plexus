using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Adding a node of a given type in the middle of the visible canvas (what a palette activation does).
/// </content>
public sealed partial class DiagramEditor
{
    /// <summary>
    ///     Adds a node of a type at the center of the visible canvas, selects it and returns it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The node is centered on the middle of the view and snapped like any other placement
    ///         (see <see cref="SnapToGrid"/>). When a node already sits exactly there, the new one is shifted
    ///         diagonally by one grid cell until its place is free, so repeated calls leave every node visible.
    ///         The add is one undo entry. This is what <see cref="NodePalette"/> does when a type is
    ///         activated and the palette follows this editor, and it is public so hosts can offer the
    ///         same from a menu: <c>editor.AddNodeAtCenter(diagram.Types.Get("demo.math.Add"))</c>.
    ///     </para>
    ///     <para>
    ///         Nothing is added, and null is returned, when the editor is read-only, shows no page, or the
    ///         page's diagram has no node type with the key of <paramref name="type"/>.
    ///     </para>
    /// </remarks>
    /// <param name="type">The node type to create; the diagram's own type with the same key is the one used.</param>
    /// <returns>The new node, or null when nothing was added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    public Node? AddNodeAtCenter(NodeType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (IsReadOnly || LivePage() is not { } page || !page.Diagram.Types.TryGet(type.Key, out var registered) || registered is null)
        {
            return null;
        }

        // Center on the middle of the view, then walk diagonally past nodes that already occupy the spot
        var position = DropPosition(registered, page, new AvaloniaPoint(Bounds.Width / 2, Bounds.Height / 2));
        var step = Math.Max(EffectiveTheme.GridSize, 1);
        while (page.Nodes.Any(node => node.Position == position))
        {
            position = new PlexusPoint(position.X + step, position.Y + step);
        }

        var added = page.AddNode(registered, position);
        SetSelection([added]);
        return added;
    }
}
