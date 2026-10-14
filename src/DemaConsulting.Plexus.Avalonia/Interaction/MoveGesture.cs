using Avalonia.Input;
using AvaloniaPoint = Avalonia.Point;
using PlexusPage = DemaConsulting.Plexus.DiagramPage;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia.Interaction;

/// <summary>
///     Drags the selection (or duplicates it first when Ctrl is held) and keeps wires re-routed live.
/// </summary>
/// <remarks>
///     <para>
///         The press has already updated the selection. Nothing is edited until the pointer travels past
///         the drag threshold, so a plain click never creates an undo entry. From then on every move
///         applies only the change since the previous move through <see cref="DiagramPage.Move"/> (which
///         re-routes wires and moves group members), all inside one history transaction: the whole drag
///         is a single undo step, and Escape rolls it back completely.
///     </para>
///     <para>
///         Snapping is applied to the dragged element's own top-left corner rather than to the pointer
///         delta, so the element lands on the grid even when it started off-grid. A press that never
///         turned into a drag finishes the deferred selection change on release: clicking an already
///         selected element among several narrows the selection to it, and Shift/Ctrl-clicking one
///         removes it.
///     </para>
/// </remarks>
internal sealed class MoveGesture : EditorGesture
{
    /// <summary>
    ///     The element that was pressed; its position is what snaps to the grid.
    /// </summary>
    private readonly DiagramElement _pressed;

    /// <summary>
    ///     Whether the pressed element was already selected before the press.
    /// </summary>
    private readonly bool _wasSelected;

    /// <summary>
    ///     The diagram position of the press.
    /// </summary>
    private readonly PlexusPoint _startDiagram;

    /// <summary>
    ///     The top-left corner of the pressed element at the start of the drag.
    /// </summary>
    private readonly PlexusPoint _origin;

    /// <summary>
    ///     The selection as it was when the press happened, restored if the drag is cancelled.
    /// </summary>
    private readonly List<DiagramElement> _selectionBefore;

    /// <summary>
    ///     The open transaction, or null until the drag started.
    /// </summary>
    private HistoryTransaction? _transaction;

    /// <summary>
    ///     The elements being moved (the originals, or the copies after a Ctrl-drag).
    /// </summary>
    private List<DiagramElement> _moving = [];

    /// <summary>
    ///     The total offset applied to <see cref="_moving"/> so far.
    /// </summary>
    private PlexusPoint _applied;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MoveGesture"/> class.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="startScreen">The control-relative position of the press.</param>
    /// <param name="pressed">The element under the pointer at the press.</param>
    /// <param name="wasSelected">Whether that element was already selected before the press changed the selection.</param>
    /// <param name="selectionBefore">The selection before the press.</param>
    public MoveGesture(DiagramEditor editor, AvaloniaPoint startScreen, DiagramElement pressed, bool wasSelected, IEnumerable<DiagramElement> selectionBefore)
        : base(editor, startScreen)
    {
        _pressed = pressed;
        _wasSelected = wasSelected;
        _selectionBefore = [.. selectionBefore];
        _startDiagram = editor.ToDiagram(startScreen);
        _origin = PositionOf(pressed);
    }

    /// <inheritdoc />
    public override StandardCursorType? Cursor => _transaction is null ? null : StandardCursorType.SizeAll;

    /// <inheritdoc />
    public override void Move(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        if (Editor.LivePage() is not { } page)
        {
            return;
        }

        // Stay a click until the pointer has clearly moved
        if (_transaction is null)
        {
            if (DistanceFromStart(screen) < DragThreshold)
            {
                return;
            }

            Begin(page, (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0);
        }

        // Where the pressed element wants to be, snapped, expressed as a total offset from where it started
        var pointer = Editor.ToDiagram(screen);
        var targetX = Editor.Snap(_origin.X + (pointer.X - _startDiagram.X));
        var targetY = Editor.Snap(_origin.Y + (pointer.Y - _startDiagram.Y));
        var total = new PlexusPoint(targetX - _origin.X, targetY - _origin.Y);

        // Apply only the difference so the model sees small, mergeable steps
        var step = total - _applied;
        if (step.X != 0 || step.Y != 0)
        {
            page.Move(_moving, step.X, step.Y);
            _applied = total;
        }
    }

    /// <inheritdoc />
    public override void Complete(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        // A real drag commits as one undo entry
        if (_transaction is not null)
        {
            _transaction.Dispose();
            _transaction = null;
            return;
        }

        // A click: finish the selection change that was deferred at the press
        if (!_wasSelected)
        {
            return;
        }

        if ((modifiers & (KeyModifiers.Shift | KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            Editor.SetSelection(Editor.Selection.Where(e => !ReferenceEquals(e, _pressed)));
        }
        else
        {
            Editor.SetSelection([_pressed]);
        }
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        // Rolling back also removes any Ctrl-drag copies; the old selection is then valid again
        if (_transaction is not null)
        {
            _transaction.Rollback();
            _transaction = null;
            Editor.SetSelection(_selectionBefore);
        }
    }

    /// <summary>
    ///     Gets the position that snaps to the grid while an element is dragged.
    /// </summary>
    /// <remarks>
    ///     For an expanded group this is the first node or annotation inside it rather than the
    ///     frame: the frame includes padding and a title bar whose sizes need not be grid multiples,
    ///     and snapping those would leave the members themselves off the grid.
    /// </remarks>
    /// <param name="element">The element.</param>
    /// <returns>The position, or the origin for kinds that have none.</returns>
    private static PlexusPoint PositionOf(DiagramElement element) => element switch
    {
        Node node => node.Bounds.Position,
        Annotation annotation => annotation.Bounds.Position,
        Group { IsCollapsed: false } group when FirstLeaf(group) is { } leaf => PositionOf(leaf),
        Group group => group.Bounds.Position,
        _ => PlexusPoint.Zero,
    };

    /// <summary>
    ///     Finds the first node or annotation among a group's members, searching nested groups too.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The first leaf member, or null when the group holds only empty groups.</returns>
    private static DiagramElement? FirstLeaf(Group group) =>
        group.AllMembers().FirstOrDefault(m => m is Node or Annotation);

    /// <summary>
    ///     Starts the drag: opens the transaction and, for a Ctrl-drag, replaces the selection by its copies.
    /// </summary>
    /// <param name="page">The page being edited.</param>
    /// <param name="duplicate">Whether to duplicate the selection first.</param>
    private void Begin(PlexusPage page, bool duplicate)
    {
        _transaction = page.Diagram.History.BeginTransaction(duplicate ? "Duplicate" : "Move");
        _moving = Editor.MovableSelection();
        if (!duplicate)
        {
            return;
        }

        // Copies appear exactly on top of the originals; the drag then carries the copies away
        var copies = page.Duplicate(_moving, 0, 0);
        Editor.SetSelection(copies);
        _moving = Editor.MovableSelection();
    }
}
