using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using DemaConsulting.Plexus.Avalonia.Interaction;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Pointer input: press, drag, release, hover, wheel and the events raised to the host.
/// </content>
public sealed partial class DiagramEditor
{
    /// <summary>
    ///     How many device-independent pixels one wheel notch scrolls.
    /// </summary>
    private const double WheelScrollStep = 48;

    /// <summary>
    ///     The zoom factor applied per wheel notch while Ctrl is held.
    /// </summary>
    private const double WheelZoomFactor = 1.1;

    /// <summary>
    ///     Cursors created so far, by type (creating one per hover change would be wasteful).
    /// </summary>
    private readonly Dictionary<StandardCursorType, Cursor> _cursors = [];

    /// <summary>
    ///     The gesture in progress, or null.
    /// </summary>
    private EditorGesture? _gesture;

    /// <summary>
    ///     The pointer captured for the gesture in progress, so cancelling can release it.
    /// </summary>
    private IPointer? _gesturePointer;

    /// <summary>
    ///     Whether Space is held, which turns left-drag into panning.
    /// </summary>
    private bool _spaceDown;

    /// <summary>
    ///     The last pointer position relative to the control, or null when the pointer is outside.
    /// </summary>
    private AvaloniaPoint? _pointerPosition;

    /// <summary>
    ///     The element and position of a right-press awaiting its release before the context event is raised.
    /// </summary>
    private (DiagramElement? Element, AvaloniaPoint Screen)? _pendingContext;

    /// <summary>
    ///     Occurs when the body of a node is double-clicked. Double-clicking the node's name renames it instead.
    /// </summary>
    /// <remarks>
    ///     The editor selects nothing special and opens nothing: handle it to show a property dialog for <see cref="NodeDoubleClickedEventArgs.Node"/>.
    ///     The <c>DemaConsulting.Plexus.Avalonia.PropertyGrid</c> package provides a ready-made modal dialog (name, properties and host data, applied as one undo entry); a custom
    ///     window works the same way. Not raised
    ///     for annotations, groups or wires, and not when a double-click lands on a port, name or handle.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     using Avalonia.Controls;
    ///     using DemaConsulting.Plexus.Avalonia;
    ///     using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
    ///
    ///     // Inside a Window (this): double-click a node to edit it
    ///     public sealed class EditorWindow : Window
    ///     {
    ///         public EditorWindow(DiagramEditor editor)
    ///         {
    ///             Content = editor;
    ///             editor.NodeDoubleClicked += async (_, e) => await NodePropertiesDialog.ShowAsync(this, e.Node);
    ///         }
    ///     }
    ///     </code>
    /// </example>
    public event EventHandler<NodeDoubleClickedEventArgs>? NodeDoubleClicked;

    /// <summary>
    ///     Occurs when the user right-clicks an element or the empty canvas; the host shows its own menu.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Named <c>DiagramContextRequested</c> rather than <c>ContextRequested</c> because every
    ///         Avalonia control already has a <c>ContextRequested</c> event, which still works too: hosts
    ///         may assign a <c>ContextFlyout</c> to the editor. This event adds the diagram element and
    ///         the diagram position. It is raised when the right button is released; the element under the pointer has
    ///         already been selected (the selection is cleared for empty canvas).
    ///     </para>
    ///     <para>
    ///         To show a menu, create an Avalonia <c>MenuFlyout</c> and call <c>flyout.ShowAt(editor, true)</c>. With <c>showAtPointer</c> true Avalonia places the menu at the window's
    ///         last known pointer position (<c>TopLevel.LastPointerPosition</c>), not at a position taken from the event. Because this event is raised when the right button is released,
    ///         that is the release position, which equals the click position unless the pointer moved while the button was held. To place a menu at an explicit position, use an Avalonia
    ///         <c>ContextMenu</c> (a flyout has no placement rectangle) and anchor it at <see cref="DiagramContextRequestedEventArgs.ScreenPosition"/> (editor coordinates):
    ///         set <c>PlacementTarget</c> to the editor, <c>Placement</c> to <c>AnchorAndGravity</c>, <c>PlacementAnchor</c> to <c>TopLeft</c> and <c>PlacementGravity</c> to <c>BottomRight</c>,
    ///         <c>PlacementRect</c> to a 1 by 1 rectangle at that point, then call <c>menu.Open(editor)</c> (see <see cref="DiagramContextRequestedEventArgs"/> for the code).
    ///         <see cref="DiagramContextRequestedEventArgs.Position"/> (diagram coordinates) is what
    ///         you pass to <c>DiagramPage.AddNode</c> for an "Add here" entry. A complete handler is in the application example of the <see cref="DiagramEditor"/> type.
    ///     </para>
    /// </remarks>
    public event EventHandler<DiagramContextRequestedEventArgs>? DiagramContextRequested;

    /// <summary>
    ///     Handles a pointer press: left starts selecting, moving, connecting, reshaping or a double-click action; middle starts panning; right selects and prepares the context request. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        ArgumentNullException.ThrowIfNull(e);
        if (IsFromInlineEditor(e))
        {
            return;
        }

        // Clicking the canvas takes keyboard focus and ends any inline edit
        Focus();
        FinishEditOnOutsideInput();
        var point = e.GetCurrentPoint(this);
        _pointerPosition = point.Position;

        // Any press (drag, connection, double-click, context request) hides the tooltip of what it landed on
        SuppressToolTip(HitTest(point.Position));
        switch (point.Properties.PointerUpdateKind)
        {
            case PointerUpdateKind.MiddleButtonPressed:
                BeginGesture(e, new PanGesture(this, point.Position));
                break;
            case PointerUpdateKind.RightButtonPressed:
                OnRightPressed(point.Position);
                break;
            case PointerUpdateKind.LeftButtonPressed:
                OnLeftPressed(e, point.Position);
                break;
        }
    }

    /// <summary>
    ///     Handles pointer movement: drives the gesture in progress, otherwise updates the hover highlight and the cursor. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        ArgumentNullException.ThrowIfNull(e);
        if (IsFromInlineEditor(e))
        {
            return;
        }

        var screen = e.GetPosition(this);
        _pointerPosition = screen;

        // A running gesture owns the pointer; otherwise just track hover and the cursor
        if (_gesture is { } gesture)
        {
            gesture.Move(screen, e.KeyModifiers);
            SetCursor(gesture.Cursor);
            return;
        }

        UpdateHover(screen);
    }

    /// <summary>
    ///     Handles a pointer release: raises <see cref="DiagramContextRequested"/> after a right-click, otherwise completes the gesture in progress. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        ArgumentNullException.ThrowIfNull(e);
        if (IsFromInlineEditor(e))
        {
            return;
        }

        var screen = e.GetPosition(this);
        if (e.InitialPressMouseButton == MouseButton.Right && _pendingContext is { } pending)
        {
            // The context request is raised on release, like a native menu would be
            _pendingContext = null;
            DiagramContextRequested?.Invoke(this, new DiagramContextRequestedEventArgs(pending.Element, ScreenToDiagram(screen), screen));
            return;
        }

        // Detach the gesture before completing it, so completion can never re-enter itself
        if (_gesture is { } gesture)
        {
            _gesture = null;
            _gesturePointer = null;
            e.Pointer.Capture(null);
            gesture.Complete(screen, e.KeyModifiers);
            SetCursor(null);
            InvalidateVisual();
        }
    }

    /// <summary>
    ///     Cancels the gesture in progress when the pointer capture is lost (the window was deactivated, a popup opened), undoing what it already changed. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        // Losing capture mid-gesture (window deactivated, popup opened) abandons it cleanly
        CancelGesture();
    }

    /// <summary>
    ///     Clears the hover highlight (unless a gesture is still running) and hides the tooltip when the pointer leaves the editor. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ResetToolTip();
        _toolTipSuppressed = null;
        if (_gesture is null)
        {
            _pointerPosition = null;
            SetHover(null);
        }
    }

    /// <summary>
    ///     Handles the mouse wheel: plain wheel scrolls vertically, Shift+wheel horizontally, and Ctrl+wheel zooms around the cursor (10 percent per notch). Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ArgumentNullException.ThrowIfNull(e);
        if (IsFromInlineEditor(e))
        {
            return;
        }

        var screen = e.GetPosition(this);
        if (IsCommandModifier(e.KeyModifiers))
        {
            // Ctrl+wheel zooms around the cursor, each notch multiplying by a constant factor
            ZoomAt(screen, Zoom * Math.Pow(WheelZoomFactor, e.Delta.Y));
        }
        else
        {
            // Plain wheel scrolls; Shift turns the vertical wheel into a horizontal scroll
            var horizontal = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? e.Delta.Y : e.Delta.X;
            var vertical = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0 : e.Delta.Y;
            Offset = new AvaloniaPoint(
                Offset.X - (horizontal * WheelScrollStep / Zoom),
                Offset.Y - (vertical * WheelScrollStep / Zoom));
        }

        e.Handled = true;
    }

    /// <summary>
    ///     Cancels the running gesture, undoing whatever it already changed.
    /// </summary>
    internal void CancelGesture()
    {
        if (_gesture is { } gesture)
        {
            // Detach first: releasing capture raises PointerCaptureLost, which re-enters this method
            _gesture = null;
            ReleaseGesturePointer();
            gesture.Cancel();
            SetCursor(null);
            InvalidateVisual();
        }
    }

    /// <summary>
    ///     Releases the pointer capture taken by <see cref="BeginGesture"/>, if this control still holds it.
    /// </summary>
    private void ReleaseGesturePointer()
    {
        var pointer = _gesturePointer;
        _gesturePointer = null;
        if (pointer is not null && ReferenceEquals(pointer.Captured, this))
        {
            pointer.Capture(null);
        }
    }

    /// <summary>
    ///     Starts a gesture and captures the pointer so it keeps receiving movement outside the control.
    /// </summary>
    /// <param name="e">The press that starts the gesture.</param>
    /// <param name="gesture">The gesture.</param>
    private void BeginGesture(PointerPressedEventArgs e, EditorGesture gesture)
    {
        _gesture = gesture;
        _gesturePointer = e.Pointer;
        ResetToolTip();
        e.Pointer.Capture(this);
        SetCursor(gesture.Cursor);
        e.Handled = true;
    }

    /// <summary>
    ///     Handles a left press: double-clicks, then whatever the press landed on.
    /// </summary>
    /// <param name="e">The press.</param>
    /// <param name="screen">The press position relative to the control.</param>
    private void OnLeftPressed(PointerPressedEventArgs e, AvaloniaPoint screen)
    {
        // Space+drag pans regardless of what is underneath
        if (_spaceDown)
        {
            BeginGesture(e, new PanGesture(this, screen));
            return;
        }

        // A fast second click on a node is a double-click; on anything else (a group toggle clicked
        // twice in a row, a port, the canvas) it is just another click and is handled normally
        var hit = HitTest(screen);
        if (e.ClickCount >= 2 && OnDoubleClick(hit))
        {
            e.Handled = true;
            return;
        }

        var additive = IsAdditive(e.KeyModifiers);
        var editable = !IsReadOnly;
        if (editable && TryBeginEditGesture(e, screen, hit))
        {
            return;
        }

        switch (hit.Kind)
        {
            case HitKind.Wire when hit.Element is Wire wire:
                PressElement(e, screen, wire, additive, WireSegmentGesture.TryCreate(this, screen, wire, hit.SegmentIndex), editable);
                break;
            case HitKind.Canvas:
                if (!additive)
                {
                    ClearSelection();
                }

                BeginGesture(e, new RubberBandGesture(this, screen, additive));
                break;
            default:
                if (hit.Element is { } element)
                {
                    PressElement(e, screen, element, additive, null, editable);
                }

                break;
        }
    }

    /// <summary>
    ///     Starts the gesture of an editing hit (a port, a resize or wire handle, a group toggle) of an editable diagram.
    /// </summary>
    /// <param name="e">The press.</param>
    /// <param name="screen">The press position relative to the control.</param>
    /// <param name="hit">What the press landed on.</param>
    /// <returns><see langword="true"/> when the press was an editing hit and has been handled.</returns>
    private bool TryBeginEditGesture(PointerPressedEventArgs e, AvaloniaPoint screen, HitResult hit)
    {
        switch (hit.Kind)
        {
            case HitKind.Port when hit.Port is { } port:
                BeginGesture(e, new ConnectGesture(this, screen, port));
                return true;
            case HitKind.AnnotationResizeHandle when hit.Element is Annotation annotation:
                BeginGesture(e, new ResizeGesture(this, screen, annotation));
                return true;
            case HitKind.GroupCollapseToggle when hit.Element is Group group:
                // The toggle is a button: one undoable change, no drag
                SetSelection([group]);
                group.IsCollapsed = !group.IsCollapsed;
                e.Handled = true;
                return true;
            case HitKind.WireBend when hit.Element is Wire bendWire:
                // Handles exist only on a selected wire, so the selection needs no change; a bend the wire
                // cannot reshape (transient display route) is just ignored
                if (WireBendGesture.TryCreate(this, screen, bendWire, hit.BendIndex) is { } bendGesture)
                {
                    BeginGesture(e, bendGesture);
                }

                return true;
            case HitKind.WireSourceHandle or HitKind.WireTargetHandle when hit.Element is Wire endWire:
                BeginGesture(e, new WireEndGesture(this, screen, endWire, hit.Kind == HitKind.WireSourceHandle));
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///     Applies the selection rules for a press on an element and starts the matching drag gesture.
    /// </summary>
    /// <remarks>
    ///     An unselected element is selected at once (added to the selection with Shift/Ctrl). A press
    ///     on an already selected element leaves the selection alone so a multi-element drag can
    ///     start; narrowing or toggling is deferred to the release, where the gesture knows it was a click.
    /// </remarks>
    /// <param name="e">The press.</param>
    /// <param name="screen">The press position relative to the control.</param>
    /// <param name="element">The element that was pressed.</param>
    /// <param name="additive">Whether Shift or Ctrl is held.</param>
    /// <param name="special">A specialized gesture to use instead of the plain move (wire segments), or null.</param>
    /// <param name="editable">Whether editing gestures may start.</param>
    private void PressElement(PointerPressedEventArgs e, AvaloniaPoint screen, DiagramElement element, bool additive, EditorGesture? special, bool editable)
    {
        var before = _selection.ToList();
        var wasSelected = IsSelected(element);
        if (!wasSelected)
        {
            SetSelection(additive ? before.Append(element) : [element]);
        }

        if (!editable)
        {
            return;
        }

        // Wires with no movable segment simply select; everything else drags
        var gesture = special ?? (element is Wire ? null : new MoveGesture(this, screen, element, wasSelected, before));
        if (gesture is not null)
        {
            BeginGesture(e, gesture);
        }
    }

    /// <summary>
    ///     Handles a right press: makes the element under the pointer the selection and remembers it for the release.
    /// </summary>
    /// <param name="screen">The press position relative to the control.</param>
    private void OnRightPressed(AvaloniaPoint screen)
    {
        // Right-clicking an unselected element selects it first, as every desktop app does
        var element = HitTest(screen).Element;
        if (element is not null && !IsSelected(element))
        {
            SetSelection([element]);
        }
        else if (element is null)
        {
            ClearSelection();
        }

        _pendingContext = (element, screen);
    }

    /// <summary>
    ///     Handles the second press of a double-click.
    /// </summary>
    /// <param name="hit">What the press landed on.</param>
    /// <returns><see langword="true"/> when the double-click meant something (a node or its name) and is consumed.</returns>
    private bool OnDoubleClick(HitResult hit)
    {
        // A name starts an inline rename; the rest of the node is reported to the host
        if (hit.Kind == HitKind.NodeName && hit.Element is Node named)
        {
            BeginRename(named);
            return true;
        }

        // An annotation is edited in place, unless the editor is read-only (then it is just a click)
        if (hit.Kind == HitKind.Annotation && hit.Element is Annotation note && !IsReadOnly)
        {
            BeginEditAnnotation(note);
            return true;
        }

        if (hit.Kind == HitKind.Node && hit.Element is Node node)
        {
            NodeDoubleClicked?.Invoke(this, new NodeDoubleClickedEventArgs(node));
            return true;
        }

        // A wire segment gets a draggable jog; the wire is selected so its handles show up
        if (hit.Kind == HitKind.Wire && hit.Element is Wire wire && !IsReadOnly && LivePage() is not null)
        {
            return AddWireJog(wire, hit.SegmentIndex);
        }

        return false;
    }

    /// <summary>
    ///     Adds a jog to the wire segment under the pointer.
    /// </summary>
    /// <param name="wire">The wire that was double-clicked.</param>
    /// <param name="segmentIndex">The segment that was hit, as an index into the displayed polyline.</param>
    /// <returns><see langword="true"/> when a jog was added; <see langword="false"/> when the wire cannot take one.</returns>
    private bool AddWireJog(Wire wire, int segmentIndex)
    {
        // Only a wire drawn from its stored route can be edited (not a transient route to a collapsed group)
        if (Scene is not { } scene || !scene.TryGetWirePoints(wire, out var shown) || shown.Count != wire.Route.Count + 2)
        {
            return false;
        }

        var near = _pointerPosition is { } pointer ? ToDiagram(pointer) : default;
        SetSelection([wire]);
        return wire.AddJog(segmentIndex, new DemaConsulting.Plexus.Geometry.Point(Snap(near.X), Snap(near.Y)));
    }

    /// <summary>
    ///     Updates the hover highlight, the tooltip and the cursor for a pointer that is not dragging.
    /// </summary>
    /// <param name="screen">The pointer position relative to the control.</param>
    private void UpdateHover(AvaloniaPoint screen)
    {
        if (_spaceDown)
        {
            SetCursor(StandardCursorType.Hand);
            ResetToolTip();
            return;
        }

        var hit = HitTest(screen);
        SetHover(hit.Element);
        UpdateToolTip(hit);

        // Hint at what a press would do: ports connect, handles resize, wire handles and segments reshape
        var cursor = hit.Kind switch
        {
            HitKind.Port when !IsReadOnly => StandardCursorType.Cross,
            HitKind.AnnotationResizeHandle when !IsReadOnly => StandardCursorType.BottomRightCorner,
            HitKind.GroupCollapseToggle when !IsReadOnly => StandardCursorType.Hand,
            HitKind.WireBend when !IsReadOnly => StandardCursorType.SizeAll,
            HitKind.WireSourceHandle or HitKind.WireTargetHandle when !IsReadOnly => StandardCursorType.Cross,
            HitKind.Wire when !IsReadOnly => WireSegmentCursor(hit),
            _ => null,
        };
        SetCursor(cursor);
    }

    /// <summary>
    ///     Chooses the resize cursor for a wire segment: vertical movement for horizontal segments and the reverse.
    /// </summary>
    /// <param name="hit">The wire hit.</param>
    /// <returns>The cursor, or null when the segment cannot be dragged (diagonal, or a wire drawn to a collapsed group).</returns>
    private StandardCursorType? WireSegmentCursor(HitResult hit)
    {
        if (hit.Element is not Wire wire || Scene is not { } scene || !scene.TryGetWirePoints(wire, out var shown)
            || shown.Count != wire.Route.Count + 2 || hit.GetWireSegment(shown) is not { } segment)
        {
            return null;
        }

        var horizontal = Math.Abs(segment.Start.Y - segment.End.Y) < 1e-6;
        var vertical = Math.Abs(segment.Start.X - segment.End.X) < 1e-6;
        if (horizontal == vertical)
        {
            return null;
        }

        return horizontal ? StandardCursorType.SizeNorthSouth : StandardCursorType.SizeWestEast;
    }

    /// <summary>
    ///     Shows a standard cursor, or the default arrow for null.
    /// </summary>
    /// <param name="type">The cursor type, or null for the default.</param>
    private void SetCursor(StandardCursorType? type)
    {
        if (type is not { } kind)
        {
            Cursor = null;
            return;
        }

        if (!_cursors.TryGetValue(kind, out var cursor))
        {
            cursor = new Cursor(kind);
            _cursors[kind] = cursor;
        }

        Cursor = cursor;
    }

    /// <summary>
    ///     Determines whether Ctrl (or Cmd on macOS) is part of the modifiers.
    /// </summary>
    /// <param name="modifiers">The modifiers.</param>
    /// <returns><see langword="true"/> when a command modifier is held.</returns>
    private static bool IsCommandModifier(KeyModifiers modifiers) =>
        (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;

    /// <summary>
    ///     Determines whether the modifiers ask to add to the selection (Shift or Ctrl/Cmd).
    /// </summary>
    /// <param name="modifiers">The modifiers.</param>
    /// <returns><see langword="true"/> when the selection should be extended rather than replaced.</returns>
    private static bool IsAdditive(KeyModifiers modifiers) =>
        IsCommandModifier(modifiers) || modifiers.HasFlag(KeyModifiers.Shift);
}
