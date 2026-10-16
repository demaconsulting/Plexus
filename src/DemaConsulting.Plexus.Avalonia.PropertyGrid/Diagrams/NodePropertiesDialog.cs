using Avalonia.Controls;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

/// <summary>
///     The properties dialog of a Plexus node: a modal window that shows the node's name, properties and host data in a
///     staged <see cref="PropertyGrid"/>. OK applies everything as one undo entry; Cancel changes nothing.
/// </summary>
/// <remarks>
///     <para>
///         It is a thin layer over <see cref="PropertyGridDialog"/> and <see cref="NodePropertySource"/>: the title is
///         <c>"{Name} - Properties"</c>, the grid is staged, and the node adapter makes the commit one undo entry (the
///         rename, the changed properties and the edited host data together). OK is refused with the reason shown while a
///         value is invalid or the rename is vetoed. Custom editors named by <see cref="PropertyDefinition.EditorHint"/> or
///         <see cref="PropertyEditorAttribute"/> come from <see cref="PropertyGrid.DefaultEditors"/>, so register
///         them there once at start-up (see <see cref="PropertyGrid.EditorRegistry"/> for giving a single grid its own, which the dialog's
///         <see cref="PropertyGridDialog.Grid"/> from <see cref="Create(Node)"/> also accepts).
///     </para>
///     <para>
///         The dialog's controls carry the automation ids of the grid and its dialog: <c>PropertyGridDialog</c>,
///         <c>PropertyGridDialog_OK</c>, <c>PropertyGridDialog_Cancel</c>, <c>PropertyGridDialog_Status</c> and, for every
///         row, <c>PropertyGrid_Row_{path}</c> and <c>PropertyGrid_Editor_{path}</c> (for example
///         <c>PropertyGrid_Row_Node.Name</c>, see <see cref="NodePropertySource"/> for the row paths). A dialog with a
///         preview adds <c>PropertyGridDialog_Preview</c>, the panel that holds the preview control.
///     </para>
///     <para>
///         <b>Live preview.</b> <see cref="NodePropertiesDialogOptions.Preview"/> turns the dialog into a custom node
///         inspector: a control next to the grid (or above it, see <see cref="NodePropertiesDialogOptions.PreviewPlacement"/>)
///         that visualizes what the settings do and redraws while the user edits, before OK. The factory receives a
///         <see cref="NodePreviewContext"/> that reads the effective (staged) value of any row (the pending edit when there is
///         one, otherwise the node's current value) and raises <see cref="NodePreviewContext.Changed"/> on every edit, reset,
///         cancel and commit. The context has no write path, so the preview never mutates the node: OK still commits as one undo
///         entry and Cancel still changes nothing. "Cancel" in that list means <see cref="PropertyEditSession.Cancel"/>, which
///         discards every pending edit and notifies once; the dialog's Cancel button and closing the window do not cancel the
///         session and raise no notification, and OK notifies once for its commit, before the dialog closes. The context is
///         disposed as soon as the close is confirmed and again when the window has closed, so nothing is raised after that.
///         The dialog is 480 x 640 device-independent pixels without a preview; a side preview (400 wide) widens it to
///         880 x 640 and a top preview (220 high) makes it 480 x 860; <see cref="NodePropertiesDialogOptions.Width"/> and
///         <see cref="NodePropertiesDialogOptions.Height"/> override each dimension. Hosts that build their own window use
///         <see cref="NodePreviewContext"/> directly over their own grid or session.
///     </para>
/// </remarks>
/// <example>
///     One line in the window that hosts a <c>DiagramEditor</c>: double-click a node to edit it. The host data of the node (<c>Node.Data</c>) appears
///     in the dialog as rows when its node type declares a <c>DataType</c> (see <see cref="NodePropertySource"/>).
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
///
///     // Inside a Window (this), with the editor of the window
///     public sealed class EditorWindow : Window
///     {
///         public EditorWindow(DiagramEditor editor)
///         {
///             Content = editor;
///             editor.NodeDoubleClicked += async (_, e) =>
///             {
///                 bool changed = await NodePropertiesDialog.ShowAsync(this, e.Node);   // true when OK applied the edits as one undo entry
///             };
///         }
///     }
///     ]]></code>
/// </example>
/// <example>
///     A custom inspector: the same dialog with a live preview to the right of the grid that shows the staged frequency.
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
///
///     public static class InspectorSample
///     {
///         // owner: the window that hosts the DiagramEditor; node: the double-clicked node
///         public static Task<bool> InspectAsync(Window owner, Node node)
///         {
///             var options = new NodePropertiesDialogOptions
///             {
///                 Preview = context =>
///                 {
///                     var label = new TextBlock();
///                     void Redraw() => label.Text = $"{context.GetPropertyValue("Frequency")} Hz";   // pending edit, else the node's value
///                     context.Changed += (_, _) => Redraw();                                        // every edit, reset and cancel
///                     Redraw();
///                     return label;
///                 },
///             };
///             return NodePropertiesDialog.ShowAsync(owner, node, options);   // OK is still one undo entry; Cancel changes nothing
///         }
///     }
///     ]]></code>
/// </example>
public static class NodePropertiesDialog
{
    /// <summary>
    ///     The width of the dialog, in device-independent pixels.
    /// </summary>
    private const double DialogWidth = 480;

    /// <summary>
    ///     The height of the dialog, in device-independent pixels.
    /// </summary>
    private const double DialogHeight = 640;

    /// <summary>
    ///     The width of the dialog with a side preview, in device-independent pixels: the grid keeps its width and the
    ///     preview gets <see cref="SidePreviewWidth"/>.
    /// </summary>
    private const double SidePreviewDialogWidth = 880;

    /// <summary>
    ///     The height of the dialog with a top preview, in device-independent pixels: the grid keeps most of its height and
    ///     the preview gets <see cref="TopPreviewHeight"/>.
    /// </summary>
    private const double TopPreviewDialogHeight = 860;

    /// <summary>
    ///     The width of a side preview, in device-independent pixels.
    /// </summary>
    private const double SidePreviewWidth = 400;

    /// <summary>
    ///     The height of a top preview, in device-independent pixels.
    /// </summary>
    private const double TopPreviewHeight = 220;

    /// <summary>
    ///     Shows the properties dialog of a node modally.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="node">The node to edit; it must be attached to a diagram.</param>
    /// <returns>
    ///     A task that completes with <see langword="true"/> when the user pressed OK and the edits were committed as one
    ///     undo entry, and <see langword="false"/> when the user cancelled (nothing changed).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not attached to a diagram.</exception>
    public static Task<bool> ShowAsync(Window owner, Node node)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(node);
        return Create(node).ShowDialog<bool>(owner);
    }

    /// <summary>
    ///     Shows the properties dialog of a node modally, with the optional live preview, title and size of
    ///     <paramref name="options"/>.
    /// </summary>
    /// <remarks>
    ///     OK and Cancel behave as in <see cref="ShowAsync(Window, Node)"/>: OK commits the name, properties and data as one
    ///     undo entry, Cancel changes nothing. The preview only reads (see <see cref="NodePreviewContext"/>).
    /// </remarks>
    /// <param name="owner">The owner window.</param>
    /// <param name="node">The node to edit; it must be attached to a diagram.</param>
    /// <param name="options">The options; <see langword="null"/> gives the plain dialog.</param>
    /// <returns>
    ///     A task that completes with <see langword="true"/> when the user pressed OK and the edits were committed as one
    ///     undo entry, and <see langword="false"/> when the user cancelled (nothing changed).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not attached to a diagram.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The width or height of <paramref name="options"/> is not a positive finite number.</exception>
    /// <exception cref="InvalidOperationException">The preview factory returned <see langword="null"/>.</exception>
    public static Task<bool> ShowAsync(Window owner, Node node, NodePropertiesDialogOptions? options)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(node);
        return Create(node, options).ShowDialog<bool>(owner);
    }

    /// <summary>
    ///     Creates the dialog window without showing it (for hosts that show it themselves).
    /// </summary>
    /// <param name="node">The node to edit; it must be attached to a diagram.</param>
    /// <returns>The window; use <see cref="PropertyGridDialog.Accept"/> or <see cref="Window.Close()"/> to finish it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not attached to a diagram.</exception>
    public static PropertyGridDialog Create(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new PropertyGridDialog(node, GetTitle(node)) { Width = DialogWidth, Height = DialogHeight };
    }

    /// <summary>
    ///     Creates the dialog window with the optional live preview, title and size of <paramref name="options"/> without
    ///     showing it (for hosts that show it themselves).
    /// </summary>
    /// <remarks>
    ///     With a preview, the dialog creates a <see cref="NodePreviewContext"/> over its grid, calls the factory once, shows
    ///     the returned control in the panel <c>PropertyGridDialog_Preview</c> and disposes the context as soon as the close of
    ///     the window is confirmed (and again, idempotently, when it has closed). When the factory throws or returns
    ///     <see langword="null"/>, or its control cannot be shown, the context is disposed, the dialog's grid releases its
    ///     property source and session (so nothing keeps listening to the node), and the exception propagates.
    /// </remarks>
    /// <param name="node">The node to edit; it must be attached to a diagram.</param>
    /// <param name="options">The options; <see langword="null"/> gives the plain dialog.</param>
    /// <returns>The window; use <see cref="PropertyGridDialog.Accept"/> or <see cref="Window.Close()"/> to finish it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not attached to a diagram.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The width or height of <paramref name="options"/> is not a positive finite number.</exception>
    /// <exception cref="InvalidOperationException">The preview factory returned <see langword="null"/>.</exception>
    public static PropertyGridDialog Create(Node node, NodePropertiesDialogOptions? options)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Validate the size before anything is built so a bad option never leaves a half-made window
        var preview = options?.Preview;
        var side = options?.PreviewPlacement != NodePreviewPlacement.Top;
        var width = Validate(options?.Width, nameof(NodePropertiesDialogOptions.Width)) ?? (preview is not null && side ? SidePreviewDialogWidth : DialogWidth);
        var height = Validate(options?.Height, nameof(NodePropertiesDialogOptions.Height)) ?? (preview is not null && !side ? TopPreviewDialogHeight : DialogHeight);

        // Build the plain dialog; without a preview this is the same window as Create(node)
        var dialog = new PropertyGridDialog(node, options?.Title ?? GetTitle(node)) { Width = width, Height = height };
        if (preview is null)
        {
            return dialog;
        }

        // Give the factory a read-only view of the staged values and show its control next to the grid
        var context = new NodePreviewContext(dialog.Grid);
        try
        {
            var control = preview(context) ?? throw new InvalidOperationException("The preview factory returned null.");
            dialog.SetPreview(control, side ? Dock.Right : Dock.Top, side ? SidePreviewWidth : TopPreviewHeight);
        }
        catch
        {
            // The dialog is never handed out: release its grid's source (which listens to the node) and session along with the context
            context.Dispose();
            dialog.Grid.SelectedObject = null;
            throw;
        }

        // Stop the notifications as soon as the close is confirmed (OK or Cancel button, window close), before the grid is torn
        // down, and again (idempotent) when the window has closed, so the preview is never redrawn by a dialog that is closing
        dialog.CloseConfirmed += (_, _) => context.Dispose();
        dialog.Closed += (_, _) => context.Dispose();
        return dialog;
    }

    /// <summary>
    ///     Gets the title of a node's dialog: <c>"{Name} - Properties"</c>.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The title.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public static string GetTitle(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Name + " - Properties";
    }

    /// <summary>
    ///     Checks an optional dialog dimension.
    /// </summary>
    /// <param name="value">The dimension, or <see langword="null"/> for the default.</param>
    /// <param name="name">The option name, for the exception.</param>
    /// <returns>The dimension, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a positive finite number.</exception>
    private static double? Validate(double? value, string name)
    {
        if (value is { } size && (!double.IsFinite(size) || size <= 0))
        {
            throw new ArgumentOutOfRangeException(name, size, $"The dialog {name.ToLowerInvariant()} must be a positive finite number.");
        }

        return value;
    }
}
