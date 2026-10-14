using Avalonia.Input;
using AvaloniaPoint = Avalonia.Point;

namespace DemaConsulting.Plexus.Avalonia.Interaction;

/// <summary>
///     Scrolls the view while the pointer is dragged (middle button, or Space plus left button).
/// </summary>
/// <remarks>
///     The gesture keeps the diagram point that was under the pointer at the press under the pointer
///     for the whole drag, which feels like grabbing the canvas. It edits no model state.
/// </remarks>
internal sealed class PanGesture : EditorGesture
{
    /// <summary>
    ///     The view offset when the drag began.
    /// </summary>
    private readonly AvaloniaPoint _startOffset;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PanGesture"/> class.
    /// </summary>
    /// <param name="editor">The editor to scroll.</param>
    /// <param name="startScreen">The control-relative position of the press.</param>
    public PanGesture(DiagramEditor editor, AvaloniaPoint startScreen)
        : base(editor, startScreen)
    {
        _startOffset = editor.Offset;
    }

    /// <inheritdoc />
    public override StandardCursorType? Cursor => StandardCursorType.SizeAll;

    /// <inheritdoc />
    public override void Move(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        // Screen movement divided by the zoom is diagram movement; the content follows the pointer, so subtract
        Editor.Offset = new AvaloniaPoint(
            _startOffset.X - ((screen.X - StartScreen.X) / Editor.Zoom),
            _startOffset.Y - ((screen.Y - StartScreen.Y) / Editor.Zoom));
    }
}
