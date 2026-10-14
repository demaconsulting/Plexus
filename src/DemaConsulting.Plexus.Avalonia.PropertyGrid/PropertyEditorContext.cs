namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     What an editor control of a <see cref="PropertyGrid"/> row knows and can do: the item, its current value and
///     text, and the writes that go through the grid's <see cref="PropertyEditSession"/>.
/// </summary>
/// <remarks>
///     <para>
///         Editors write only through <see cref="SetValue"/> and <see cref="SetText"/>, so staged mode, validation, error
///         display and the host's commit scope apply uniformly. A failed write is remembered by the session as the row's
///         error (shown by the grid as a red border with the reason as a tooltip); for text, the rejected text is kept
///         in <see cref="PendingText"/> so the user can fix it.
///     </para>
///     <para>
///         The grid raises <see cref="Refreshed"/> whenever the row's value, pending state or error may have changed,
///         <see cref="CommitRequested"/> when the user presses Enter, and <see cref="CancelRequested"/> when the user
///         presses Escape; an editor that holds uncommitted input should honor both.
///     </para>
/// </remarks>
/// <example>
///     A text editor written from scratch: it shows the row's text (or the rejected text the user still has to fix), writes through the context, and honors Enter and
///     Escape. Register it as described on <see cref="PropertyGrid.DefaultEditors"/> (all grids) or <see cref="PropertyGrid.EditorRegistry"/> (one grid), for example
///     <c>PropertyGrid.DefaultEditors.Register("plain-text", TextEditorSample.Create);</c>. A slider that writes a number is shown on <see cref="PropertyGrid.DefaultEditors"/>.
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     public static class TextEditorSample
///     {
///         public static Control Create(PropertyEditorContext context)
///         {
///             var box = new TextBox { Text = context.PendingText ?? context.Text, IsReadOnly = context.IsReadOnly };
///             box.LostFocus += (_, _) => context.SetText(box.Text ?? "");       // validated and converted by the item; a failure becomes the row's error
///             context.CommitRequested += (_, _) => context.SetText(box.Text ?? "");   // Enter
///             context.CancelRequested += (_, _) => context.Cancel();                   // Escape: drops the pending edit and shows the value again
///             context.Refreshed += (_, _) => box.Text = context.PendingText ?? context.Text;
///             return box;
///         }
///     }
///     ]]></code>
/// </example>
public sealed class PropertyEditorContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyEditorContext"/> class.
    /// </summary>
    /// <param name="grid">The grid that hosts the row.</param>
    /// <param name="item">The item the row edits.</param>
    internal PropertyEditorContext(PropertyGrid grid, PropertyItem item)
    {
        Grid = grid;
        Item = item;
    }

    /// <summary>
    ///     Occurs when the row's value, pending state or error may have changed and the editor should show them again.
    /// </summary>
    public event EventHandler? Refreshed;

    /// <summary>
    ///     Occurs when the user asks to commit the input in progress (Enter).
    /// </summary>
    public event EventHandler? CommitRequested;

    /// <summary>
    ///     Occurs when the user asks to abandon the input in progress (Escape).
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    ///     Gets the grid that hosts the row.
    /// </summary>
    public PropertyGrid Grid { get; }

    /// <summary>
    ///     Gets the item the row edits.
    /// </summary>
    public PropertyItem Item { get; }

    /// <summary>
    ///     Gets the edit session the writes go through.
    /// </summary>
    public PropertyEditSession Session => Grid.Session!;

    /// <summary>
    ///     Gets a value indicating whether the value cannot be edited.
    /// </summary>
    public bool IsReadOnly => Item.IsReadOnly;

    /// <summary>
    ///     Gets the current value: the pending value in staged mode, else the target's value.
    /// </summary>
    public object? Value => Session.GetValue(Item);

    /// <summary>
    ///     Gets the current value as the text a user sees (see <see cref="PropertyEditSession.GetText"/>).
    /// </summary>
    public string Text => Session.GetText(Item);

    /// <summary>
    ///     Gets the text the user typed that the item rejected, or <see langword="null"/> when there is none. An editor
    ///     shows it instead of <see cref="Text"/> until it is fixed or cancelled.
    /// </summary>
    public string? PendingText { get; private set; }

    /// <summary>
    ///     Gets the current error message of the row, or <see langword="null"/>.
    /// </summary>
    public string? Error => Session.GetError(Item);

    /// <summary>
    ///     Writes a value through the session (immediately in live mode, buffered in staged mode).
    /// </summary>
    /// <param name="value">The new value.</param>
    /// <returns>The outcome; a failure is also the row's error.</returns>
    public PropertyResult SetValue(object? value)
    {
        PendingText = null;
        return Session.SetValue(Item, value);
    }

    /// <summary>
    ///     Converts text typed by the user and writes the result through the session. Rejected text stays available as
    ///     <see cref="PendingText"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The outcome; a failure is also the row's error.</returns>
    public PropertyResult SetText(string text)
    {
        PendingText = null;
        var result = Session.SetText(Item, text);
        if (!result.Succeeded)
        {
            PendingText = text;
        }

        Refreshed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    /// <summary>
    ///     Rejects text the editor itself could not interpret: the row shows the error and the text stays available as
    ///     <see cref="PendingText"/> so the user can fix it.
    /// </summary>
    /// <param name="text">The rejected text.</param>
    /// <param name="message">The reason shown to the user.</param>
    /// <returns>A failed result carrying the message.</returns>
    public PropertyResult RejectText(string text, string message)
    {
        PendingText = text;
        var result = Session.ReportError(Item, message);
        Refreshed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    /// <summary>
    ///     Abandons the input in progress and the row's pending edit and error, and has the editor show the value again.
    /// </summary>
    public void Cancel()
    {
        PendingText = null;
        Session.Discard(Item);
        Refreshed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Raises <see cref="Refreshed"/>.
    /// </summary>
    internal void RaiseRefreshed() => Refreshed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    ///     Raises <see cref="CommitRequested"/>.
    /// </summary>
    internal void RaiseCommitRequested() => CommitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    ///     Raises <see cref="CancelRequested"/>.
    /// </summary>
    internal void RaiseCancelRequested() => CancelRequested?.Invoke(this, EventArgs.Empty);
}
