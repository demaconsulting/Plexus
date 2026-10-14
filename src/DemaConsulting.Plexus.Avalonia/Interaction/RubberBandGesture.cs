using Avalonia.Input;
using Avalonia.Media;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia.Interaction;

/// <summary>
///     Selects every element touched by a rectangle dragged on the empty canvas.
/// </summary>
/// <remarks>
///     Nodes, annotations and collapsed groups are selected when the rectangle touches them;
///     expanded groups only when the rectangle swallows them whole (otherwise any drag inside a
///     group would grab the group). Wires are not rubber-banded: they travel with their nodes
///     through copy and duplicate, and a wire can be clicked when it needs to be selected alone.
///     With Shift or Ctrl held at the press the previous selection is kept and added to. The
///     selection updates live while dragging, so the highlight shows what will be selected.
/// </remarks>
internal sealed class RubberBandGesture : EditorGesture
{
    /// <summary>
    ///     The selection to keep underneath the band (empty unless the band is additive).
    /// </summary>
    private readonly List<DiagramElement> _baseSelection;

    /// <summary>
    ///     The diagram position of the press, the fixed corner of the band.
    /// </summary>
    private readonly PlexusPoint _startDiagram;

    /// <summary>
    ///     The current band, or null until the drag threshold is passed.
    /// </summary>
    private PlexusRect? _band;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RubberBandGesture"/> class.
    /// </summary>
    /// <param name="editor">The editor whose selection is changed.</param>
    /// <param name="startScreen">The control-relative position of the press.</param>
    /// <param name="additive">Whether the band adds to the current selection instead of replacing it.</param>
    public RubberBandGesture(DiagramEditor editor, AvaloniaPoint startScreen, bool additive)
        : base(editor, startScreen)
    {
        _startDiagram = editor.ToDiagram(startScreen);
        _baseSelection = additive ? [.. editor.Selection] : [];
    }

    /// <inheritdoc />
    public override void Move(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        // A tiny wobble during a plain click must not turn into a band
        if (_band is null && DistanceFromStart(screen) < DragThreshold)
        {
            return;
        }

        var band = PlexusRect.FromPoints(_startDiagram, Editor.ToDiagram(screen));
        _band = band;

        // Rebuild the selection from scratch each time so shrinking the band deselects again
        if (Editor.LivePage() is { } page)
        {
            Editor.SetSelection(_baseSelection.Concat(page.Elements.Where(e => IsCaught(e, band))));
        }

        Editor.InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Complete(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        // The selection was kept current during the drag; only the band outline needs to go
        _band = null;
        Editor.InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        _band = null;
        Editor.InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Draw(DrawingContext context, ScenePainter painter, DiagramTheme theme, double zoom)
    {
        if (_band is not { } band)
        {
            return;
        }

        // A translucent fill with a hairline outline reads as "area", and stays one pixel wide at any zoom
        var fill = theme.SelectionColor with { A = 40 };
        context.DrawRectangle(painter.GetBrush(fill), painter.GetPen(theme.SelectionColor, 1 / zoom), band.ToAvalonia());
    }

    /// <summary>
    ///     Decides whether the band selects an element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="band">The band in diagram coordinates.</param>
    /// <returns><see langword="true"/> when the element is visible and caught by the band.</returns>
    private static bool IsCaught(DiagramElement element, PlexusRect band)
    {
        if (!element.IsVisible)
        {
            return false;
        }

        return element switch
        {
            Node node => band.Intersects(node.Bounds),
            Annotation annotation => band.Intersects(annotation.Bounds),
            Group { IsCollapsed: true } group => band.Intersects(group.Bounds),
            Group group => band.Contains(group.Bounds),
            _ => false,
        };
    }
}
