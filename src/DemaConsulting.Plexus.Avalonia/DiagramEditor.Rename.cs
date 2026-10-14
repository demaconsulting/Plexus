using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Inline rename: a text box floated over the node's name.
/// </content>
public sealed partial class DiagramEditor
{
    /// <summary>
    ///     The Automation Id of the inline rename text box.
    /// </summary>
    public const string RenameEditorAutomationId = "DiagramEditor.RenameEditor";

    /// <summary>
    ///     The text box shown while renaming, or null.
    /// </summary>
    private TextBox? _renameEditor;

    /// <summary>
    ///     The node being renamed, or null.
    /// </summary>
    private Node? _renameNode;

    /// <summary>
    ///     Gets a value indicating whether an inline rename is in progress.
    /// </summary>
    public bool IsRenaming => _renameEditor is not null;

    /// <summary>
    ///     Gets the reason the last commit attempt was rejected, or null while the text is acceptable.
    /// </summary>
    /// <remarks>
    ///     The reason is also shown as a tooltip on the text box. It comes from
    ///     <see cref="Diagram.TryRename"/>, so it includes anything a <see cref="Diagram.NodeRenaming"/> handler cancelled with.
    /// </remarks>
    public string? RenameError { get; private set; }

    /// <summary>
    ///     Gets the inline rename text box, or null when not renaming (internal so tests can type into it).
    /// </summary>
    internal TextBox? RenameEditor => _renameEditor;

    /// <summary>
    ///     Starts an inline rename of a node, with its current name selected.
    /// </summary>
    /// <remarks>
    ///     Enter commits through <see cref="Diagram.TryRename"/>; if the name is rejected the reason is
    ///     shown and editing continues. Escape cancels; clicking elsewhere commits when the name is
    ///     acceptable and cancels otherwise. Does nothing when the editor is read-only.
    /// </remarks>
    /// <param name="node">The node, which must be on the shown page.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public void BeginRename(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (IsReadOnly || !ReferenceEquals(node.Page, LivePage()))
        {
            return;
        }

        CancelEdit();
        var theme = EffectiveTheme;
        var editor = new TextBox
        {
            Text = node.Name,
            FontFamily = new FontFamily(theme.FontFamily),
            FontSize = theme.NameFontSize * Zoom,
            Padding = new Thickness(3, 0),
            MinHeight = 0,
            MinWidth = 0,
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
        };
        AutomationProperties.SetAutomationId(editor, RenameEditorAutomationId);
        editor.KeyDown += OnRenameKeyDown;
        editor.LostFocus += OnRenameLostFocus;
        editor.TextChanged += OnRenameTextChanged;

        // The box is a real child of the editor so it takes focus and input like any text box
        _renameNode = node;
        _renameEditor = editor;
        RenameError = null;
        VisualChildren.Add(editor);
        LogicalChildren.Add(editor);
        InvalidateMeasure();
        editor.Focus();
        editor.SelectAll();
    }

    /// <summary>
    ///     Abandons the inline rename without changing the node.
    /// </summary>
    public void CancelRename() => EndRename(refocus: true);

    /// <summary>
    ///     Tries to commit the inline rename.
    /// </summary>
    /// <remarks>
    ///     Exposed to tests and the automation layer so a rename can be committed without synthesizing a key press.
    /// </remarks>
    /// <returns><see langword="true"/> when the rename ended (committed, or nothing to commit); false when the name was rejected and editing continues.</returns>
    public bool CommitRename()
    {
        if (_renameEditor is not { } editor || _renameNode is not { } node)
        {
            return true;
        }

        var proposed = editor.Text ?? string.Empty;
        if (proposed == node.Name)
        {
            EndRename(refocus: true);
            return true;
        }

        // The rename pipeline (validation, uniqueness, host handlers) decides; its reason is for the user
        if (LivePage()?.Diagram.TryRename(node, proposed, out var reason) ?? true)
        {
            EndRename(refocus: true);
            return true;
        }

        RenameError = reason;
        ToolTip.SetTip(editor, reason);
        ToolTip.SetIsOpen(editor, true);
        editor.BorderBrush = Brushes.Red;
        return false;
    }

    /// <summary>
    ///     Determines whether an event originated inside an inline editor text box (rename or annotation text).
    /// </summary>
    /// <param name="e">The routed event arguments.</param>
    /// <returns><see langword="true"/> when the event came from an inline editor or its parts.</returns>
    private bool IsFromInlineEditor(RoutedEventArgs e) => IsFromEditorBox(_renameEditor, e) || IsFromEditorBox(_annotationEditor, e);

    /// <summary>
    ///     Determines whether an event originated inside one particular editor text box.
    /// </summary>
    /// <param name="editor">The text box, or null when none is open.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <returns><see langword="true"/> when the event came from the box or its parts.</returns>
    private static bool IsFromEditorBox(TextBox? editor, RoutedEventArgs e) =>
        editor is not null
        && e.Source is Visual source
        && (ReferenceEquals(source, editor) || editor.IsVisualAncestorOf(source));

    /// <summary>
    ///     Ends any inline edit when the user presses somewhere else: commit if acceptable, otherwise cancel.
    /// </summary>
    private void FinishEditOnOutsideInput()
    {
        if (IsEditing && !CommitEdit())
        {
            CancelEdit();
        }
    }

    /// <summary>
    ///     Computes where the rename box goes: over the node's name, at least as wide as the node.
    /// </summary>
    /// <returns>The rectangle relative to the editor, or null when the node is gone.</returns>
    private AvaloniaRect? ComputeRenameRect()
    {
        if (_renameNode is not { } node || !ReferenceEquals(node.Page, LivePage()))
        {
            return null;
        }

        // Center a box over the name line; the node's width is a comfortable minimum for typing
        var layout = NodeLayout.Compute(node, EffectiveTheme, _measurer);
        var name = layout.Name.Bounds;
        var width = Math.Max(layout.Box.Width, name.Width + 24);
        var topLeft = DiagramToScreen(new AvaloniaPoint(name.X + (name.Width / 2) - (width / 2), name.Y - 2));
        return new AvaloniaRect(topLeft, new Size(width * Zoom, (name.Height + 4) * Zoom));
    }

    /// <summary>
    ///     Removes the rename box and forgets the node.
    /// </summary>
    /// <param name="refocus">Whether to return keyboard focus to the editor.</param>
    private void EndRename(bool refocus)
    {
        if (_renameEditor is not { } editor)
        {
            return;
        }

        // Clear the fields first: removing the focused box raises LostFocus, which must see "no rename"
        _renameEditor = null;
        _renameNode = null;
        RenameError = null;
        editor.KeyDown -= OnRenameKeyDown;
        editor.LostFocus -= OnRenameLostFocus;
        editor.TextChanged -= OnRenameTextChanged;
        ToolTip.SetIsOpen(editor, false);
        LogicalChildren.Remove(editor);
        VisualChildren.Remove(editor);
        InvalidateMeasure();
        if (refocus)
        {
            Focus();
        }
    }

    /// <summary>
    ///     Handles Enter (commit) and Escape (cancel) in the rename box.
    /// </summary>
    /// <param name="sender">The rename box.</param>
    /// <param name="e">The key event.</param>
    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelRename();
            e.Handled = true;
        }
    }

    /// <summary>
    ///     Handles the rename box losing focus: commit when acceptable, otherwise cancel.
    /// </summary>
    /// <param name="sender">The rename box.</param>
    /// <param name="e">The event data.</param>
    private void OnRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, _renameEditor) && !CommitRename())
        {
            EndRename(refocus: false);
        }
    }

    /// <summary>
    ///     Clears the rejection feedback as soon as the user edits the text again.
    /// </summary>
    /// <param name="sender">The rename box.</param>
    /// <param name="e">The event data.</param>
    private void OnRenameTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox editor && RenameError is not null)
        {
            RenameError = null;
            ToolTip.SetIsOpen(editor, false);
            editor.ClearValue(TemplatedControl.BorderBrushProperty);
        }
    }
}
