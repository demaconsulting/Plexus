using Avalonia.Input;
using DemaConsulting.Plexus.Routing;
using AvaloniaPoint = Avalonia.Point;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia.Interaction;

/// <summary>
///     Drags one bend of a selected wire; the two segments that meet there follow, so the wire stays orthogonal.
/// </summary>
/// <remarks>
///     The bend moves to the pointer (snapped to the grid): the segment on one side takes the new Y,
///     the one on the other side the new X, and whatever follows them stretches (see
///     <see cref="RouteEditor.MoveBend"/>). A bend next to a port stub gets a jog inserted instead of
///     moving the port. Bends that end up collinear with their neighbors are merged by
///     <see cref="Wire.SetRoute"/>, so a dragged bend can be flattened away.
/// </remarks>
internal sealed class WireBendGesture : WireReshapeGesture
{
    /// <summary>
    ///     The index of the dragged bend within the wire's route.
    /// </summary>
    private readonly int _bendIndex;

    /// <summary>
    ///     The position of the bend at the press.
    /// </summary>
    private readonly PlexusPoint _startBend;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WireBendGesture"/> class.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="startScreen">The control-relative position of the press.</param>
    /// <param name="wire">The wire.</param>
    /// <param name="bendIndex">The index of the bend within the wire's route.</param>
    private WireBendGesture(DiagramEditor editor, AvaloniaPoint startScreen, Wire wire, int bendIndex)
        : base(editor, startScreen, wire, "Move wire bend")
    {
        _bendIndex = bendIndex;
        _startBend = wire.Route[bendIndex];
    }

    /// <inheritdoc />
    public override StandardCursorType? Cursor => StandardCursorType.SizeAll;

    /// <summary>
    ///     Creates the gesture for a bend, if the wire can be edited.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="startScreen">The control-relative position of the press.</param>
    /// <param name="wire">The wire that was hit.</param>
    /// <param name="bendIndex">The bend's index within the wire's route.</param>
    /// <returns>The gesture, or null when the bend does not exist or the wire is drawn differently from its stored route.</returns>
    public static WireBendGesture? TryCreate(DiagramEditor editor, AvaloniaPoint startScreen, Wire wire, int bendIndex) =>
        IsStoredRoute(editor, wire) && bendIndex >= 0 && bendIndex < wire.Route.Count
            ? new WireBendGesture(editor, startScreen, wire, bendIndex)
            : null;

    /// <inheritdoc />
    protected override IReadOnlyList<PlexusPoint>? Reshape(PlexusPoint pointer)
    {
        var target = new PlexusPoint(
            Editor.Snap(_startBend.X + (pointer.X - StartDiagram.X)),
            Editor.Snap(_startBend.Y + (pointer.Y - StartDiagram.Y)));
        return RouteEditor.MoveBend(StartAnchor, StartRoute, EndAnchor, _bendIndex, target, StubLength);
    }
}
