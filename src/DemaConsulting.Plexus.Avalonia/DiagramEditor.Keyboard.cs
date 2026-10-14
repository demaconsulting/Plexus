using Avalonia.Input;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Keyboard shortcuts.
/// </content>
public sealed partial class DiagramEditor
{
    /// <summary>
    ///     Handles the keyboard shortcuts: Delete, Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z, Ctrl+C, Ctrl+X, Ctrl+V, Ctrl+A, Ctrl+D, F2, Escape, and Space for pan mode (see the type remarks for what each does). Shortcuts are ignored while an inline edit box has the keyboard. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        ArgumentNullException.ThrowIfNull(e);

        // While an inline editor is open its text box owns the keyboard (it also bubbles keys up to us)
        if (IsEditing || IsFromInlineEditor(e))
        {
            return;
        }

        var command = IsCommandModifier(e.KeyModifiers);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        e.Handled = e.Key switch
        {
            Key.Space when !command => HoldSpace(),
            Key.Escape => EscapeGesture(),
            Key.Delete when !command => RunEdit(DeleteSelection),
            Key.Z when command && shift => RunHistory(Redo),
            Key.Z when command => RunHistory(Undo),
            Key.Y when command => RunHistory(Redo),
            Key.A when command => RunSelection(SelectAll),
            Key.D when command => RunEdit(() => DuplicateSelection()),
            Key.C when command => RunClipboard(CopyAsync),
            Key.X when command => RunClipboard(CutAsync),
            Key.V when command => RunClipboard(() => PasteAsync()),
            Key.F2 when !command => RunEdit(EditSelected),
            _ => false,
        };
    }

    /// <summary>
    ///     Leaves pan mode when Space is released. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key == Key.Space && _spaceDown)
        {
            _spaceDown = false;
            if (_gesture is null)
            {
                SetCursor(null);
            }
        }
    }

    /// <summary>
    ///     Leaves pan mode when the editor loses keyboard focus, so a key-up that happens elsewhere cannot leave it stuck. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);

        // A key-up that happens elsewhere would otherwise leave the editor stuck in pan mode
        _spaceDown = false;
    }

    /// <summary>
    ///     Enters pan mode while Space is held.
    /// </summary>
    /// <returns>Always <see langword="true"/>: Space is consumed so it does not scroll or click a parent.</returns>
    private bool HoldSpace()
    {
        _spaceDown = true;
        if (_gesture is null)
        {
            SetCursor(StandardCursorType.Hand);
        }

        return true;
    }

    /// <summary>
    ///     Handles Escape: cancels the running gesture.
    /// </summary>
    /// <returns><see langword="true"/> when there was a gesture to cancel (the key is then consumed).</returns>
    private bool EscapeGesture()
    {
        if (_gesture is null)
        {
            return false;
        }

        CancelGesture();
        return true;
    }

    /// <summary>
    ///     Runs an editing command unless the editor is read-only or a drag is in progress.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>Always <see langword="true"/>, so the shortcut is consumed even when it did nothing.</returns>
    private bool RunEdit(Action command)
    {
        if (!IsReadOnly && _gesture is null && LivePage() is not null)
        {
            command();
        }

        return true;
    }

    /// <summary>
    ///     Runs an undo or redo shortcut.
    /// </summary>
    /// <param name="command">The history operation.</param>
    /// <returns>Always <see langword="true"/>, so the shortcut is consumed even when there was nothing to do.</returns>
    private static bool RunHistory(Func<bool> command)
    {
        command();
        return true;
    }

    /// <summary>
    ///     Runs a command that only affects the selection, which read-only editors allow.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool RunSelection(Action command)
    {
        command();
        return true;
    }

    /// <summary>
    ///     Starts a clipboard command without blocking the key handler.
    /// </summary>
    /// <remarks>
    ///     Key handlers are synchronous but the OS clipboard is asynchronous, so the command is
    ///     started and observed here. Clipboard access can legitimately fail (another process holds
    ///     it, the session has none); that must not crash the application from an async-void-like
    ///     path, so failures are reported through <see cref="ClipboardFailed"/> and the key press
    ///     otherwise has no effect.
    /// </remarks>
    /// <param name="command">The asynchronous command.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private bool RunClipboard(Func<Task> command)
    {
        _ = ObserveAsync(command);
        return true;
    }

    /// <summary>
    ///     Awaits a clipboard command and reports platform failures through <see cref="ClipboardFailed"/>.
    /// </summary>
    /// <param name="command">The asynchronous command.</param>
    /// <returns>A task that always completes successfully.</returns>
    private async Task ObserveAsync(Func<Task> command)
    {
        try
        {
            await command().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.ExternalException or IOException or NotSupportedException)
        {
            // Nobody awaits a key-initiated command, so the failure is handed to the host instead of being lost
            ClipboardFailed?.Invoke(this, new ClipboardFailedEventArgs(ex));
        }
    }

    /// <summary>
    ///     Starts the inline editor of the selected element: a rename for a node, a text edit for an annotation.
    /// </summary>
    /// <remarks>
    ///     Only acts when exactly one element is selected and it is a node or an annotation.
    /// </remarks>
    private void EditSelected()
    {
        if (_selection.Count != 1)
        {
            return;
        }

        switch (_selection[0])
        {
            case Node node:
                BeginRename(node);
                break;
            case Annotation annotation:
                BeginEditAnnotation(annotation);
                break;
        }
    }

    /// <summary>
    ///     Undoes the most recent change to the diagram.
    /// </summary>
    /// <remarks>
    ///     When a gesture (move, connect, ...) or an inline edit (rename, annotation text) is in
    ///     flight, Undo ONLY cancels it: the half-made change is discarded and the history is left
    ///     untouched, so a second Undo is needed to revert the previous completed entry. Otherwise it
    ///     reverts the most recent history entry. This is what Ctrl+Z calls; hosts use it for their
    ///     Edit menu. Does nothing when the editor is read-only or shows no page. To enable or disable a menu item, use
    ///     <c>Diagram.History.CanUndo</c> and refresh it from <c>Diagram.History.Changed</c>. Undo throws while a host history transaction is open,
    ///     so never leave a <c>BeginTransaction</c> scope open across a modal dialog or any other wait for the user.
    /// </remarks>
    /// <returns>
    ///     <see langword="true"/> when an in-flight gesture or inline edit was cancelled, or an entry was undone;
    ///     <see langword="false"/> when there was nothing to cancel or undo, or the editor cannot edit.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the host has a history transaction open.</exception>
    public bool Undo()
    {
        if (IsReadOnly || LivePage() is not { } page)
        {
            return false;
        }

        // An interaction in flight consumes the request: cancelling it is the whole undo.
        if (_gesture is not null || IsEditing)
        {
            CancelInteractions();
            return true;
        }

        return page.Diagram.History.Undo();
    }

    /// <summary>
    ///     Re-applies the most recently undone change to the diagram.
    /// </summary>
    /// <remarks>
    ///     Symmetric with <see cref="Undo"/>: when a gesture or inline edit is in flight, Redo ONLY
    ///     cancels it (the history is left untouched) and a second Redo is needed to re-apply the
    ///     undone entry. Otherwise it re-applies the most recently undone entry. This is what Ctrl+Y
    ///     and Ctrl+Shift+Z call. Does nothing when the editor is read-only or shows no page.
    /// </remarks>
    /// <returns>
    ///     <see langword="true"/> when an in-flight gesture or inline edit was cancelled, or an entry was redone;
    ///     <see langword="false"/> when there was nothing to cancel or redo, or the editor cannot edit.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the host has a history transaction open.</exception>
    public bool Redo()
    {
        if (IsReadOnly || LivePage() is not { } page)
        {
            return false;
        }

        // An interaction in flight consumes the request: cancelling it is the whole redo.
        if (_gesture is not null || IsEditing)
        {
            CancelInteractions();
            return true;
        }

        return page.Diagram.History.Redo();
    }

    /// <summary>
    ///     Abandons whatever the user is in the middle of: the running gesture and any inline edit.
    /// </summary>
    private void CancelInteractions()
    {
        CancelGesture();
        CancelEdit();
    }
}
