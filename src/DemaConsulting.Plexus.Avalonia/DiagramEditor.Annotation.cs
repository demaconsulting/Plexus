using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaRect = Avalonia.Rect;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Inline annotation editing: a multi-line text box floated over the annotation, plus the shared
///     <see cref="IsEditing"/>, <see cref="CommitEdit"/> and <see cref="CancelEdit"/> entry points that cover rename too.
/// </content>
public sealed partial class DiagramEditor
{
    /// <summary>
    ///     The Automation Id of the inline annotation text box.
    /// </summary>
    public const string AnnotationEditorAutomationId = "DiagramEditor.AnnotationEditor";

    /// <summary>
    ///     The smallest on-screen size of the annotation text box, so tiny notes stay editable.
    /// </summary>
    private static readonly Size MinAnnotationEditorSize = new(80, 36);

    /// <summary>
    ///     The text box shown while editing an annotation, or null.
    /// </summary>
    private TextBox? _annotationEditor;

    /// <summary>
    ///     The annotation being edited, or null.
    /// </summary>
    private Annotation? _editAnnotation;

    /// <summary>
    ///     Gets a value indicating whether an inline annotation text edit is in progress.
    /// </summary>
    public bool IsEditingAnnotation => _annotationEditor is not null;

    /// <summary>
    ///     Gets a value indicating whether any inline edit (node rename or annotation text) is in progress.
    /// </summary>
    public bool IsEditing => IsRenaming || IsEditingAnnotation;

    /// <summary>
    ///     Gets the inline annotation text box, or null when not editing (internal so tests can type into it).
    /// </summary>
    internal TextBox? AnnotationEditor => _annotationEditor;

    /// <summary>
    ///     Starts an inline text edit of an annotation, with its current text selected.
    /// </summary>
    /// <remarks>
    ///     A multi-line text box is floated over the annotation (this is also what double-clicking an
    ///     annotation, or pressing F2 with a single annotation selected, does). Ctrl+Enter or moving the
    ///     focus elsewhere commits; Escape cancels; plain Enter inserts a line break. A commit that changed
    ///     the text is one undo entry, an unchanged text records nothing. Does nothing when the editor is read-only.
    /// </remarks>
    /// <param name="annotation">The annotation, which must be on the shown page.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="annotation"/> is null.</exception>
    public void BeginEditAnnotation(Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (IsReadOnly || !ReferenceEquals(annotation.Page, LivePage()))
        {
            return;
        }

        CancelEdit();
        var theme = EffectiveTheme;
        var editor = new TextBox
        {
            Text = annotation.Text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily(theme.FontFamily),
            FontSize = theme.AnnotationFontSize * Zoom,
            Padding = new Thickness(theme.AnnotationPadding * Zoom),
            MinHeight = 0,
            MinWidth = 0,
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Top,
        };
        AutomationProperties.SetAutomationId(editor, AnnotationEditorAutomationId);

        // Tunnel so Ctrl+Enter and Escape are seen before the text box turns them into text input
        editor.AddHandler(KeyDownEvent, OnAnnotationEditorKeyDown, RoutingStrategies.Tunnel);
        editor.LostFocus += OnAnnotationEditorLostFocus;

        // The box is a real child of the editor so it takes focus and input like any text box
        _editAnnotation = annotation;
        _annotationEditor = editor;
        VisualChildren.Add(editor);
        LogicalChildren.Add(editor);
        InvalidateMeasure();
        editor.Focus();
        editor.SelectAll();
    }

    /// <summary>
    ///     Tries to commit the inline edit in progress (rename or annotation text).
    /// </summary>
    /// <remarks>
    ///     An annotation edit always commits. A rename can be rejected by the rename pipeline, in which
    ///     case <see langword="false"/> is returned, <see cref="RenameError"/> explains why and editing continues.
    /// </remarks>
    /// <returns><see langword="true"/> when the edit ended (committed, or nothing to commit); false when a rename was rejected.</returns>
    public bool CommitEdit() => CommitRename() && CommitAnnotationEdit();

    /// <summary>
    ///     Abandons the inline edit in progress (rename or annotation text) without changing the diagram.
    /// </summary>
    /// <remarks>
    ///     Does nothing, and does not move keyboard focus, when no edit is in progress.
    /// </remarks>
    public void CancelEdit()
    {
        EndRename(refocus: true);
        EndAnnotationEdit(refocus: true);
    }

    /// <summary>
    ///     Commits the annotation text edit.
    /// </summary>
    /// <param name="refocus">Whether to return keyboard focus to the editor afterwards.</param>
    /// <returns>Always <see langword="true"/>; there is nothing to reject about free text.</returns>
    private bool CommitAnnotationEdit(bool refocus = true)
    {
        if (_annotationEditor is not { } editor || _editAnnotation is not { } annotation)
        {
            return true;
        }

        // Normalize line breaks so a text that was not really edited never produces a spurious undo entry
        var text = NormalizeLineBreaks(editor.Text ?? string.Empty);
        var changed = text != NormalizeLineBreaks(annotation.Text);
        EndAnnotationEdit(refocus);
        if (changed && ReferenceEquals(annotation.Page, LivePage()))
        {
            annotation.Text = text;
        }

        return true;
    }

    /// <summary>
    ///     Converts every line break to a single line feed, the form the model stores.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalized text.</returns>
    private static string NormalizeLineBreaks(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    /// <summary>
    ///     Computes where the annotation text box goes: exactly over the annotation, at least a usable minimum size.
    /// </summary>
    /// <returns>The rectangle relative to the editor, or null when the annotation is gone.</returns>
    private AvaloniaRect? ComputeAnnotationEditRect()
    {
        if (_editAnnotation is not { } annotation || !ReferenceEquals(annotation.Page, LivePage()))
        {
            return null;
        }

        var bounds = annotation.Bounds;
        var topLeft = DiagramToScreen(new global::Avalonia.Point(bounds.Left, bounds.Top));
        return new AvaloniaRect(
            topLeft,
            new Size(
                Math.Max(bounds.Width * Zoom, MinAnnotationEditorSize.Width),
                Math.Max(bounds.Height * Zoom, MinAnnotationEditorSize.Height)));
    }

    /// <summary>
    ///     Removes the annotation text box and forgets the annotation.
    /// </summary>
    /// <param name="refocus">Whether to return keyboard focus to the editor.</param>
    private void EndAnnotationEdit(bool refocus)
    {
        if (_annotationEditor is not { } editor)
        {
            return;
        }

        // Clear the fields first: removing the focused box raises LostFocus, which must see "no edit"
        _annotationEditor = null;
        _editAnnotation = null;
        editor.RemoveHandler(KeyDownEvent, OnAnnotationEditorKeyDown);
        editor.LostFocus -= OnAnnotationEditorLostFocus;
        LogicalChildren.Remove(editor);
        VisualChildren.Remove(editor);
        InvalidateMeasure();
        if (refocus)
        {
            Focus();
        }
    }

    /// <summary>
    ///     Handles Ctrl+Enter (commit) and Escape (cancel) in the annotation text box.
    /// </summary>
    /// <param name="sender">The annotation text box.</param>
    /// <param name="e">The key event.</param>
    private void OnAnnotationEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && IsCommandModifier(e.KeyModifiers))
        {
            CommitAnnotationEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            EndAnnotationEdit(refocus: true);
            e.Handled = true;
        }
    }

    /// <summary>
    ///     Handles the annotation text box losing focus: the text is committed.
    /// </summary>
    /// <param name="sender">The annotation text box.</param>
    /// <param name="e">The event data.</param>
    private void OnAnnotationEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        // Focus already moved on, so the commit must not steal it back
        if (ReferenceEquals(sender, _annotationEditor))
        {
            CommitAnnotationEdit(refocus: false);
        }
    }
}
