using Avalonia.Input;
using AvaloniaPoint = Avalonia.Point;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia.Interaction;

/// <summary>
///     Base of the gestures that reshape a selected wire's route: the shared drag threshold, transaction and live update.
/// </summary>
/// <remarks>
///     Nothing is edited until the pointer travels past the drag threshold, so a plain click never
///     creates an undo entry. From then on every move recomputes the route from the route at the start
///     of the drag (not from the previous move, so inserted jogs and cleaned-up bends cannot drift)
///     and stores it through <see cref="Wire.SetRoute"/> inside one history transaction: the whole drag
///     is a single undo step and Escape rolls it back completely. The route geometry itself lives in
///     <see cref="Routing.RouteEditor"/>; derived gestures only say how the pointer maps onto it.
/// </remarks>
internal abstract class WireReshapeGesture : EditorGesture
{
    /// <summary>
    ///     The undo description of the whole drag.
    /// </summary>
    private readonly string _description;

    /// <summary>
    ///     The open transaction, or null until the drag started.
    /// </summary>
    private HistoryTransaction? _transaction;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WireReshapeGesture"/> class.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="startScreen">The control-relative position of the press.</param>
    /// <param name="wire">The wire being reshaped.</param>
    /// <param name="description">The undo description.</param>
    protected WireReshapeGesture(DiagramEditor editor, AvaloniaPoint startScreen, Wire wire, string description)
        : base(editor, startScreen)
    {
        Wire = wire;
        _description = description;
        StartRoute = [.. wire.Route];
        StartAnchor = wire.Source.Anchor;
        EndAnchor = wire.Target.Anchor;
        StartDiagram = editor.ToDiagram(startScreen);
        StubLength = editor.EffectiveTheme.WireStubLength;
    }

    /// <summary>
    ///     Gets the wire being reshaped.
    /// </summary>
    protected Wire Wire { get; }

    /// <summary>
    ///     Gets the route as it was when the drag began.
    /// </summary>
    protected PlexusPoint[] StartRoute { get; }

    /// <summary>
    ///     Gets the source anchor when the drag began.
    /// </summary>
    protected PlexusPoint StartAnchor { get; }

    /// <summary>
    ///     Gets the target anchor when the drag began.
    /// </summary>
    protected PlexusPoint EndAnchor { get; }

    /// <summary>
    ///     Gets the diagram position of the press.
    /// </summary>
    protected PlexusPoint StartDiagram { get; }

    /// <summary>
    ///     Gets the length of the straight stub kept at a port.
    /// </summary>
    protected double StubLength { get; }

    /// <inheritdoc />
    public override void Move(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        if (_transaction is null)
        {
            if (DistanceFromStart(screen) < DragThreshold || Editor.LivePage() is not { } page)
            {
                return;
            }

            _transaction = page.Diagram.History.BeginTransaction(_description);
        }

        // A pointer position the route cannot follow (for example inside a port's stub) leaves the last good shape
        var route = Reshape(Editor.ToDiagram(screen));
        if (route is not null)
        {
            Wire.SetRoute(route);
        }
    }

    /// <inheritdoc />
    public override void Complete(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        _transaction?.Dispose();
        _transaction = null;
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        _transaction?.Rollback();
        _transaction = null;
    }

    /// <summary>
    ///     Computes the route for a pointer position from the route at the start of the drag.
    /// </summary>
    /// <param name="pointer">The pointer in diagram coordinates.</param>
    /// <returns>The new interior bend points, or null when the pointer position has no valid route.</returns>
    protected abstract IReadOnlyList<PlexusPoint>? Reshape(PlexusPoint pointer);

    /// <summary>
    ///     Checks that a wire is shown with its stored route, which is the only shape that can be edited.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="wire">The wire.</param>
    /// <returns><see langword="true"/> when the wire is drawn from its stored route (not a transient route to a collapsed group).</returns>
    protected static bool IsStoredRoute(DiagramEditor editor, Wire wire) =>
        editor.Scene is { } scene
        && scene.TryGetWirePoints(wire, out var shown)
        && shown.Count == wire.Route.Count + 2;
}
