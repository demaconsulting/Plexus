using Avalonia.Input;
using Avalonia.Media;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;

namespace DemaConsulting.Plexus.Avalonia.Interaction;

/// <summary>
///     One pointer interaction in progress: from the press that started it until the release or cancellation that ends it.
/// </summary>
/// <remarks>
///     The editor runs at most one gesture at a time and forwards pointer movement to it. A gesture
///     that edits the model does so inside a single history transaction, which is how a whole drag
///     becomes one undo entry and how <see cref="Cancel"/> (Escape) can roll it back completely.
///     Gestures draw their own transient feedback (rubber band, connection preview) in diagram
///     coordinates through <see cref="Draw"/>.
/// </remarks>
internal abstract class EditorGesture
{
    /// <summary>
    ///     How far, in device-independent pixels, the pointer must travel before a press becomes a drag.
    /// </summary>
    protected const double DragThreshold = 3;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EditorGesture"/> class.
    /// </summary>
    /// <param name="editor">The editor the gesture works on.</param>
    /// <param name="startScreen">The control-relative position of the initial press.</param>
    protected EditorGesture(DiagramEditor editor, AvaloniaPoint startScreen)
    {
        Editor = editor;
        StartScreen = startScreen;
    }

    /// <summary>
    ///     Gets the cursor to show while the gesture runs, or null for the default arrow.
    /// </summary>
    public virtual StandardCursorType? Cursor => null;

    /// <summary>
    ///     Gets the editor the gesture works on.
    /// </summary>
    protected DiagramEditor Editor { get; }

    /// <summary>
    ///     Gets the control-relative position of the initial press.
    /// </summary>
    protected AvaloniaPoint StartScreen { get; }

    /// <summary>
    ///     Handles pointer movement.
    /// </summary>
    /// <param name="screen">The pointer position relative to the editor.</param>
    /// <param name="modifiers">The keyboard modifiers held now.</param>
    public abstract void Move(AvaloniaPoint screen, KeyModifiers modifiers);

    /// <summary>
    ///     Handles the pointer release that finishes the gesture, committing its edits.
    /// </summary>
    /// <param name="screen">The pointer position relative to the editor.</param>
    /// <param name="modifiers">The keyboard modifiers held now.</param>
    public virtual void Complete(AvaloniaPoint screen, KeyModifiers modifiers)
    {
    }

    /// <summary>
    ///     Abandons the gesture, undoing any edits it made so far.
    /// </summary>
    public virtual void Cancel()
    {
    }

    /// <summary>
    ///     Draws transient feedback on top of the diagram.
    /// </summary>
    /// <param name="context">The drawing context, already in diagram coordinates.</param>
    /// <param name="painter">The painter that owns the brush and pen caches.</param>
    /// <param name="theme">The theme in effect.</param>
    /// <param name="zoom">The current zoom, for keeping line thicknesses constant on screen.</param>
    public virtual void Draw(DrawingContext context, ScenePainter painter, DiagramTheme theme, double zoom)
    {
    }

    /// <summary>
    ///     Measures how far a pointer position is from the press position.
    /// </summary>
    /// <param name="screen">The pointer position relative to the editor.</param>
    /// <returns>The distance in device-independent pixels.</returns>
    protected double DistanceFromStart(AvaloniaPoint screen) =>
        Math.Sqrt(Math.Pow(screen.X - StartScreen.X, 2) + Math.Pow(screen.Y - StartScreen.Y, 2));
}
