using AvaloniaPoint = Avalonia.Point;
using PlexusPage = DemaConsulting.Plexus.DiagramPage;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Commands on the selection and the view that every host menu needs: group, ungroup, collapse, move to page, zoom and annotations.
/// </content>
public sealed partial class DiagramEditor
{
    /// <summary>
    ///     The factor one <see cref="ZoomIn"/> or <see cref="ZoomOut"/> step multiplies or divides the zoom by.
    /// </summary>
    public const double ZoomStep = 1.25;

    /// <summary>
    ///     The width of an annotation created by <see cref="AddAnnotationAtCenter"/>, in diagram units.
    /// </summary>
    private const double NewAnnotationWidth = 150;

    /// <summary>
    ///     The height of an annotation created by <see cref="AddAnnotationAtCenter"/>, in diagram units.
    /// </summary>
    private const double NewAnnotationHeight = 60;

    /// <summary>
    ///     Gets the diagram point at the middle of the visible canvas.
    /// </summary>
    /// <remarks>
    ///     It follows zoom and scrolling, so it is where "add here" commands from a menu should put new content.
    ///     Coordinates are in Plexus diagram units (not snapped; see <see cref="SnapPoint"/>).
    /// </remarks>
    public PlexusPoint ViewCenter => ToDiagram(new AvaloniaPoint(Bounds.Width / 2, Bounds.Height / 2));

    /// <summary>
    ///     Zooms in by one step (<see cref="ZoomStep"/>) around the middle of the canvas.
    /// </summary>
    /// <remarks>
    ///     The zoom stays within <see cref="MinZoom"/> and <see cref="MaxZoom"/>.
    /// </remarks>
    public void ZoomIn() => ZoomAt(new AvaloniaPoint(Bounds.Width / 2, Bounds.Height / 2), Zoom * ZoomStep);

    /// <summary>
    ///     Zooms out by one step (<see cref="ZoomStep"/>) around the middle of the canvas.
    /// </summary>
    /// <remarks>
    ///     The zoom stays within <see cref="MinZoom"/> and <see cref="MaxZoom"/>.
    /// </remarks>
    public void ZoomOut() => ZoomAt(new AvaloniaPoint(Bounds.Width / 2, Bounds.Height / 2), Zoom / ZoomStep);

    /// <summary>
    ///     Rounds a diagram point to the grid when <see cref="SnapToGrid"/> is on; otherwise returns it unchanged.
    /// </summary>
    /// <remarks>
    ///     Use it for "Add node here" from a context menu: <c>editor.SnapPoint(e.Position)</c>. (The name differs from
    ///     <see cref="SnapToGrid"/>, which is the on/off property.)
    /// </remarks>
    /// <param name="point">The point in diagram coordinates.</param>
    /// <returns>The snapped point.</returns>
    public PlexusPoint SnapPoint(PlexusPoint point) => new(Snap(point.X), Snap(point.Y));

    /// <summary>
    ///     Groups the selected nodes, annotations and groups under a title and selects the new group (one undo entry).
    /// </summary>
    /// <remarks>
    ///     Selected wires are ignored (a wire cannot be a group member). Nothing happens, and null is returned, when the
    ///     editor is read-only, shows no page, or nothing that can be grouped is selected.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     var group = editor.GroupSelection("Input stage");     // null when nothing that can be grouped is selected
    ///     if (group is null) { statusBar.Text = "Select the nodes to group first."; }
    ///     </code>
    /// </example>
    /// <param name="title">The title shown in the group's title bar.</param>
    /// <returns>The new group, or null when nothing was grouped.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is null.</exception>
    public Group? GroupSelection(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (IsReadOnly || LivePage() is not { } page)
        {
            return null;
        }

        var members = _selection.Where(e => e is not Wire).ToList();
        if (members.Count == 0)
        {
            return null;
        }

        var group = page.CreateGroup(members, title);
        SetSelection([group]);
        return group;
    }

    /// <summary>
    ///     Dissolves the selected groups, keeping their members (one undo entry).
    /// </summary>
    /// <remarks>
    ///     Only groups in the selection are dissolved; a selected member does not stand for its group here (use the group's frame).
    ///     Does nothing when the editor is read-only, shows no page, or no group is selected.
    /// </remarks>
    /// <returns>How many groups were dissolved.</returns>
    public int UngroupSelection()
    {
        if (IsReadOnly || LivePage() is not { } page)
        {
            return 0;
        }

        var groups = _selection.OfType<Group>().ToList();
        if (groups.Count == 0)
        {
            return 0;
        }

        page.Diagram.History.Transact("Ungroup", () => groups.ForEach(page.Ungroup));
        ClearSelection();
        return groups.Count;
    }

    /// <summary>
    ///     Collapses or expands the selected groups, or the groups that contain the selected elements (one undo entry).
    /// </summary>
    /// <remarks>
    ///     A selected member stands for its group, so users do not have to select the frame. Each group flips on its own:
    ///     collapsed groups expand and expanded ones collapse. Does nothing when the editor is read-only or no group is involved.
    /// </remarks>
    /// <returns>How many groups changed.</returns>
    public int ToggleCollapseSelection()
    {
        if (IsReadOnly || LivePage() is null)
        {
            return 0;
        }

        var groups = _selection.Select(e => e as Group ?? e.Parent).OfType<Group>().Distinct().ToList();
        if (groups.Count == 0)
        {
            return 0;
        }

        Page!.Diagram.History.Transact("Toggle collapse", () => groups.ForEach(g => g.IsCollapsed = !g.IsCollapsed));
        return groups.Count;
    }

    /// <summary>
    ///     Moves the selected elements to another page of the same diagram (one undo entry) and clears the selection.
    /// </summary>
    /// <remarks>
    ///     The editor keeps showing the current page; set <see cref="Page"/> to the target to follow the elements. Nothing happens
    ///     when the editor is read-only, the selection is empty, or the target is the shown page or not a page of this diagram.
    ///     See <see cref="Diagram.MoveToPage"/> for how wires and groups are treated.
    /// </remarks>
    /// <param name="target">The destination page.</param>
    /// <returns><see langword="true"/> when elements were moved.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>
    public bool MoveSelectionToPage(PlexusPage target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (IsReadOnly || LivePage() is not { } page || _selection.Count == 0
            || ReferenceEquals(target, page) || !target.IsAttached || !ReferenceEquals(target.Diagram, page.Diagram))
        {
            return false;
        }

        // Copy first: moving removes the elements from the page, which prunes the live selection
        page.Diagram.MoveToPage([.. _selection], target);
        ClearSelection();
        return true;
    }

    /// <summary>
    ///     Adds an annotation centered on the visible canvas (snapped to the grid), selects it and returns it (one undo entry).
    /// </summary>
    /// <remarks>
    ///     The annotation is 150 by 60 diagram units; resize it afterwards or use <see cref="BeginEditAnnotation"/> to type its text.
    ///     Nothing is added, and null is returned, when the editor is read-only or shows no page.
    /// </remarks>
    /// <param name="text">The annotation text.</param>
    /// <returns>The new annotation, or null when nothing was added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    public Annotation? AddAnnotationAtCenter(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (IsReadOnly || LivePage() is not { } page)
        {
            return null;
        }

        var center = ViewCenter;
        var bounds = new PlexusRect(
            Snap(center.X - (NewAnnotationWidth / 2)),
            Snap(center.Y - (NewAnnotationHeight / 2)),
            NewAnnotationWidth,
            NewAnnotationHeight);
        var annotation = page.AddAnnotation(text, bounds);
        SetSelection([annotation]);
        return annotation;
    }
}
