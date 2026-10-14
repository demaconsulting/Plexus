using Avalonia.Input;
using AvaloniaPoint = Avalonia.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia.Interaction;

/// <summary>
///     Resizes an annotation by dragging its corner handle.
/// </summary>
/// <remarks>
///     The top-left corner stays put; the bottom-right corner follows the pointer, snapped to the
///     grid, and the box never shrinks below a minimum size. Bounds are updated live inside one
///     history transaction, so the whole drag is a single undo entry.
/// </remarks>
internal sealed class ResizeGesture : EditorGesture
{
    /// <summary>
    ///     The smallest width an annotation can be resized to.
    /// </summary>
    private const double MinWidth = 30;

    /// <summary>
    ///     The smallest height an annotation can be resized to.
    /// </summary>
    private const double MinHeight = 20;

    /// <summary>
    ///     The annotation being resized.
    /// </summary>
    private readonly Annotation _annotation;

    /// <summary>
    ///     The bounds when the drag began.
    /// </summary>
    private readonly PlexusRect _startBounds;

    /// <summary>
    ///     The open transaction, or null until the drag started.
    /// </summary>
    private HistoryTransaction? _transaction;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ResizeGesture"/> class.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="startScreen">The control-relative position of the press.</param>
    /// <param name="annotation">The annotation whose handle was pressed.</param>
    public ResizeGesture(DiagramEditor editor, AvaloniaPoint startScreen, Annotation annotation)
        : base(editor, startScreen)
    {
        _annotation = annotation;
        _startBounds = annotation.Bounds;
    }

    /// <inheritdoc />
    public override StandardCursorType? Cursor => StandardCursorType.BottomRightCorner;

    /// <inheritdoc />
    public override void Move(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        if (_transaction is null)
        {
            if (DistanceFromStart(screen) < DragThreshold || Editor.LivePage() is not { } page)
            {
                return;
            }

            _transaction = page.Diagram.History.BeginTransaction("Resize annotation");
        }

        // The pointer sets the bottom-right corner; clamp so the note stays usable
        var pointer = Editor.ToDiagram(screen);
        var width = Math.Max(MinWidth, Editor.Snap(pointer.X) - _startBounds.Left);
        var height = Math.Max(MinHeight, Editor.Snap(pointer.Y) - _startBounds.Top);
        var bounds = new PlexusRect(_startBounds.Left, _startBounds.Top, width, height);
        if (bounds != _annotation.Bounds)
        {
            _annotation.Bounds = bounds;
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
}
