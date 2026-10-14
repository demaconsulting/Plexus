using Avalonia.Input;
using DemaConsulting.Plexus.Routing;
using AvaloniaPoint = Avalonia.Point;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia.Interaction;

/// <summary>
///     Drags one segment of a wire perpendicular to itself, keeping the wire orthogonal.
/// </summary>
/// <remarks>
///     A horizontal segment moves vertically and a vertical one horizontally; its two neighbors are
///     perpendicular, so they just stretch and the wire stays orthogonal. The first and last
///     segments (the stubs at the ports) can be dragged too: the ports stay where they are and a
///     short jog is inserted next to the port, so the wire still leaves and enters straight (see
///     <see cref="RouteEditor.MoveSegment"/>). The new position snaps to the grid.
/// </remarks>
internal sealed class WireSegmentGesture : WireReshapeGesture
{
    /// <summary>
    ///     The tolerance when deciding whether a segment is horizontal or vertical.
    /// </summary>
    private const double Epsilon = 1e-6;

    /// <summary>
    ///     The index of the dragged segment within the full polyline.
    /// </summary>
    private readonly int _segmentIndex;

    /// <summary>
    ///     Whether the segment is horizontal (and therefore moves along Y).
    /// </summary>
    private readonly bool _horizontal;

    /// <summary>
    ///     The coordinate (Y for horizontal, X for vertical) of the segment at the press.
    /// </summary>
    private readonly double _startCoordinate;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WireSegmentGesture"/> class.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="startScreen">The control-relative position of the press.</param>
    /// <param name="wire">The wire.</param>
    /// <param name="segmentIndex">The index of the segment within the full polyline.</param>
    /// <param name="horizontal">Whether the segment is horizontal.</param>
    private WireSegmentGesture(DiagramEditor editor, AvaloniaPoint startScreen, Wire wire, int segmentIndex, bool horizontal)
        : base(editor, startScreen, wire, "Move wire segment")
    {
        _segmentIndex = segmentIndex;
        _horizontal = horizontal;
        var points = wire.GetPoints();
        _startCoordinate = horizontal ? points[segmentIndex].Y : points[segmentIndex].X;
    }

    /// <inheritdoc />
    public override StandardCursorType? Cursor => _horizontal ? StandardCursorType.SizeNorthSouth : StandardCursorType.SizeWestEast;

    /// <summary>
    ///     Creates the gesture for a segment, if that segment can be moved.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="startScreen">The control-relative position of the press.</param>
    /// <param name="wire">The wire that was hit.</param>
    /// <param name="segmentIndex">The hit segment's index within the displayed polyline (anchor, bends, anchor).</param>
    /// <returns>The gesture, or null when the segment is diagonal or the wire is drawn differently from its stored route.</returns>
    public static WireSegmentGesture? TryCreate(DiagramEditor editor, AvaloniaPoint startScreen, Wire wire, int segmentIndex)
    {
        // A wire shown with a transient display route (collapsed group) has no editable stored shape
        var points = wire.GetPoints();
        if (!IsStoredRoute(editor, wire) || segmentIndex < 0 || segmentIndex > points.Count - 2)
        {
            return null;
        }

        var a = points[segmentIndex];
        var b = points[segmentIndex + 1];
        var horizontal = Math.Abs(a.Y - b.Y) < Epsilon;
        var vertical = Math.Abs(a.X - b.X) < Epsilon;

        // Exactly one must hold: zero-length and diagonal segments have no unambiguous perpendicular
        return horizontal == vertical ? null : new WireSegmentGesture(editor, startScreen, wire, segmentIndex, horizontal);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<PlexusPoint>? Reshape(PlexusPoint pointer)
    {
        // Only the component perpendicular to the segment matters
        var coordinate = Editor.Snap(_startCoordinate + (_horizontal ? pointer.Y - StartDiagram.Y : pointer.X - StartDiagram.X));
        return RouteEditor.MoveSegment(StartAnchor, StartRoute, EndAnchor, _segmentIndex, coordinate, StubLength);
    }
}
